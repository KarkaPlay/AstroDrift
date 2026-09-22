using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;

/// <summary>
/// Единая точка доступа к типографике. Шрифт берётся из NamedStyle
/// (Assets/Resources/TypographyConfig.asset → Styles). Размеры и трекинг — авторские,
/// из префабов/сцены: Typography их не трогает.
/// Шрифт выбирается по текущей локали Unity (localeFonts стиля); пустой шрифт —
/// fallback на TMP Settings default (LiberationSans SDF): ни Missing, ни Null.
///
/// fontStyle применяется ТОЛЬКО если у стиля включён Apply Font Style; иначе вес
/// символов ноды не трогается (авторский из префаба сохраняется).
///
/// Реакции на смену локали у Typography НЕТ (§3.5): единственная точка подписки на
/// SelectedLocaleChanged — LanguageService, он же вызывает TypeRoleApplier.ApplyAll()
/// и сброс гардов §3.4. Носитель стиля — TypeRoleTag (self-apply в OnEnable).
/// </summary>
public static class Typography
{
    private static TypographyConfig _cfg;
    private static bool _loaded;

    public static TypographyConfig Config
    {
        get
        {
            if (!_loaded)
            {
                _cfg = Resources.Load<TypographyConfig>("TypographyConfig");
                _loaded = true;
            }
            return _cfg;
        }
    }

    /// <summary>Код текущей локали. Источник истины — Unity Localization (владелец — LanguageService).</summary>
    private static string CurrentLang
    {
        get
        {
            var locale = LocalizationSettings.SelectedLocale;
            return locale != null ? locale.Identifier.Code : null;
        }
    }

    // Предупреждаем про каждый неизвестный id один раз — иначе свип по смене локали спамит консоль.
    private static readonly System.Collections.Generic.HashSet<string> WarnedUnknownIds =
        new System.Collections.Generic.HashSet<string>();

    private static void ApplyStyle(TextMeshProUGUI tmp, string styleId)
    {
        var cfg = Config;
        if (cfg == null) return;

        if (cfg.GetStyle(styleId) == null)
        {
            if (WarnedUnknownIds.Add(styleId))
                Debug.LogWarning($"[Typography] Стиль '{styleId}' не найден в TypographyConfig — шрифт ноды не изменён.");
            return;
        }

        var font = cfg.ResolveFont(styleId, CurrentLang);
        if (font == null) font = TMP_Settings.defaultFontAsset; // fallback: LiberationSans SDF
        if (font != null) tmp.font = font;

        // Вес трогаем только по явному разрешению стиля.
        if (cfg.ResolveApplyFontStyle(styleId)) tmp.fontStyle = cfg.ResolveFontStyle(styleId);
    }

    /// <summary>Применить стиль (шрифт, и вес если разрешён) к TMP-тексту. Размер/трекинг не трогаются.</summary>
    public static void Apply(TextMeshProUGUI tmp, string styleId)
    {
        if (tmp == null) return;
        ApplyStyle(tmp, styleId);
    }

    /// <summary>Только шрифт/вес (HUD не трогаем: размеры HUD остаются сценарными).</summary>
    public static void ApplyFontOnly(TextMeshProUGUI tmp, string styleId)
    {
        if (tmp == null) return;
        ApplyStyle(tmp, styleId);
    }
}
