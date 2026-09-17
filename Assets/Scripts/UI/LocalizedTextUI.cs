using TMPro;
using UnityEngine;

/// <summary>
/// Локализованная подпись прямо в префабе (ТЗ v1.10): ключ таблицы GameTexts живёт
/// в инспекторе, поэтому владелец правит подписи меню без кода
/// (значения — в GameTexts_ru/en.asset, добавляются через AstroDrift → Setup Localization).
/// Размер/трекинг остаются авторскими (из префаба), из конфига берётся только шрифт.
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class LocalizedTextUI : MonoBehaviour
{
    [Tooltip("Ключ таблицы GameTexts (напр. menu_settings)")]
    [SerializeField] private string key;

    [Tooltip("Роль типографики — определяет шрифт (размеры остаются из префаба)")]
    [SerializeField] private TypeRole role = TypeRole.Button;

    private void OnEnable()
    {
        Typography.LanguageChanged += ApplyFont;
        ApplyFont();
        L10n.Bind(GetComponent<TextMeshProUGUI>(), key);
    }

    private void OnDisable()
    {
        Typography.LanguageChanged -= ApplyFont;
    }

    private void ApplyFont()
    {
        Typography.ApplyFontOnly(GetComponent<TextMeshProUGUI>(), role);
    }
}
