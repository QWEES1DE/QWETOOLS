using System.Runtime.InteropServices;
using System.Text;

namespace RobRunPotato;

/// <summary>
/// Минимальная обёртка над NVAPI (nvapi64.dll ставится вместе с драйвером NVIDIA).
/// Используется только DRS (Driver Settings): та же база профилей, которую
/// редактируют NVIDIA Profile Inspector и Панель управления NVIDIA.
/// В процесс игры ничего не внедряется.
/// </summary>
internal static class NvApi
{
    // ---- Коды ошибок (nvapi_lite_common.h) ----
    public const int NVAPI_OK = 0;
    public const int NVAPI_LIBRARY_NOT_FOUND = -2;
    public const int NVAPI_NVIDIA_DEVICE_NOT_FOUND = -6;
    public const int NVAPI_INVALID_USER_PRIVILEGE = -137;
    public const int NVAPI_SETTING_NOT_FOUND = -160;
    public const int NVAPI_PROFILE_NOT_FOUND = -163;
    public const int NVAPI_PROFILE_NAME_IN_USE = -164;
    public const int NVAPI_EXECUTABLE_NOT_FOUND = -166;
    public const int NVAPI_EXECUTABLE_ALREADY_IN_USE = -167;
    public const int NVAPI_END_ENUMERATION = -7;

    // ---- ID функций для nvapi_QueryInterface (nvapi_interface.h) ----
    private const uint ID_Initialize = 0x0150E828;
    private const uint ID_GetErrorMessage = 0x6C2D048C;
    private const uint ID_DRS_CreateSession = 0x0694D52E;
    private const uint ID_DRS_DestroySession = 0xDAD9CFF8;
    private const uint ID_DRS_LoadSettings = 0x375DBD6B;
    private const uint ID_DRS_SaveSettings = 0xFCBC7E14;
    private const uint ID_DRS_FindApplicationByName = 0xEEE566B2;
    private const uint ID_DRS_FindProfileByName = 0x7E4A9A0B;
    private const uint ID_DRS_CreateProfile = 0xCC176068;
    private const uint ID_DRS_CreateApplication = 0x4347A9DE;
    private const uint ID_DRS_SetSetting = 0x577DD202;
    private const uint ID_DRS_GetSetting = 0x73BF8338;
    private const uint ID_DRS_RestoreProfileDefaultSetting = 0x53F0381E;
    private const uint ID_DRS_GetProfileInfo = 0x61CD6FD6;
    private const uint ID_DRS_EnumProfiles = 0xBC371EE0;
    private const uint ID_DRS_GetNumProfiles = 0x1DAE4FBC;
    private const uint ID_DRS_DeleteProfile = 0x17093206;
    private const uint ID_DRS_EnumApplications = 0x7FA2173A;
    private const uint ID_DRS_DeleteApplication = 0x2C694BC6;

    // ---- Размеры структур (nvapi.h, #pragma pack(4)) ----
    private const int UNICODE_STRING_BYTES = 2048 * 2;            // NvAPI_UnicodeString = NvU16[2048]
    private const int BINARY_SETTING_BYTES = 4 + 4096;            // NVDRS_BINARY_SETTING

    // NVDRS_SETTING_V1
    public const int SETTING_SIZE = 4 + UNICODE_STRING_BYTES + 4 * 5 + BINARY_SETTING_BYTES * 2; // 12320
    public const uint SETTING_VER = (uint)SETTING_SIZE | (1u << 16);
    public const int SETTING_OFF_ID = 4 + UNICODE_STRING_BYTES;               // 4100
    public const int SETTING_OFF_TYPE = SETTING_OFF_ID + 4;                   // 4104
    public const int SETTING_OFF_LOCATION = SETTING_OFF_TYPE + 4;             // 4108
    public const int SETTING_OFF_PREDEF_VALUE = SETTING_OFF_ID + 4 * 5;       // 4120
    public const int SETTING_OFF_CURRENT_VALUE = SETTING_OFF_PREDEF_VALUE + BINARY_SETTING_BYTES; // 8220

    // NVDRS_PROFILE_V1
    public const int PROFILE_SIZE = 4 + UNICODE_STRING_BYTES + 4 * 4;        // 4116
    public const uint PROFILE_VER = (uint)PROFILE_SIZE | (1u << 16);
    private const int PROFILE_OFF_NAME = 4;
    private const int PROFILE_OFF_GPU_SUPPORT = PROFILE_OFF_NAME + UNICODE_STRING_BYTES; // 4100
    private const int PROFILE_OFF_IS_PREDEFINED = PROFILE_OFF_GPU_SUPPORT + 4;           // 4104
    private const int PROFILE_OFF_NUM_APPS = PROFILE_OFF_IS_PREDEFINED + 4;              // 4108
    private const int PROFILE_OFF_NUM_SETTINGS = PROFILE_OFF_NUM_APPS + 4;               // 4112

    // NVDRS_APPLICATION_V3
    public const int APP_SIZE = 4 + 4 + UNICODE_STRING_BYTES * 4 + 4;        // 16396
    public const uint APP_VER = (uint)APP_SIZE | (3u << 16);
    public const int APP_OFF_NAME = 8;
    public const int APP_OFF_FRIENDLY = APP_OFF_NAME + UNICODE_STRING_BYTES;

    // ---- Делегаты ----
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int D_Void();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int D_GetErrorMessage(int status, [Out] byte[] desc);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int D_CreateSession(out IntPtr hSession);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int D_Session(IntPtr hSession);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int D_FindApplicationByName(IntPtr hSession, IntPtr appName, out IntPtr hProfile, IntPtr pApplication);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int D_FindProfileByName(IntPtr hSession, IntPtr profileName, out IntPtr hProfile);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int D_CreateProfile(IntPtr hSession, IntPtr pProfile, out IntPtr hProfile);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int D_CreateApplication(IntPtr hSession, IntPtr hProfile, IntPtr pApplication);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int D_SetSetting(IntPtr hSession, IntPtr hProfile, IntPtr pSetting);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int D_GetSetting(IntPtr hSession, IntPtr hProfile, uint settingId, IntPtr pSetting);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int D_ProfileSetting(IntPtr hSession, IntPtr hProfile, uint settingId);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int D_EnumProfiles(IntPtr hSession, uint index, out IntPtr hProfile);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int D_GetNumProfiles(IntPtr hSession, out uint numProfiles);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int D_TwoHandles(IntPtr hSession, IntPtr hProfile);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int D_EnumApplications(IntPtr hSession, IntPtr hProfile, uint startIndex, ref uint appCount, IntPtr pApplication);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int D_DeleteApplication(IntPtr hSession, IntPtr hProfile, IntPtr appName);

    [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr QueryInterface(uint id);

    private static D_GetErrorMessage? _getErrorMessage;
    private static D_CreateSession? _createSession;
    private static D_Session? _destroySession, _loadSettings, _saveSettings;
    private static D_FindApplicationByName? _findApp;
    private static D_FindProfileByName? _findProfile;
    private static D_CreateProfile? _createProfile;
    private static D_CreateApplication? _createApp;
    private static D_SetSetting? _setSetting;
    private static D_GetSetting? _getSetting;
    private static D_ProfileSetting? _restoreSetting;
    private static D_CreateApplication? _getProfileInfo; // та же сигнатура: (hSession, hProfile, NVDRS_PROFILE*)
    private static D_EnumProfiles? _enumProfiles;
    private static D_GetNumProfiles? _getNumProfiles;
    private static D_TwoHandles? _deleteProfile;
    private static D_EnumApplications? _enumApplications;
    private static D_DeleteApplication? _deleteApplication;

    public static bool IsInitialized { get; private set; }

    private static T Get<T>(uint id) where T : Delegate
    {
        IntPtr p = QueryInterface(id);
        if (p == IntPtr.Zero)
            throw new NvApiException($"Функция NVAPI 0x{id:X8} недоступна в этом драйвере.", NVAPI_LIBRARY_NOT_FOUND);
        return Marshal.GetDelegateForFunctionPointer<T>(p);
    }

    /// <summary>Загружает nvapi64.dll и инициализирует NVAPI.</summary>
    public static void Initialize()
    {
        if (IsInitialized) return;
        try
        {
            var init = Get<D_Void>(ID_Initialize);
            _getErrorMessage = Get<D_GetErrorMessage>(ID_GetErrorMessage);
            Check(init(), "NvAPI_Initialize");
        }
        catch (DllNotFoundException)
        {
            throw new NvApiException("Не найден nvapi64.dll. Установлен ли драйвер NVIDIA и запущена ли 64-битная версия программы?", NVAPI_LIBRARY_NOT_FOUND);
        }
        catch (EntryPointNotFoundException)
        {
            throw new NvApiException("nvapi64.dll найден, но не содержит nvapi_QueryInterface.", NVAPI_LIBRARY_NOT_FOUND);
        }

        _createSession = Get<D_CreateSession>(ID_DRS_CreateSession);
        _destroySession = Get<D_Session>(ID_DRS_DestroySession);
        _loadSettings = Get<D_Session>(ID_DRS_LoadSettings);
        _saveSettings = Get<D_Session>(ID_DRS_SaveSettings);
        _findApp = Get<D_FindApplicationByName>(ID_DRS_FindApplicationByName);
        _findProfile = Get<D_FindProfileByName>(ID_DRS_FindProfileByName);
        _createProfile = Get<D_CreateProfile>(ID_DRS_CreateProfile);
        _createApp = Get<D_CreateApplication>(ID_DRS_CreateApplication);
        _setSetting = Get<D_SetSetting>(ID_DRS_SetSetting);
        _getSetting = Get<D_GetSetting>(ID_DRS_GetSetting);
        _restoreSetting = Get<D_ProfileSetting>(ID_DRS_RestoreProfileDefaultSetting);
        _getProfileInfo = Get<D_CreateApplication>(ID_DRS_GetProfileInfo);
        _enumProfiles = Get<D_EnumProfiles>(ID_DRS_EnumProfiles);
        _getNumProfiles = Get<D_GetNumProfiles>(ID_DRS_GetNumProfiles);
        _deleteProfile = Get<D_TwoHandles>(ID_DRS_DeleteProfile);
        _enumApplications = Get<D_EnumApplications>(ID_DRS_EnumApplications);
        _deleteApplication = Get<D_DeleteApplication>(ID_DRS_DeleteApplication);
        IsInitialized = true;
    }

    public static string ErrorText(int status)
    {
        if (_getErrorMessage == null) return $"код {status}";
        var buf = new byte[64];
        _getErrorMessage(status, buf);
        int len = Array.IndexOf(buf, (byte)0);
        if (len < 0) len = buf.Length;
        return $"{Encoding.ASCII.GetString(buf, 0, len)} ({status})";
    }

    public static void Check(int status, string what)
    {
        if (status != NVAPI_OK)
            throw new NvApiException($"{what}: {ErrorText(status)}", status);
    }

    // ---- Помощники для неуправляемой памяти ----
    private static IntPtr AllocZeroed(int size)
    {
        IntPtr p = Marshal.AllocHGlobal(size);
        Marshal.Copy(new byte[size], 0, p, size);
        return p;
    }

    private static void WriteUnicode(IntPtr basePtr, int offset, string s)
    {
        if (s.Length > 2047) s = s[..2047];
        var bytes = Encoding.Unicode.GetBytes(s + "\0");
        Marshal.Copy(bytes, 0, basePtr + offset, bytes.Length);
    }

    private static IntPtr AllocUnicodeString(string s)
    {
        IntPtr p = AllocZeroed(UNICODE_STRING_BYTES);
        WriteUnicode(p, 0, s);
        return p;
    }

    // ---- DRS API ----
    public static IntPtr CreateSession()
    {
        Check(_createSession!(out IntPtr h), "NvAPI_DRS_CreateSession");
        return h;
    }

    public static void DestroySession(IntPtr h) => _destroySession!(h);
    public static void LoadSettings(IntPtr h) => Check(_loadSettings!(h), "NvAPI_DRS_LoadSettings");

    public static void SaveSettings(IntPtr h)
    {
        int st = _saveSettings!(h);
        if (st == NVAPI_INVALID_USER_PRIVILEGE)
            throw new NvApiException("Нужны права администратора. Запустите программу от имени администратора.", st);
        Check(st, "NvAPI_DRS_SaveSettings");
    }

    /// <summary>Ищет профиль, к которому привязан exe. Возвращает IntPtr.Zero, если exe нигде не привязан.</summary>
    public static IntPtr FindProfileForExe(IntPtr hSession, string exeName)
    {
        IntPtr name = AllocUnicodeString(exeName);
        IntPtr app = AllocZeroed(APP_SIZE);
        try
        {
            Marshal.WriteInt32(app, 0, unchecked((int)APP_VER));
            int st = _findApp!(hSession, name, out IntPtr hProfile, app);
            if (st == NVAPI_EXECUTABLE_NOT_FOUND) return IntPtr.Zero;
            Check(st, "NvAPI_DRS_FindApplicationByName");
            return hProfile;
        }
        finally
        {
            Marshal.FreeHGlobal(name);
            Marshal.FreeHGlobal(app);
        }
    }

    public static IntPtr FindProfileByName(IntPtr hSession, string profileName)
    {
        IntPtr name = AllocUnicodeString(profileName);
        try
        {
            int st = _findProfile!(hSession, name, out IntPtr hProfile);
            if (st == NVAPI_PROFILE_NOT_FOUND) return IntPtr.Zero;
            Check(st, "NvAPI_DRS_FindProfileByName");
            return hProfile;
        }
        finally { Marshal.FreeHGlobal(name); }
    }

    public static IntPtr CreateProfile(IntPtr hSession, string profileName)
    {
        IntPtr prof = AllocZeroed(PROFILE_SIZE);
        try
        {
            Marshal.WriteInt32(prof, 0, unchecked((int)PROFILE_VER));
            WriteUnicode(prof, 4, profileName);
            Check(_createProfile!(hSession, prof, out IntPtr hProfile), "NvAPI_DRS_CreateProfile");
            return hProfile;
        }
        finally { Marshal.FreeHGlobal(prof); }
    }

    public static void AddApplication(IntPtr hSession, IntPtr hProfile, string exeName, string friendlyName)
    {
        IntPtr app = AllocZeroed(APP_SIZE);
        try
        {
            Marshal.WriteInt32(app, 0, unchecked((int)APP_VER));
            WriteUnicode(app, APP_OFF_NAME, exeName.ToLowerInvariant());
            WriteUnicode(app, APP_OFF_FRIENDLY, friendlyName);
            Check(_createApp!(hSession, hProfile, app), "NvAPI_DRS_CreateApplication");
        }
        finally { Marshal.FreeHGlobal(app); }
    }

    /// <summary>Имя профиля (то, что видно в Profile Inspector).</summary>
    public static string GetProfileName(IntPtr hSession, IntPtr hProfile)
    {
        IntPtr prof = AllocZeroed(PROFILE_SIZE);
        try
        {
            Marshal.WriteInt32(prof, 0, unchecked((int)PROFILE_VER));
            int st = _getProfileInfo!(hSession, hProfile, prof);
            if (st != NVAPI_OK) return "?";
            return Marshal.PtrToStringUni(prof + 4) ?? "?";
        }
        finally { Marshal.FreeHGlobal(prof); }
    }

    public static void SetDword(IntPtr hSession, IntPtr hProfile, uint settingId, uint value)
    {
        IntPtr s = AllocZeroed(SETTING_SIZE);
        try
        {
            Marshal.WriteInt32(s, 0, unchecked((int)SETTING_VER));
            Marshal.WriteInt32(s, SETTING_OFF_ID, unchecked((int)settingId));
            Marshal.WriteInt32(s, SETTING_OFF_TYPE, 0); // NVDRS_DWORD_TYPE
            Marshal.WriteInt32(s, SETTING_OFF_CURRENT_VALUE, unchecked((int)value));
            Check(_setSetting!(hSession, hProfile, s), $"NvAPI_DRS_SetSetting 0x{settingId:X8}");
        }
        finally { Marshal.FreeHGlobal(s); }
    }

    /// <summary>Читает DWORD-настройку. null — если она не задана (или унаследована, при onlyThisProfile).</summary>
    public static uint? GetDword(IntPtr hSession, IntPtr hProfile, uint settingId, bool onlyThisProfile = true)
    {
        IntPtr s = AllocZeroed(SETTING_SIZE);
        try
        {
            Marshal.WriteInt32(s, 0, unchecked((int)SETTING_VER));
            int st = _getSetting!(hSession, hProfile, settingId, s);
            if (st == NVAPI_SETTING_NOT_FOUND) return null;
            Check(st, $"NvAPI_DRS_GetSetting 0x{settingId:X8}");
            // NVDRS_SETTING_LOCATION: 0 = текущий профиль, 1 = глобальный, 2 = базовый, 3 = по умолчанию
            if (onlyThisProfile && Marshal.ReadInt32(s, SETTING_OFF_LOCATION) != 0) return null;
            return unchecked((uint)Marshal.ReadInt32(s, SETTING_OFF_CURRENT_VALUE));
        }
        finally { Marshal.FreeHGlobal(s); }
    }

    /// <summary>Возвращает настройку к значению по умолчанию (как «Восстановить» в Profile Inspector).</summary>
    public static void Restore(IntPtr hSession, IntPtr hProfile, uint settingId)
    {
        int st = _restoreSetting!(hSession, hProfile, settingId);
        if (st == NVAPI_SETTING_NOT_FOUND) return; // и так не задана
        Check(st, $"NvAPI_DRS_RestoreProfileDefaultSetting 0x{settingId:X8}");
    }

    /// <summary>Профиль по порядковому индексу (индексы плотные от 0), либо null, если профили
    /// закончились (NVAPI_END_ENUMERATION). Хэндл действителен до DeleteProfile / LoadSettings.</summary>
    public static IntPtr? EnumProfileByIndex(IntPtr hSession, uint index)
    {
        int st = _enumProfiles!(hSession, index, out IntPtr hProfile);
        if (st == NVAPI_END_ENUMERATION) return null;
        Check(st, "NvAPI_DRS_EnumProfiles");
        return hProfile;
    }

    public static uint GetNumProfiles(IntPtr hSession)
    {
        Check(_getNumProfiles!(hSession, out uint n), "NvAPI_DRS_GetNumProfiles");
        return n;
    }

    /// <summary>Число приложений в профиле (поле numOfApps из NVDRS_PROFILE).</summary>
    public static uint GetProfileAppCount(IntPtr hSession, IntPtr hProfile)
    {
        IntPtr prof = AllocZeroed(PROFILE_SIZE);
        try
        {
            Marshal.WriteInt32(prof, 0, unchecked((int)PROFILE_VER));
            int st = _getProfileInfo!(hSession, hProfile, prof);
            if (st != NVAPI_OK) return 0;
            return unchecked((uint)Marshal.ReadInt32(prof, PROFILE_OFF_NUM_APPS));
        }
        finally { Marshal.FreeHGlobal(prof); }
    }

    /// <summary>
    /// Имя exe-файла приложения с порядковым индексом startIndex, либо null (нет такого приложения).
    /// Приложение запрашивается по одному (appCount = 1), версия структуры — V3, как и везде.
    /// </summary>
    public static string? EnumApplicationName(IntPtr hSession, IntPtr hProfile, uint startIndex)
    {
        uint one = 1;
        IntPtr app = AllocZeroed(APP_SIZE);
        try
        {
            Marshal.WriteInt32(app, 0, unchecked((int)APP_VER));
            int st = _enumApplications!(hSession, hProfile, startIndex, ref one, app);
            if (st != NVAPI_OK) return null;
            return Marshal.PtrToStringUni(app + APP_OFF_NAME) ?? null;
        }
        finally { Marshal.FreeHGlobal(app); }
    }

    /// <summary>Снимает привязку exe к профилю (удаляет приложение из профиля).</summary>
    public static void DeleteApplication(IntPtr hSession, IntPtr hProfile, string exeName)
    {
        IntPtr name = AllocUnicodeString(exeName);
        try { Check(_deleteApplication!(hSession, hProfile, name), "NvAPI_DRS_DeleteApplication"); }
        finally { Marshal.FreeHGlobal(name); }
    }

    /// <summary>Удаляет профиль целиком. Предупреждение NVIDIA: для предопределённых профилей
    /// драйвер не удаляет их, а сбрасывает в заводские значения. Хэндл после этого невалиден.</summary>
    public static void DeleteProfile(IntPtr hSession, IntPtr hProfile) =>
        Check(_deleteProfile!(hSession, hProfile), "NvAPI_DRS_DeleteProfile");
}

internal sealed class NvApiException : Exception
{
    public int Status { get; }
    public NvApiException(string message, int status) : base(message) => Status = status;
}
