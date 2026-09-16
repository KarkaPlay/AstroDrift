using UnityEngine;

/// <summary>
/// Единый способ заливки прогресс-баров (XP bar на старте/Death, прогресс до перка в HUD).
/// Работает АНКОРАМИ, а не Image.fillAmount/Filled: Filled-тип требует одинаковых якорей
/// у фона и заливки и молча даёт пустой/полный бар, если заливка растянута анкорами.
/// t = 0 → заливка нулевой ширины (пустой бар — ожидаемое поведение, минимальной «искры» нет).
/// </summary>
public static class UiProgressBar
{
    public static void Set(RectTransform fill, float t)
    {
        if (fill == null) return;
        t = Mathf.Clamp01(t);
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(t, 1f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
    }
}
