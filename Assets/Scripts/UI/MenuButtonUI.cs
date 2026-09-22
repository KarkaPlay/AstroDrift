using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Нижняя кнопка меню (ТЗ v1.10: Настройки / Прокачка / Магазин).
/// Клик логирует событие и НЕ стартует игру (кнопка перехватывает тап раньше
/// полноэкранной зоны Btn_TapToPlay). Экран есть только у «Настройки» — остальные
/// по-прежнему логируют и ничего не открывают.
/// </summary>
[RequireComponent(typeof(Button))]
public class MenuButtonUI : MonoBehaviour
{
    [Tooltip("Идентификатор для аналитики: settings / upgrade / shop")]
    [SerializeField] private string id = "settings";

    /// <summary>Экран, который открывает кнопка. Ничего = только аналитика (шоп/прокачка).</summary>
    public enum MenuAction { None, OpenSettings }

    [Tooltip("Что делать по клику. Настройки → OpenSettings; остальные оставить None")]
    [SerializeField] private MenuAction action = MenuAction.None;

    [Tooltip("Корень экрана настроек (GameUI.SettingsScreen). Обязателен при action = OpenSettings")]
    [SerializeField] private SettingsScreen targetScreen;

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
        if (action == MenuAction.OpenSettings && targetScreen != null) targetScreen.Show();
    }
}
