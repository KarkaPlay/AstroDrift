using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// Мост «язык PluginYG2 → Unity Locale». Источник истины языка — YG2 (модуль
/// Localization), Unity Localization остаётся источником строк.
/// Подписка на YG2.onSwitchLang делается ПОСЛЕ инициализации локалей
/// (InitializationOperation.Completed) и не раньше: YG2.Initialize() в редакторе
/// обнуляет статическое поле onSwitchLang («Reset static for ESC»), поэтому
/// подписка из BeforeSceneLoad могла быть стёрта — тогда смена языка не доходила.
/// Плюс первичная синхронизация текущего языка после инициализации.
/// Локали для языка нет — остаёмся на текущей локали (исключений не бросаем).
/// </summary>
public static class AstroDriftLanguageBridge
{
    private static bool _installed;
    private static string _lastCode;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (_installed) return;
        _installed = true;

        if (LocalizationSettings.InitializationOperation.IsDone) OnLocalesReady();
        else LocalizationSettings.InitializationOperation.Completed += _ => OnLocalesReady();
    }

    private static void OnLocalesReady()
    {
#if Localization_yg
        YG.YG2.onSwitchLang += Apply; // YG2 зовёт и при старте (LangStart), и при смене языка
        Apply(YG.YG2.lang);
#else
        Apply(null);
#endif
    }

    /// <summary>Код языка YG2 → локаль Unity. Нет такой локали — текущая локаль не меняется.</summary>
    private static void Apply(string code)
    {
        if (string.IsNullOrEmpty(code)) return; // пустой код — не трогаем локаль
        if (code == _lastCode) return;          // YG2 дублирует вызов на старте — не перечитываем зря
        _lastCode = code;

        Typography.NotifyLanguageChanged(); // UI переприменяет шрифты нового языка
        var locales = LocalizationSettings.AvailableLocales;
        if (locales == null) return;
        Locale locale = locales.GetLocale(code);
        if (locale == null)
        {
            Debug.Log($"[Lang] Нет локали для языка '{code}' — остаёмся на текущей локали.");
            return;
        }
        if (LocalizationSettings.SelectedLocale != locale)
            LocalizationSettings.SelectedLocale = locale; // L10n перечитает строки сам
    }
}
