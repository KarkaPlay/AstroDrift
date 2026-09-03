using TMPro;
using UnityEngine;

/// <summary>
/// Типографический конфиг «Menu & Transitions v2» (§3, поправка владельца).
///
/// ИНСТРУКЦИЯ ДЛЯ ВЛАДЕЛЬЦА — как подставить шрифты:
///   1. Положите .ttf файлы (например Inter-Light.ttf, Inter-Regular.ttf,
///      Inter-SemiBold.ttf — но подойдёт ЛЮБОЙ шрифт) в папку Assets/TextMesh Pro/Fonts/.
///   2. ПКМ по каждому .ttf → Create → TextMeshPro → Font Asset
///      (создаст SDF-ассет рядом с файлом; для статического атласа —
///      Multi Atlas Textures можно выключить, ASCII достаточно).
///   3. Откройте ассет Assets/Resources/TypographyConfig.asset и перетащите
///      созданные TMP Font Asset'ы в поля: Heading Light / Body Regular / CTA SemiBold.
///   4. Всё. Весь UI подхватит шрифты автоматически (сцена перестроится при следующем запуске).
///
/// Пустые поля = fallback на LiberationSans SDF (TMP Settings) —
/// никаких Missing/Null, сцена полностью работает и без заполненного конфига.
/// Конфиг НЕ прибит к Inter: подставляются любые TMP Font Asset.
/// </summary>
[CreateAssetMenu(fileName = "TypographyConfig", menuName = "AstroDrift/TypographyConfig")]
public class TypographyConfig : ScriptableObject
{
    [Header("Шрифты (пусто = fallback LiberationSans SDF)")]
    [Tooltip("Заголовки и крупные цифры — Light вес (напр. Inter Light)")]
    public TMP_FontAsset headingLight;

    [Tooltip("Основной текст — Regular вес (напр. Inter Regular)")]
    public TMP_FontAsset bodyRegular;

    [Tooltip("CTA / кнопки — SemiBold вес (напр. Inter SemiBold)")]
    public TMP_FontAsset ctaSemiBold;

    [Header("Типографическая шкала §3 (1080×1920): размеры и трекинг (+%)")]
    [Tooltip("Заголовок ASTRO DRIFT")]
    public float titleSize = 96f;
    public float titleTracking = 12f;

    [Tooltip("Best / служебные")]
    public float secondarySize = 34f;
    public float secondaryTracking = 8f;

    [Tooltip("CTA «TAP TO PLAY»")]
    public float ctaSize = 44f;
    public float ctaTracking = 16f;

    [Tooltip("Death: Score")]
    public float deathScoreSize = 88f;
    public float deathScoreTracking = 4f;

    [Tooltip("Кнопки RETRY / HOME / RESUME")]
    public float buttonSize = 40f;
    public float buttonTracking = 16f;
}
