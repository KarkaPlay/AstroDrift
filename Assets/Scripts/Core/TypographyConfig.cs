using TMPro;
using UnityEngine;

/// <summary>
/// Типографический конфиг: СПИСОК СТИЛЕЙ. Размеры и трекинг — авторские,
/// живут в префабах/сцене (Typography их не трогает).
///
/// ИНСТРУКЦИЯ ДЛЯ ВЛАДЕЛЬЦА — как добавить/изменить шрифт:
///   1. Положите .ttf файлы в Assets/Fonts/ (подойдёт ЛЮБОЙ шрифт).
///   2. ПКМ по каждому .ttf → Create → TextMeshPro → Font Asset.
///   3. Откройте Assets/Resources/TypographyConfig.asset. Каждая строка списка
///      Styles — это один стиль: Id (слаг, ключ), Display Name (подпись в инспекторе),
///      Font, Font Style и чекбокс Apply Font Style.
///      «Добавить стиль» = «+» под списком Styles.
///   4. Чтобы переопределить шрифт для конкретного ЯЗЫКА — раскройте Locale Fonts
///      у нужного стиля, добавьте элемент с кодом локали (LocaleIdentifier.Code:
///      `ru`, `en`, `zh-Hans`) и укажите шрифт. Пустое поле = базовый шрифт стиля.
///   5. Кто применяет стиль к ноде: компонент TypeRoleTag на текстовой ноде
///      (список выбора стиля — в его инспекторе, рядом кнопка-ссылка на этот конфиг).
///      Пустой Style Id = тег ничего не делает, шрифт ноды из префаба сохраняется.
///
/// Пустые поля = fallback на LiberationSans SDF (TMP Settings) —
/// никаких Missing/Null, сцена полностью работает и без заполненного конфига.
/// </summary>
[CreateAssetMenu(fileName = "TypographyConfig", menuName = "AstroDrift/TypographyConfig")]
public class TypographyConfig : ScriptableObject
{
    [Tooltip("Стили типографики. Id — стабильный ключ (слаг), на него ссылаются TypeRoleTag.")]
    public TypographyStyle[] styles;

    /// <summary>Стиль по id. null — если конфиг пуст или id не найден.</summary>
    public TypographyStyle GetStyle(string id)
    {
        if (string.IsNullOrEmpty(id) || styles == null) return null;
        for (int i = 0; i < styles.Length; i++)
        {
            var s = styles[i];
            if (s == null || string.IsNullOrEmpty(s.id)) continue;
            if (string.Equals(s.id, id, System.StringComparison.OrdinalIgnoreCase)) return s;
        }
        return null;
    }

    /// <summary>Шрифт стиля с учётом языка. null — вызывающий берёт TMP Settings default.</summary>
    public TMP_FontAsset ResolveFont(string id, string localeCode)
    {
        var style = GetStyle(id);
        if (style == null) return null;
        if (!string.IsNullOrEmpty(localeCode) && style.localeFonts != null)
            for (int i = 0; i < style.localeFonts.Length; i++)
            {
                var lf = style.localeFonts[i];
                if (lf == null || lf.font == null || string.IsNullOrEmpty(lf.localeCode)) continue;
                if (string.Equals(lf.localeCode, localeCode, System.StringComparison.OrdinalIgnoreCase)) return lf.font;
            }
        return style.font;
    }

    /// <summary>Вес символов стиля. Normal, если стиль не найден или Apply Font Style выключен.</summary>
    public FontStyles ResolveFontStyle(string id)
    {
        var style = GetStyle(id);
        return style != null && style.applyFontStyle ? style.fontStyle : FontStyles.Normal;
    }

    /// <summary>Применять ли fontStyle стиля к ноде. false — fontStyle ноды не трогается.</summary>
    public bool ResolveApplyFontStyle(string id)
    {
        var style = GetStyle(id);
        return style != null && style.applyFontStyle;
    }
}

/// <summary>Один стиль типографики: базовый шрифт + вес + переопределения по языкам.</summary>
[System.Serializable]
public class TypographyStyle
{
    [Tooltip("Стабильный ключ-слаг (напр. body). Менять нельзя — на него ссылаются TypeRoleTag.")]
    public string id;

    [Tooltip("Подпись для инспектора. Можно оставить пустой — тогда показывается id.")]
    public string displayName;

    [Tooltip("Базовый шрифт. Пусто = fallback LiberationSans SDF.")]
    public TMP_FontAsset font;

    [Tooltip("Вес символов. Применяется, только если включён Apply Font Style.")]
    public FontStyles fontStyle = FontStyles.Normal;

    [Tooltip("Выключено (по умолчанию): стиль меняет только шрифт, вес ноды из префаба сохраняется.")]
    public bool applyFontStyle;

    [Tooltip("Переопределения шрифта по языкам. Пустое поле шрифта = базовый шрифт стиля.")]
    public TypographyLocaleFont[] localeFonts;
}

/// <summary>Шрифт стиля для одного языка (LocaleIdentifier.Code: ru, en).</summary>
[System.Serializable]
public class TypographyLocaleFont
{
    [Tooltip("Код локали (LocaleIdentifier.Code): ru, en, zh-Hans…")]
    public string localeCode;

    public TMP_FontAsset font;
}

/// <summary>Ключи засеянных стилей — миграция, билдеры и сцена ссылаются на одни и те же строки.</summary>
public static class TypographyStyles
{
    public const string Title = "title";
    public const string Secondary = "secondary";
    public const string Cta = "cta";
    public const string DeathScore = "deathScore";
    public const string Button = "button";
    public const string Body = "body";
    public const string LevelUpTitle = "levelUpTitle";
}

/// <summary>Имена полей конфига в одном месте — правки значений миграции не расходятся с кодом.</summary>
public static class TypographyConfigFields
{
    public const string Styles = "styles";
    public const string Id = "id";
    public const string DisplayName = "displayName";
    public const string Font = "font";
    public const string FontStyle = "fontStyle";
    public const string ApplyFontStyle = "applyFontStyle";
    public const string LocaleFonts = "localeFonts";
    public const string LocaleCode = "localeCode";
}
