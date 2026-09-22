using TMPro;
using UnityEngine;

/// <summary>
/// Носитель ШТИЛЯ типографики ноды. Стиль выбирается из TypographyConfig по styleId
/// (список — в кастомном инспекторе, рядом кнопка-ссылка на конфиг).
///
/// Пустой styleId = тег ничего не делает (opt-in/opt-out): шрифт ноды из префаба
/// сохраняется — это же гарантирует нулевой визуальный дифф для нетегированных нод.
/// Неизвестный id = одно предупреждение в консоль и никаких изменений.
///
/// Смена локали идёт через LanguageService → TypeRoleApplier.ApplyAll();
/// стартовое и рантайм-применение — self-apply в OnEnable.
/// </summary>
public class TypeRoleTag : MonoBehaviour
{
    [Tooltip("Стиль из TypographyConfig. Пусто — тег ничего не делает (шрифт префаба сохраняется).")]
    public string styleId;

    public void Apply()
    {
        if (string.IsNullOrEmpty(styleId)) return;
        Typography.ApplyFontOnly(GetComponent<TextMeshProUGUI>(), styleId);
    }

    // Self-apply (N2): карты перков создаются/уничтожаются в рантайме — одноразовый
    // свип при старте их не увидит. Шрифт не зависит от текста, поэтому порядок
    // OnEnable vs FillCard не важен.
    private void OnEnable() => Apply();
}

/// <summary>
/// Свип — ТОЛЬКО для смены локали (стартовые и рантайм-ноды покрывает OnEnable тега).
/// Opt-in: нетегированные ноды не трогаются (HUD и Ко, §8.4).
/// </summary>
public static class TypeRoleApplier
{
    public static void ApplyAll()
    {
        foreach (var tag in Object.FindObjectsByType<TypeRoleTag>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            tag.Apply();
    }
}
