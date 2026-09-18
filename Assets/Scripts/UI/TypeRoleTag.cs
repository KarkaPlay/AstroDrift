using TMPro;
using UnityEngine;

/// <summary>
/// Носитель шрифтовой роли ноды (ТЗ §3.5, B5). Роли носят ТОЛЬКО теги: таблиц
/// «роль-по-имени» нет (хрупки при переименованиях), а нетегированные ноды не
/// трогаются (opt-in, §8.4 — гарантия нулевого визуального диффа).
///
/// Владелец тега — тот, кто создаёт ноду (§8.0): билдеры префабов и код сцены.
/// Смена локали идёт через LanguageService → TypeRoleApplier.ApplyAll();
/// стартовое и рантайм-применение — self-apply в OnEnable.
/// </summary>
public class TypeRoleTag : MonoBehaviour
{
    // ВНИМАНИЕ: имя поля 'role' зафиксировано ТЗ §3.5 — билдеры задачи 5 пишут роль
    // через SerializedObject.FindProperty("role"). Переименование сломает их молча.
    public TypeRole role;

    public void Apply() => Typography.ApplyFontOnly(GetComponent<TextMeshProUGUI>(), role);

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
