using UnityEngine;

/// <summary>
/// Единый источник цветов — палитра VisualStyle §3.1.
/// «Железные» константы: хардкод разрешён только тут (DevTask правило 5).
/// Семантика: жёлтый = моя атака, красный = угроза, оранжевый = препятствие,
/// белый = я/UI, серый = мир.
/// </summary>
public static class Palette
{
    // Фон и звёзды
    public static readonly Color StarFar = Hex("#2E2E2E");
    public static readonly Color StarNear = Hex("#4A4A4A");

    // Игрок и его атака
    public static readonly Color Ship = Hex("#FFFFFF");
    public static readonly Color Bullet = Hex("#FFD700");

    // Мир
    public static readonly Color AsteroidOutline = Hex("#1F1F1F");

    // Угроза
    public static readonly Color Missile = Hex("#FF3333");
    public static readonly Color WarningRed = Hex("#FF3333");

    // UI
    public static readonly Color ScoreText = Hex("#FFFFFF");
    public static readonly Color SecondaryText = Hex("#8A8A8A");
    public static readonly Color Gold = Hex("#FFD700");
    public static readonly Color UiPanel = new Color(0.078f, 0.078f, 0.078f, 0.92f); // #141414 a0.92
    public static readonly Color UiPanelFrame = Hex("#2A2A2A");
    public static readonly Color UiOverlay = new Color(0f, 0f, 0f, 0.6f); // затемнение паузы

    public static Color AsteroidShade(float t)
    {
        // Серый оттенок 0.45–0.70 (VisualStyle §5) с сохранением яркости в HDR для лёгкого свечения краёв.
        float v = Mathf.Lerp(0.45f, 0.70f, t);
        return new Color(v, v, v, 1f);
    }

    /// <summary>Цвет текста множителя по уровню (VisualStyle §3.1).</summary>
    public static Color ComboColor(int multiplier)
    {
        switch (multiplier)
        {
            case 2: return Hex("#66FF66");
            case 3: return Hex("#66CCFF");
            case 4: return Hex("#FF66FF");
            case 5: return Hex("#FFD700");
            default: return Color.white;
        }
    }

    public static Color Hex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
        {
            int r = System.Convert.ToInt32(hex.Substring(0, 2), 16);
            int g = System.Convert.ToInt32(hex.Substring(2, 2), 16);
            int b = System.Convert.ToInt32(hex.Substring(4, 2), 16);
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }
        Debug.LogWarning($"Palette: неверный hex '{hex}'");
        return Color.magenta;
    }
}
