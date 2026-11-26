using System.Diagnostics;
using System.Text.RegularExpressions;

namespace RobRunPotato;

/// <summary>
/// Пытается понять, почему настройки драйвера не действуют:
///  1) какой exe игра запускает НА САМОМ ДЕЛЕ (из лога Unity и запущенных процессов);
///  2) какой графический API использует игра (DX11 / DX12 / Vulkan).
/// Unity пишет всё это в %USERPROFILE%\AppData\LocalLow\&lt;Компания&gt;\&lt;Игра&gt;\Player.log
/// </summary>
internal static class Diagnostics
{
    public sealed class Result
    {
        public string? LogPath;
        public string? GraphicsApi;     // «Direct3D 11.0 [level 11.1]» и т.п.
        public string? RealExePath;     // по папке «<Имя>_Data» из лога
        public List<string> RunningGameExes = new();
        public bool IsDx12 => GraphicsApi?.Contains("12") == true && GraphicsApi.Contains("Direct3D");
        public bool IsVulkan => GraphicsApi?.Contains("Vulkan", StringComparison.OrdinalIgnoreCase) == true;
    }

    public static Result Run()
    {
        var r = new Result();
        try { ReadUnityLog(r); } catch { }
        try { r.RunningGameExes = FindRunningGameExes(); } catch { }
        return r;
    }

    private static IEnumerable<string> CandidateLogs()
    {
        string localLow = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow");
        if (!Directory.Exists(localLow)) yield break;

        foreach (string company in Directory.GetDirectories(localLow))
        {
            bool kefir = Path.GetFileName(company).Contains("kefir", StringComparison.OrdinalIgnoreCase);
            string[] games;
            try { games = Directory.GetDirectories(company); } catch { continue; }
            foreach (string game in games)
            {
                string gn = Path.GetFileName(game).Replace(" ", "").ToLowerInvariant();
                if (!kefir && !gn.Contains("robrun")) continue;
                foreach (string name in new[] { "Player.log", "Player-prev.log" })
                {
                    string p = Path.Combine(game, name);
                    if (File.Exists(p)) yield return p;
                }
            }
        }
    }

    private static string ReadShared(string path)
    {
        // Лог может быть открыт игрой — читаем с FileShare.ReadWrite
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var sr = new StreamReader(fs);
        return sr.ReadToEnd();
    }

    private static void ReadUnityLog(Result r)
    {
        var log = CandidateLogs().OrderByDescending(File.GetLastWriteTime).FirstOrDefault();
        if (log == null) return;
        r.LogPath = log;
        string text = ReadShared(log);

        // Графический API
        var m = Regex.Match(text, @"Version:\s+(Direct3D\s+\d+(\.\d+)?[^\r\n]*)");
        if (m.Success) r.GraphicsApi = m.Groups[1].Value.Trim();
        else if (Regex.IsMatch(text, @"Vulkan", RegexOptions.IgnoreCase) &&
                 Regex.IsMatch(text, @"Vulkan API version|GfxDevice: creating device client; kGfxThreadingMode.*Vulkan|Initialize engine.*Vulkan", RegexOptions.IgnoreCase))
            r.GraphicsApi = "Vulkan";

        // Путь к «<Имя>_Data» → настоящий exe = «<Имя>.exe» рядом с этой папкой
        var d = Regex.Match(text, @"([A-Za-z]:[\\/][^'""\r\n<>|]*?)[\\/]([^\\/'""\r\n<>|]+)_Data[\\/]");
        if (d.Success)
        {
            string dir = d.Groups[1].Value.Replace('/', '\\');
            string exe = Path.Combine(dir, d.Groups[2].Value + ".exe");
            r.RealExePath = exe;
        }
    }

    /// <summary>Запущенные процессы, рядом с exe которых лежит папка «&lt;Имя&gt;_Data» и путь похож на ROB RUN.</summary>
    private static List<string> FindRunningGameExes()
    {
        var list = new List<string>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                string? path = p.MainModule?.FileName;
                if (path == null) continue;
                string norm = path.Replace(" ", "").Replace("_", "").ToLowerInvariant();
                if (!norm.Contains("robrun")) continue;
                string dataDir = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "_Data");
                if (Directory.Exists(dataDir)) list.Add(path);
            }
            catch { /* системные/защищённые процессы */ }
            finally { p.Dispose(); }
        }
        return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Готовый текст отчёта для окна программы.</summary>
    public static List<string> Report(Result r, string? selectedExe)
    {
        var lines = new List<string> { "=== ДИАГНОСТИКА ===" };

        // 1. exe
        string? realName = r.RunningGameExes.Select(Path.GetFileName).FirstOrDefault()
                           ?? (r.RealExePath != null ? Path.GetFileName(r.RealExePath) : null);
        lines.Add("");
        lines.Add("[1] Какой exe использует игра");
        if (r.RunningGameExes.Count > 0)
            foreach (var e in r.RunningGameExes) lines.Add("  Запущен: " + e);
        if (r.RealExePath != null) lines.Add("  По логу Unity: " + r.RealExePath);
        if (realName == null)
            lines.Add("  Не удалось определить. Запустите игру, дождитесь меню и нажмите «Диагностика» ещё раз.");
        else if (selectedExe != null && !realName.Equals(selectedExe, StringComparison.OrdinalIgnoreCase))
        {
            lines.Add($"  ❌ В программе выбран «{selectedExe}», а игра работает как «{realName}»!");
            lines.Add("     Это главная причина, почему ничего не меняется. Нажмите «Исправить exe».");
        }
        else
            lines.Add($"  ✔ Выбран правильный exe: {realName}");

        // 2. API
        lines.Add("");
        lines.Add("[2] Графический API игры");
        if (r.LogPath != null) lines.Add("  Лог: " + r.LogPath);
        if (r.GraphicsApi == null)
            lines.Add("  Не найден в логе (запустите игру хотя бы раз).");
        else
        {
            lines.Add("  " + r.GraphicsApi);
            if (r.IsDx12 || r.IsVulkan)
            {
                lines.Add("  ❌ В DirectX 12 / Vulkan драйвер NVIDIA НЕ умеет менять LOD Bias и анизотропию —");
                lines.Add("     эти настройки физически не действуют. Работают только V-Sync, питание, лимит кадров.");
                lines.Add("     Попробуйте в Steam → ROB RUN → Свойства → Параметры запуска: -force-d3d11");
                lines.Add("     (сработает, если игра собрана с поддержкой DX11).");
            }
            else
                lines.Add("  ✔ DirectX 11 — LOD Bias и фильтрация через драйвер в принципе работают.");
        }

        lines.Add("");
        lines.Add("[3] Не забудьте: после «Применить» игру нужно ПОЛНОСТЬЮ перезапустить.");
        return lines;
    }
}
