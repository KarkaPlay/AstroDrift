using UnityEngine;

/// <summary>
/// Один общий unlit-материал на всё (DevTask шаг 0). Цвет — per-renderer
/// через MaterialPropertyBlock (аналог SpriteRenderer.color, без клонирования материала).
/// </summary>
public static class MaterialProvider
{
    private static Material _shared;
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public static Material Shared
    {
        get
        {
            if (_shared == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                _shared = new Material(shader) { name = "ProceduralShared" };
            }
            return _shared;
        }
    }

    /// <summary>Назначает цвет renderer'у без клонирования общего материала.</summary>
    public static void SetColor(Renderer renderer, Color color)
    {
        if (renderer == null) return;
        var pb = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(pb);
        pb.SetColor(ColorId, color);
        renderer.SetPropertyBlock(pb);
    }
}
