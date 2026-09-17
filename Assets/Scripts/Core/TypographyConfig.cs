using TMPro;
using UnityEngine;

/// <summary>
/// Типографический конфиг: ТОЛЬКО шрифты. Размеры и трекинг — авторские,
/// живут в префабах/сцене (Typography их не трогает).
///
/// ИНСТРУКЦИЯ ДЛЯ ВЛАДЕЛЬЦА — как подставить шрифты:
///   1. Положите .ttf файлы в Assets/Fonts/ (подойдёт ЛЮБОЙ шрифт).
///   2. ПКМ по каждому .ttf → Create → TextMeshPro → Font Asset.
///   3. Откройте Assets/Resources/TypographyConfig.asset и перетащите созданные
///      TMP Font Asset'ы в базовые слоты: Heading Light / Title Bold /
///      Body Regular / CTA SemiBold.
///   4. Нужен другой шрифт для конкретного языка — добавьте элемент в
///      Language Overrides, укажите код языка и заполните только те слоты,
///      которые отличаются (пустой слот = базовый).
///
/// Пустые поля = fallback на LiberationSans SDF (TMP Settings) —
/// никаких Missing/Null, сцена полностью работает и без заполненного конфига.
/// </summary>
[CreateAssetMenu(fileName = "TypographyConfig", menuName = "AstroDrift/TypographyConfig")]
public class TypographyConfig : ScriptableObject
{
    [Header("Базовые шрифты (пусто = fallback LiberationSans SDF)")]
    [Tooltip("Заголовки и крупные цифры (Death: Score) — Light вес")]
    public TMP_FontAsset headingLight;

    [Tooltip("Заголовок оверлея левел-апа. Пусто — берётся headingLight")]
    public TMP_FontAsset titleBold;

    [Tooltip("Основной текст — Regular вес")]
    public TMP_FontAsset bodyRegular;

    [Tooltip("CTA / кнопки — SemiBold вес")]
    public TMP_FontAsset ctaSemiBold;

    [Header("Переопределения по языку (пустой слот = базовый шрифт)")]
    public TypographyLanguageFonts[] languageOverrides;

    /// <summary>Набор разрешённых шрифтов для языка (базовые + переопределения).</summary>
    public FontSet GetFonts(string langCode)
    {
        var set = new FontSet
        {
            heading = headingLight,
            title = titleBold,
            body = bodyRegular,
            cta = ctaSemiBold,
        };
        if (string.IsNullOrEmpty(langCode) || languageOverrides == null) return set;

        for (int i = 0; i < languageOverrides.Length; i++)
        {
            var o = languageOverrides[i];
            if (o == null || string.IsNullOrEmpty(o.langCode)) continue;
            if (!string.Equals(o.langCode, langCode, System.StringComparison.OrdinalIgnoreCase)) continue;
            if (o.heading != null) set.heading = o.heading;
            if (o.titleBold != null) set.title = o.titleBold;
            if (o.body != null) set.body = o.body;
            if (o.cta != null) set.cta = o.cta;
            break;
        }
        return set;
    }
}

/// <summary>Шрифты одного языка: пустое поле = взять базовый слот конфига.</summary>
[System.Serializable]
public class TypographyLanguageFonts
{
    [Tooltip("Код языка YG2 (ru, en, tr…)")]
    public string langCode;

    public TMP_FontAsset heading;
    public TMP_FontAsset titleBold;
    public TMP_FontAsset body;
    public TMP_FontAsset cta;
}

/// <summary>Разрешённые шрифты по слотам (null = не задан).</summary>
public struct FontSet
{
    public TMP_FontAsset heading;
    public TMP_FontAsset title;
    public TMP_FontAsset body;
    public TMP_FontAsset cta;
}
