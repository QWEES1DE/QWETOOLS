namespace RobRunPotato;

/// <summary>ID настроек драйвера (NvApiDriverSettings.h — те же, что видны в Profile Inspector).</summary>
internal static class NvSetting
{
    public const uint LodBias            = 0x00738E8F; // Texture filtering - LOD Bias (DX). Единица = 1/8, диапазон -128..+128
    public const uint DriverControlledLod= 0x00638E8F; // Texture filtering - Driver Controlled LOD Bias (должен быть OFF)
    public const uint NegativeLodBias    = 0x0019BB68; // Texture filtering - Negative LOD bias (0 = Allow, 1 = Clamp)
    public const uint FilterQuality      = 0x00CE2691; // Texture filtering - Quality
    public const uint TrilinearOpt       = 0x002ECAF2; // Texture filtering - Trilinear optimization
    public const uint AnisoSampleOpt     = 0x00E73211; // Texture filtering - Anisotropic sample optimization
    public const uint BilinearInAniso    = 0x0084CD70; // Texture filtering - Anisotropic filter optimization
    public const uint AnisoMode          = 0x10D2BB16; // Anisotropic filtering mode (0 = приложение, 1 = пользователь)
    public const uint AnisoLevel         = 0x101E61A9; // Anisotropic filtering setting (0 = Off[Point], 1 = Off[Linear], 2..16 = Nx)
    public const uint Fxaa               = 0x1074C972; // FXAA
    public const uint Mfaa               = 0x0098C1AC; // MFAA
    public const uint MaxPreRendered     = 0x007BA09E; // Maximum pre-rendered frames
    public const uint VSync              = 0x00A879CF; // Vertical Sync
    public const uint PowerMode          = 0x1057EB71; // Power management mode
    public const uint TrSupersampling    = 0x10D48A85; // Antialiasing - Transparency Supersampling.
                                                       // Известная особенность драйвера: без него LOD Bias (DX) часто игнорируется

    public static readonly uint[] All =
    {
        LodBias, DriverControlledLod, NegativeLodBias, FilterQuality, TrilinearOpt, AnisoSampleOpt,
        BilinearInAniso, AnisoMode, AnisoLevel, Fxaa, Mfaa, MaxPreRendered, VSync, PowerMode, TrSupersampling
    };

    public static string Name(uint id) => id switch
    {
        LodBias => "LOD Bias текстур (DX)",
        DriverControlledLod => "LOD Bias под контролем драйвера",
        NegativeLodBias => "Отрицательный LOD Bias",
        FilterQuality => "Фильтрация текстур: качество",
        TrilinearOpt => "Трилинейная оптимизация",
        AnisoSampleOpt => "Оптимизация анизотропной выборки",
        BilinearInAniso => "Оптимизация анизотропного фильтра",
        AnisoMode => "Режим анизотропной фильтрации",
        AnisoLevel => "Анизотропная фильтрация",
        Fxaa => "FXAA",
        Mfaa => "MFAA",
        MaxPreRendered => "Макс. кол-во заранее подготовленных кадров",
        VSync => "Вертикальная синхронизация",
        PowerMode => "Режим управления питанием",
        TrSupersampling => "Сглаживание прозрачности (нужно для LOD)",
        _ => $"0x{id:X8}"
    };

    public static string Describe(uint id, uint v) => id switch
    {
        LodBias => FormatLod(unchecked((int)v)),
        DriverControlledLod or TrilinearOpt or AnisoSampleOpt or BilinearInAniso or Fxaa or Mfaa
            => v == 0 ? "Выкл" : "Вкл",
        NegativeLodBias => v == 0 ? "Разрешить" : "Привязка (Clamp)",
        FilterQuality => v switch
        {
            0xFFFFFFF6 => "Высокое качество",
            0x00000000 => "Качество",
            0x0000000A => "Производительность",
            0x00000014 => "Высокая производительность",
            _ => $"0x{v:X}"
        },
        AnisoMode => v == 0 ? "Управление из приложения" : "Пользовательский",
        AnisoLevel => v switch { 0 => "Выкл [Point] (пиксели)", 1 => "Выкл [Linear]", _ => $"{v}x" },
        MaxPreRendered => v == 0 ? "Приложение" : v.ToString(),
        VSync => v switch
        {
            0x08416747 => "Принудительно выкл",
            0x47814940 => "Принудительно вкл",
            0x60925292 => "Приложение",
            _ => $"0x{v:X}"
        },
        TrSupersampling => v switch { 0 => "Выкл", 0x8 => "AA_MODE_REPLAY_MODE_ALL", _ => $"0x{v:X}" },
        PowerMode => v switch
        {
            0 => "Адаптивный",
            1 => "Максимальная производительность",
            5 => "Оптимальное энергопотребление",
            _ => v.ToString()
        },
        _ => $"0x{v:X8}"
    };

    /// <summary>LOD bias хранится в 1/8 долях: +8 = +1.000, -8 = -1.000.</summary>
    public static uint LodToRaw(double lod) => unchecked((uint)(int)Math.Round(lod * 8));
    public static string FormatLod(int raw) => (raw >= 0 ? "+" : "") + (raw / 8.0).ToString("0.000");
}

internal sealed class Preset
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    /// <summary>LOD bias в «целых» единицах (0, +0.5, +3 ...). null = не трогать.</summary>
    public double? Lod { get; init; }
    /// <summary>Остальные настройки. Пустой словарь и Lod == null = «сбросить всё».</summary>
    public Dictionary<uint, uint> Settings { get; init; } = new();
    public bool IsReset => Lod == null && Settings.Count == 0;
}

internal static class Presets
{
    private const uint Q_PERF = 0x0000000A, Q_HIGHPERF = 0x00000014;
    private const uint VSYNC_OFF = 0x08416747;

    // Общие «бесплатные» оптимизации для всех уровней кроме стандартного
    private static Dictionary<uint, uint> Base(uint quality) => new()
    {
        [NvSetting.DriverControlledLod] = 0,   // иначе ручной LOD bias игнорируется
        [NvSetting.FilterQuality] = quality,
        [NvSetting.TrilinearOpt] = 1,
        [NvSetting.AnisoSampleOpt] = 1,
        [NvSetting.Fxaa] = 0,
        [NvSetting.Mfaa] = 0,
        [NvSetting.VSync] = VSYNC_OFF,
        [NvSetting.MaxPreRendered] = 1,
        [NvSetting.PowerMode] = 1,
    };

    private static Dictionary<uint, uint> With(this Dictionary<uint, uint> d, uint id, uint v) { d[id] = v; return d; }

    public static readonly Preset[] All =
    {
        new()
        {
            Name = "Стандарт",
            Description = "Убирает все изменения: драйвер работает как обычно, а графикой управляет сама игра."
        },
        new()
        {
            Name = "Оптимально",
            Description = "Картинка почти не меняется. Фильтрация в режиме «Производительность», V-Sync выключен, " +
                          "видеокарта всегда на максимальных частотах.",
            Lod = 0,
            Settings = Base(Q_PERF)
        },
        new()
        {
            Name = "Низко",
            Description = "Фильтрация в режиме «Высокая производительность», анизотропия 2x, " +
                          "текстуры чуть мягче (LOD +0.5).",
            Lod = 0.5,
            Settings = Base(Q_HIGHPERF)
                .With(NvSetting.BilinearInAniso, 1)
                .With(NvSetting.AnisoMode, 1).With(NvSetting.AnisoLevel, 2)
        },
        new()
        {
            Name = "Очень низко",
            Description = "Анизотропия выключена, текстуры заметно размыты (LOD +1.5).",
            Lod = 1.5,
            Settings = Base(Q_HIGHPERF)
                .With(NvSetting.BilinearInAniso, 1)
                .With(NvSetting.AnisoMode, 1).With(NvSetting.AnisoLevel, 1)
        },
        new()
        {
            Name = "Potato 🥔",
            Description = "Текстуры превращаются в «мыло» (LOD +3). Меньше нагрузка на видеопамять и шину, " +
                          "заметно легче на слабых видеокартах.",
            Lod = 3,
            Settings = Base(Q_HIGHPERF)
                .With(NvSetting.BilinearInAniso, 1)
                .With(NvSetting.AnisoMode, 1).With(NvSetting.AnisoLevel, 1)
        },
        new()
        {
            Name = "Ультра-Potato 🥔🥔",
            Description = "Предел: LOD +6 и точечная фильтрация (Point). Текстуры почти однотонные и пиксельные. " +
                          "Для совсем слабых ПК или ради смеха.",
            Lod = 6,
            Settings = Base(Q_HIGHPERF)
                .With(NvSetting.BilinearInAniso, 1)
                .With(NvSetting.AnisoMode, 1).With(NvSetting.AnisoLevel, 0)
        },
    };
}
