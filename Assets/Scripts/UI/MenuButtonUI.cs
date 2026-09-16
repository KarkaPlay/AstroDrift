using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Нижняя кнопка меню (ТЗ v1.10: Настройки / Прокачка / Магазин).
/// Экранов за кнопками пока нет — клик только логирует событие и НЕ стартует игру
/// (кнопка перехватывает тап раньше полноэкранной зоны Btn_TapToPlay).
/// </summary>
[RequireComponent(typeof(Button))]
public class MenuButtonUI : MonoBehaviour
{
    [Tooltip("Идентификатор для аналитики: settings / upgrade / shop")]
    [SerializeField] private string id = "settings";

    private void OnEnable()
    {
        var btn = GetComponent<Button>();
        btn.onClick.RemoveListener(OnClick);
        btn.onClick.AddListener(OnClick);
    }

    private void OnDisable()
    {
        var btn = GetComponent<Button>();
        if (btn != null) btn.onClick.RemoveListener(OnClick);
    }

    private void OnClick()
    {
        Analytics.Log("menu_button", new Dictionary<string, object> { { "id", id } });
    }
}
