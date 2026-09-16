using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Единый способ заливки прогресс-баров (XP bar на старте/Death, прогресс до перка в HUD).
/// ОДИН механизм: Image.Type.Filled + Horizontal/Left, якоря растянуты (0,0)-(1,1),
/// значение живёт в fillAmount (закругление до 0.001 — чтобы 0.9999 не мигало полным).
/// Прежний анкорный вариант (anchorMax.x = t) конфликтовал с Debug-значением
/// fillAmount = 0.4, оставшимся в префабе: бар «врал» на любом значении.
/// </summary>
public static class UiProgressBar
{
    public static void Set(RectTransform fill, float t)
    {
        if (fill == null) return;
        var img = fill.GetComponent<Image>();
        if (img == null) return;

        // Без спрайта Image.OnPopulateMesh идёт по пути «полный квад» и ИГНОРИРУЕТ Type.Filled —
        // бар рисуется всегда полным при любом fillAmount. Раньше это молчало: «бар врал».
        if (img.sprite == null)
            Debug.LogError("UiProgressBar: у заливки '" + fill.name + "' нет спрайта — Image.Type.Filled игнорируется и бар рисуется всегда полным. Назначьте спрайт (Assets/New UI/Generated/RoundedBar.png).", img);

        // Куда бы нода ни «уехала» в сцене/префабе — приводим к канону здесь.
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;

        img.fillAmount = Mathf.Round(Mathf.Clamp01(t) * 1000f) / 1000f;
    }
}
