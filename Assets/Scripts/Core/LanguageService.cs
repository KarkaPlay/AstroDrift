using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// Единственный владелец языка на старте (ТЗ Localization §6.1): читает код языка
/// платформы, маппит на ближайшую доступную локаль Unity и выставляет SelectedLocale
/// один раз за запуск. Строки остаются за Unity Localization (L10n перечитает сам).
///
/// Контракт C1–C3:
///  C1 — AvailableLocales.GetLocale / SelectedLocale трогаем ТОЛЬКО после
///       LocalizationSettings.InitializationOperation (до этого Locales.Count == 0);
///  C2 — Bootstrap.Build стартует по «И» (PlatformBoot.Ready И StartupApplied), иначе
///       UI построится в дефолтной локали и будет видимая вспышка языка;
///  C3 — watchdog 5 с: init не завершился → дефолт + StartupApplied, старт не висит.
///
/// Фолбэк unsupported/tr/zh/pt/пусто → ru — решение продукта D1 (§6.3), осознанное
/// отклонение от поведения удалённого моста/CorrectLang. При добавлении локали
/// (например tr) правится одна строка таблицы Map, не логика.
/// </summary>
public static class LanguageService
{
    private const float WatchdogSeconds = 5f;
    private const string DefaultCode = "ru"; // D1: первичная аудитория

    /// <summary>Код языка платформы → доступная локаль проекта. Одна строка на язык.</summary>
    private static readonly (string code, string locale)[] Map =
    {
        ("ru", "ru"),
        ("en", "en"),
        ("tr", "ru"), // D1: локали tr нет — фолбэк, триггер пересмотра — добавление tr
        ("zh", "ru"), // D1: zh-Hans/zh-Hant → база zh → нет локали → ru
        ("pt", "ru"), // D1: pt-BR → база pt → нет локали → ru
    };

    private static bool _started;
    private static WatchdogRunner _watchdog;

    /// <summary>Локаль применена либо выставился дефолт по watchdog'у — контракт для Bootstrap (C2).</summary>
    public static bool StartupApplied { get; private set; }

    /// <summary>StartupApplied: false → true, один раз за запуск.</summary>
    public static event Action StartupAppliedChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        if (_started) return;
        _started = true;

        // Единственная точка реакций на язык (§3.5): заголовки типографики отдельной
        // цепочки не имеют. Подписка одна за сессию (_started идемпотентен).
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;

        // C1: IsDone → применяем сразу; иначе ждём Completed (на baseline Locales.Count == 0).
        if (LocalizationSettings.InitializationOperation.IsDone)
        {
            OnLocalesReady();
            return;
        }

        ArmWatchdog(); // C3
        LocalizationSettings.InitializationOperation.Completed += _ => OnLocalesReady();
    }

    /// <summary>
    /// Смена локали (§3.5): свип тегов ролей + сброс гардов горячего пути (§3.4).
    /// Свип opt-in — нетегированные ноды (HUD и др.) не трогаются.
    /// </summary>
    private static void OnSelectedLocaleChanged(Locale _)
    {
        TypeRoleApplier.ApplyAll();

        // §3.4: сброс гарда горячего пути (best_value) — поля живут у владельца
        // (GameUI), а точка подписки остаётся ровно одна: здесь (§3.5).
        GameUI.ResetLanguageGuards();
    }

    /// <summary>
    /// Хук под будущий явный выбор языка игроком (экран Настроек, DoNotChangeLanguageStartup).
    /// Реализация вне милстоуна — тело пустое по ТЗ §6.1.
    /// </summary>
    public static void TryApplyPlayerOverride()
    {
    }

    private static void OnLocalesReady()
    {
        DisarmWatchdog(); // C3: init успел — таймер снимаем

        string raw = ReadPlatformCode();
        string code = MapToLocaleCode(raw);
        Apply(code);
    }

#if UNITY_WEBGL && STORE_YANDEX
    /// <summary>ЯИ: язык аккаунта (гейт G1). Пустая строка — источника нет (таблица §6.1: пусто → ru).</summary>
    private static string ReadPlatformCode() => YandexLanguageSource.GetAccountLanguage();
#else
    /// <summary>RuStore / itch / Editor: systemLanguage — основной путь (§5.2), не фолбэк.</summary>
    private static string ReadPlatformCode()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Russian: return "ru";
            case SystemLanguage.English: return "en";
            case SystemLanguage.Turkish: return "tr";
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional:
            case SystemLanguage.Chinese: return "zh";
            case SystemLanguage.Portuguese: return "pt";
            default: return string.Empty; // unsupported → ru (D1)
        }
    }
#endif

    /// <summary>Код платформы → ближайшая локаль проекта. Региональные коды — через базу (pt-BR → pt).</summary>
    private static string MapToLocaleCode(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return DefaultCode;

        string code = raw.Trim().ToLowerInvariant();
        int sep = code.IndexOfAny(new[] { '-', '_' });
        if (sep > 0) code = code.Substring(0, sep);

        foreach (var m in Map)
            if (m.code == code) return m.locale;

        return DefaultCode; // unsupported → ru (D1)
    }

    private static void Apply(string code)
    {
        var locales = LocalizationSettings.AvailableLocales;
        Locale locale = locales != null ? locales.GetLocale(code) : null;

        // Локали нет — остаёмся на текущей, без исключений (контракт удалённого моста).
        if (locale != null && LocalizationSettings.SelectedLocale != locale)
            LocalizationSettings.SelectedLocale = locale; // L10n перечитает строки сам

        MarkApplied();
    }

    private static void MarkApplied()
    {
        if (StartupApplied) return;
        StartupApplied = true;
        StartupAppliedChanged?.Invoke();
        StartupAppliedChanged = null;
    }

    // --- C3: watchdog -------------------------------------------------------

    private static void ArmWatchdog()
    {
        var go = new GameObject("[LanguageService.Watchdog]");
        go.hideFlags = HideFlags.HideAndDontSave;
        UnityEngine.Object.DontDestroyOnLoad(go);
        _watchdog = go.AddComponent<WatchdogRunner>();
    }

    private static void DisarmWatchdog()
    {
        if (_watchdog == null) return;
        UnityEngine.Object.Destroy(_watchdog.gameObject);
        _watchdog = null;
    }

    /// <summary>C3: init не завершился за WatchdogSeconds — не держим старт игры.</summary>
    internal static void OnWatchdogTimeout()
    {
        if (StartupApplied || _watchdog == null) return;

        DisarmWatchdog(); // init не завершился за WatchdogSeconds — применяем дефолт и продолжаем старт

        if (LocalizationSettings.InitializationOperation.IsDone) Apply(DefaultCode); // C1 соблюдён: init завершился
        else MarkApplied();                                                          // C1: локаль не трогаем, AvailableLocales пуст
    }

    private class WatchdogRunner : MonoBehaviour
    {
        private float _deadline;
        private bool _fired;

        private void Awake() => _deadline = Time.realtimeSinceStartup + WatchdogSeconds;

        private void Update()
        {
            if (_fired || Time.realtimeSinceStartup < _deadline) return;
            _fired = true;
            OnWatchdogTimeout();
        }
    }
}
