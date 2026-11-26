using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace RobRunPotato;

/// <summary>Ищет exe игры ROB RUN в библиотеках Steam и среди запущенных процессов.</summary>
internal static class GameLocator
{
    private static readonly string[] Junk =
        { "unitycrashhandler", "easyanticheat", "eac", "start_protected", "unins", "crashreport", "launcher" };

    public static List<string> FindCandidates()
    {
        var result = new List<string>();
        try { result.AddRange(FromRunningProcesses()); } catch { /* нет доступа к процессу — не страшно */ }
        try { result.AddRange(FromSteam()); } catch { }
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool LooksLikeRobRun(string name)
    {
        string n = name.Replace(" ", "").Replace("_", "").Replace("-", "").ToLowerInvariant();
        return n.Contains("robrun");
    }

    private static IEnumerable<string> FromRunningProcesses()
    {
        foreach (var p in Process.GetProcesses())
        {
            string? path = null;
            try
            {
                if (!LooksLikeRobRun(p.ProcessName)) continue;
                path = p.MainModule?.FileName;
            }
            catch { }
            finally { p.Dispose(); }
            if (path != null && !IsJunk(Path.GetFileName(path))) yield return path;
        }
    }

    private static bool IsJunk(string exeName)
    {
        string n = exeName.ToLowerInvariant();
        return Junk.Any(n.StartsWith);
    }

    private static IEnumerable<string> SteamLibraries()
    {
        string? steam = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string
                        ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string;
        if (string.IsNullOrEmpty(steam)) yield break;
        steam = steam.Replace('/', '\\');
        yield return steam;

        string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf)) yield break;
        foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"(.+?)\""))
            yield return m.Groups[1].Value.Replace(@"\\", @"\");
    }

    private static IEnumerable<string> FromSteam()
    {
        foreach (string lib in SteamLibraries().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string common = Path.Combine(lib, "steamapps", "common");
            if (!Directory.Exists(common)) continue;

            foreach (string gameDir in Directory.GetDirectories(common))
            {
                if (!LooksLikeRobRun(Path.GetFileName(gameDir))) continue;

                var exes = Directory.GetFiles(gameDir, "*.exe", SearchOption.TopDirectoryOnly)
                                    .Where(e => !IsJunk(Path.GetFileName(e)))
                                    .ToList();

                // Признак Unity: рядом с основным exe лежит папка «<Имя>_Data»
                var unityMain = exes.Where(e =>
                    Directory.Exists(Path.Combine(gameDir, Path.GetFileNameWithoutExtension(e) + "_Data"))).ToList();

                foreach (string e in unityMain.Count > 0 ? unityMain : exes)
                    yield return e;
            }
        }
    }
}
