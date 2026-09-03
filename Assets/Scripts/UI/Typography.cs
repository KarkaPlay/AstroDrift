using TMPro;
using UnityEngine;

public enum TypeRole { Title, Secondary, Cta, DeathScore, Button, Body }

/// <summary>
/// Единая точка доступа к типографике «Menu & Transitions v2» (§3, поправка владельца).
/// Весь UI ссылается на шрифты ТОЛЬКО через TypographyConfig (Assets/Resources/TypographyConfig.asset).
/// Пока владелец не подставил шрифты — корректный fallback на TMP Settings default
/// (LiberationSans SDF): ни Missing, ни Null, сцена работает с пустым конфигом.
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

    /// <summary>Применить роль (шрифт + вес + размер + трекинг) к TMP-тексту.</summary>
    public static void Apply(TextMeshProUGUI tmp, TypeRole role)
    {
        if (tmp == null) return;
        var cfg = Config;

        TMP_FontAsset font;
        float size, spacing;
        FontStyles style;

        switch (role)
        {
            case TypeRole.Title:
                font = cfg != null ? cfg.headingLight : null;
                size = cfg != null ? cfg.titleSize : 96f;
                spacing = cfg != null ? cfg.titleTracking : 12f;
                style = FontStyles.Normal;
                break;
            case TypeRole.Secondary:
                font = cfg != null ? cfg.bodyRegular : null;
                size = cfg != null ? cfg.secondarySize : 34f;
                spacing = cfg != null ? cfg.secondaryTracking : 8f;
                style = FontStyles.Normal;
                break;
            case TypeRole.Cta:
                font = cfg != null ? cfg.ctaSemiBold : null;
                size = cfg != null ? cfg.ctaSize : 44f;
                spacing = cfg != null ? cfg.ctaTracking : 16f;
                style = FontStyles.Normal;
                break;
            case TypeRole.DeathScore:
                font = cfg != null ? cfg.headingLight : null;
                size = cfg != null ? cfg.deathScoreSize : 88f;
                spacing = cfg != null ? cfg.deathScoreTracking : 4f;
                style = FontStyles.Normal;
                break;
            case TypeRole.Button:
                font = cfg != null ? cfg.bodyRegular : null;
                size = cfg != null ? cfg.buttonSize : 40f;
                spacing = cfg != null ? cfg.buttonTracking : 16f;
                style = FontStyles.Normal;
                break;
            default: // Body — служебные тексты
                font = cfg != null ? cfg.bodyRegular : null;
                size = cfg != null ? cfg.secondarySize : 34f;
                spacing = 0f;
                style = FontStyles.Normal;
                break;
        }

        if (font == null) font = TMP_Settings.defaultFontAsset; // fallback: LiberationSans SDF
        if (font != null) tmp.font = font;
        tmp.fontStyle = style;
        tmp.fontSize = size;
        tmp.characterSpacing = spacing; // TMP: значение в 1/100 em → «+12 %» = 12
    }

    /// <summary>Только шрифт/вес (HUD не трогаем: размеры HUD остаются сценарными).</summary>
    public static void ApplyFontOnly(TextMeshProUGUI tmp, TypeRole role)
    {
        if (tmp == null) return;
        var cfg = Config;
        TMP_FontAsset font;
        switch (role)
        {
            case TypeRole.Title:
            case TypeRole.DeathScore:
                font = cfg != null ? cfg.headingLight : null; break;
            case TypeRole.Cta:
                font = cfg != null ? cfg.ctaSemiBold : null; break;
            default:
                font = cfg != null ? cfg.bodyRegular : null; break;
        }
        if (font == null) font = TMP_Settings.defaultFontAsset;
        if (font != null) tmp.font = font;
    }
}
