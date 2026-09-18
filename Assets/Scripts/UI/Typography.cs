using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;

public enum TypeRole { Title, Secondary, Cta, DeathScore, Button, Body, LevelUpTitle }

/// <summary>
/// Единая точка доступа к типографике. Весь UI берёт шрифты ТОЛЬКО отсюда
/// (Assets/Resources/TypographyConfig.asset). Размеры и трекинг — авторские,
/// из префабов/сцены: Typography их не трогает.
/// Шрифт выбирается по текущей локали Unity (переопределения локали в конфиге);
/// пока владелец не подставил шрифты — fallback на TMP Settings default
/// (LiberationSans SDF): ни Missing, ни Null, сцена работает с пустым конфигом.
///
/// Реакции на смену локали у Typography НЕТ (ТЗ §3.5): единственная точка подписки на
/// SelectedLocaleChanged — LanguageService, он же вызывает TypeRoleApplier.ApplyAll()
/// и сброс гардов §3.4. Носитель роли — TypeRoleTag (self-apply в OnEnable).
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

    /// <summary>Шрифт для роли с учётом языка. null — вызывающий берёт TMP Settings default.</summary>
    private static TMP_FontAsset Resolve(TypeRole role)
    {
        var cfg = Config;
        if (cfg == null) return null;
        var f = cfg.GetFonts(CurrentLang);
        switch (role)
        {
            case TypeRole.LevelUpTitle:
                return f.title != null ? f.title : f.heading;
            case TypeRole.Title:
            case TypeRole.DeathScore:
                return f.heading;
            case TypeRole.Cta:
                return f.cta;
            default: // Secondary / Button / Body — служебные тексты
                return f.body;
        }
    }

    private static void ApplyFont(TextMeshProUGUI tmp, TypeRole role)
    {
        var font = Resolve(role);
        if (font == null) font = TMP_Settings.defaultFontAsset; // fallback: LiberationSans SDF
        if (font != null) tmp.font = font;
        tmp.fontStyle = FontStyles.Normal;
    }

    /// <summary>Применить роль (шрифт + вес) к TMP-тексту. Размер/трекинг не трогаются.</summary>
    public static void Apply(TextMeshProUGUI tmp, TypeRole role)
    {
        if (tmp == null) return;
        ApplyFont(tmp, role);
    }

    /// <summary>Только шрифт/вес (HUD не трогаем: размеры HUD остаются сценарными).</summary>
    public static void ApplyFontOnly(TextMeshProUGUI tmp, TypeRole role)
    {
        if (tmp == null) return;
        ApplyFont(tmp, role);
    }
}
