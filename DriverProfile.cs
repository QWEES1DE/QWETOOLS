namespace RobRunPotato;

/// <summary>Высокоуровневые операции с профилем драйвера для одного exe.</summary>
internal static class DriverProfile
{
    public const string OwnProfileName = "ROB RUN (Potato Slider)";
    public static string? LastProfileName { get; private set; }

    private static T WithSession<T>(Func<IntPtr, T> action)
    {
        NvApi.Initialize();
        IntPtr h = NvApi.CreateSession();
        try
        {
            NvApi.LoadSettings(h);
            return action(h);
        }
        finally { NvApi.DestroySession(h); }
    }

    /// <summary>
    /// Удаляет все профили, в которых привязан этот exe (включая профили, созданные/правленные
    /// в Панели управления NVIDIA), а также наш старый профиль «{OwnProfileName}» без привязки.
    /// Привязку в глобальном (базовом) профиле снимает отдельно — она не видна при перечислении.
    /// Связанные с игрой профили, где exe нет, не трогаем.
    /// </summary>
    private static void WipeExeFromAllProfiles(IntPtr h, string exeName, List<string> log)
    {
        string exeLower = exeName.ToLowerInvariant();

        // Удаление профилей сдвигает нумерацию, поэтому всегда идём с текущего индекса:
        // после удаления профиля на его место становится следующий.
        uint numProfiles = NvApi.GetNumProfiles(h);
        uint guard = numProfiles * 2 + 32; // защита от зацикливания при изменениях драйвера на лету
        uint index = 0;
        while (guard-- > 0)
        {
            IntPtr? maybeProfile = NvApi.EnumProfileByIndex(h, index);
            if (maybeProfile is not IntPtr hProfile) break; // профили закончились

            string profileName = NvApi.GetProfileName(h, hProfile);
            bool isOurs = string.Equals(profileName, OwnProfileName, StringComparison.OrdinalIgnoreCase);

            uint appCount = NvApi.GetProfileAppCount(h, hProfile);
            bool hasExe = false;
            for (uint i = 0; i < appCount && !hasExe; i++)
            {
                string? appName = NvApi.EnumApplicationName(h, hProfile, i);
                if (appName == null) break;
                if (string.Equals(appName, exeLower, StringComparison.OrdinalIgnoreCase)) hasExe = true;
            }

            if (!hasExe && !isOurs) { index++; continue; }

            try
            {
                NvApi.DeleteProfile(h, hProfile);
                log.Add(hasExe
                    ? $"Удалён профиль «{profileName}» (в нём был {exeName})."
                    : $"Удалён наш старый профиль «{profileName}» (привязок к нему не осталось).");

                // Встроенный (предопределённый) профиль драйвер не удаляет, а сбрасывает в заводские
                // значения — он остаётся на том же индексе. Заменить его нельзя, но привязку exe
                // с него снимет проверка ниже, поэтому прекращаем перечисление.
                IntPtr? next = NvApi.EnumProfileByIndex(h, index);
                if (next is IntPtr hNext &&
                    string.Equals(NvApi.GetProfileName(h, hNext), profileName, StringComparison.OrdinalIgnoreCase))
                {
                    log.Add($"Профиль «{profileName}» встроенный — драйвер сбросил его в значения по умолчанию.");
                    break;
                }
                // индекс не увеличиваем: следующий профиль сдвинулся на место удалённого
            }
            catch (NvApiException ex)
            {
                log.Add($"! Не удалось удалить профиль «{profileName}»: {ex.Message}");
                index++;
            }
        }

        // Остаточная привязка (например, в глобальном профиле — он не перечисляется): снимаем её,
        // не удаляя сам профиль, иначе драйвер не даст привязать exe к новому профилю (-167).
        if (NvApi.FindProfileForExe(h, exeName) is IntPtr hLeft && hLeft != IntPtr.Zero)
        {
            string leftName = NvApi.GetProfileName(h, hLeft);
            try
            {
                NvApi.DeleteApplication(h, hLeft, exeName);
                log.Add($"Снята привязка {exeName} с профиля «{leftName}» (сам профиль оставлен).");
            }
            catch (NvApiException ex)
            {
                log.Add($"! Не удалось снять привязку {exeName} с профиля «{leftName}»: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Гарантирует «чистый лист»: после очистки exe нигде не привязан, поэтому создаём
    /// свежий профиль программы и привязываем exe только к нему.
    /// </summary>
    private static IntPtr CreateCleanProfile(IntPtr h, string exeName, List<string> log)
    {
        WipeExeFromAllProfiles(h, exeName, log);

        IntPtr hProfile = NvApi.CreateProfile(h, OwnProfileName);
        log.Add($"Создан чистый профиль «{OwnProfileName}».");
        NvApi.AddApplication(h, hProfile, exeName, "ROB RUN");
        log.Add($"К профилю привязан {exeName}.");
        return hProfile;
    }

    /// <summary>Строит итоговый список значений для пресета (с учётом своего LOD bias).</summary>
    public static Dictionary<uint, uint> BuildValues(Preset preset, double? customLod)
    {
        var values = new Dictionary<uint, uint>(preset.Settings);
        double? lod = customLod ?? preset.Lod;
        if (lod is double l)
        {
            values[NvSetting.LodBias] = NvSetting.LodToRaw(l);
            values[NvSetting.DriverControlledLod] = 0;
            // при отрицательном LOD («резкость») драйвер не должен его обрезать
            values[NvSetting.NegativeLodBias] = l < 0 ? 0u : 1u;
            // Без «Transparency Supersampling» драйвер часто игнорирует LOD Bias (DX).
            // Без MSAA он ничего не стоит по производительности.
            values[NvSetting.TrSupersampling] = 0x8;
        }
        return values;
    }

    public static List<string> Apply(string exeName, Preset preset, double? customLod)
    {
        var log = new List<string>();
        WithSession(h =>
        {
            log.Add($"Очищаю старые профили с {exeName} (в т.ч. созданные в Панели управления NVIDIA)…");
            IntPtr hProfile = CreateCleanProfile(h, exeName, log);
            var values = BuildValues(preset, customLod);

            // Сначала сбрасываем всё, что мы когда-либо трогаем, чтобы уровни не «накладывались».
            foreach (uint id in NvSetting.All)
                if (!values.ContainsKey(id))
                    NvApi.Restore(h, hProfile, id);

            foreach (var (id, v) in values)
            {
                NvApi.SetDword(h, hProfile, id, v);
                log.Add($"  {NvSetting.Name(id)} = {NvSetting.Describe(id, v)}");
            }

            NvApi.SaveSettings(h);
            log.Add(values.Count == 0
                ? "Все настройки возвращены к стандартным."
                : "Сохранено в драйвер. Перезапустите игру!");
            return 0;
        });
        return log;
    }

    /// <summary>Текущие значения настроек в профиле exe (только те, что заданы).</summary>
    public static Dictionary<uint, uint>? ReadCurrent(string exeName)
    {
        return WithSession<Dictionary<uint, uint>?>(h =>
        {
            IntPtr hProfile = NvApi.FindProfileForExe(h, exeName);
            if (hProfile == IntPtr.Zero) return null;
            LastProfileName = NvApi.GetProfileName(h, hProfile);
            var result = new Dictionary<uint, uint>();
            foreach (uint id in NvSetting.All)
                if (NvApi.GetDword(h, hProfile, id) is uint v)
                    result[id] = v;
            return result;
        });
    }

    /// <summary>
    /// Полная очистка без применения пресета: удаляет все профили, где привязан exe
    /// (в т.ч. созданные в Панели управления NVIDIA), и снимает его привязку с глобального профиля.
    /// </summary>
    public static List<string> Cleanup(string exeName)
    {
        var log = new List<string>();
        WithSession(h =>
        {
            log.Add($"Очистка: ищу все профили с {exeName}…");
            WipeExeFromAllProfiles(h, exeName, log);
            NvApi.SaveSettings(h);
            log.Add("Готово: привязок exe больше нет, пресет не применён.");
            return 0;
        });
        return log;
    }
}
