using UnityEngine;

/// <summary>
/// Процедурная геометрия (VisualStyle §5): ни одного ручного спрайта.
/// Все игровые формы — полигоны, строятся по вершинам.
/// </summary>
public static class GeometryFactory
{
    /// <summary>Результат генерации полигона: меш + контур (для коллайдера/обводки).</summary>
    public class PolyData
    {
        public Mesh Mesh;
        public Vector2[] Outline; // вершины по порядку (замкнуты визуально, без дублирования первой)
    }

    /// <summary>Равнобедренный треугольник, нос вверх. (корабль 0.6 × 0.8)</summary>
    public static Mesh Triangle(float width, float height)
    {
        var v = new Vector3[]
        {
            new Vector3(0f, height * 0.5f, 0f),
            new Vector3(-width * 0.5f, -height * 0.5f, 0f),
            new Vector3(width * 0.5f, -height * 0.5f, 0f),
        };
        return Build(v, new[] { 0, 1, 2 });
    }

    /// <summary>Квадрат (звёзды 1×1/2×2 px, осколки частиц).</summary>
    public static Mesh Quad(float size)
    {
        float h = size * 0.5f;
        var v = new Vector3[]
        {
            new Vector3(-h, -h, 0), new Vector3(h, -h, 0), new Vector3(h, h, 0), new Vector3(-h, h, 0),
        };
        return Build(v, new[] { 0, 1, 2, 0, 2, 3 });
    }

    /// <summary>
    /// Неправильный N-угольник (астероид): радиус = base × (1 + rand(−0.35…+0.35)),
    /// случайный поворот, контур — те же вершины.
    /// </summary>
    public static PolyData IrregularPolygon(float baseRadius, int verts, float irregularity = 0.35f)
    {
        var points = new Vector2[verts];
        float rot = Random.Range(0f, Mathf.PI * 2f);
        for (int i = 0; i < verts; i++)
        {
            float a = i / (float)verts * Mathf.PI * 2f + rot;
            float r = baseRadius * (1f + Random.Range(-irregularity, irregularity));
            points[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
        return FromPoints(points);
    }

    /// <summary>Меш + контур из набора вершин (fan-триангуляция, выпуклые формы).</summary>
    public static PolyData FromPoints(Vector2[] points)
    {
        int n = points.Length;
        var v = new Vector3[n];
        for (int i = 0; i < n; i++) v[i] = new Vector3(points[i].x, points[i].y, 0f);

        var t = new int[(n - 2) * 3];
        for (int i = 1; i < n - 1; i++)
        {
            t[(i - 1) * 3 + 0] = 0;
            t[(i - 1) * 3 + 1] = i;
            t[(i - 1) * 3 + 2] = i + 1;
        }
        return new PolyData { Mesh = Build(v, t), Outline = points };
    }

    public static Mesh Build(Vector3[] vertices, int[] triangles)
    {
        var mesh = new Mesh { name = "Proc" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Создаёт тонкую обводку контура (LineRenderer loop). Цвет — тёмный контур из спеки.</summary>
    public static LineRenderer CreateOutline(GameObject go, Vector2[] outline, Color color, float width)
    {
        var lr = go.AddComponent<LineRenderer>();
        lr.material = MaterialProvider.Shared;
        lr.startColor = color;
        lr.endColor = color;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.loop = true;
        lr.useWorldSpace = false;
        lr.positionCount = outline.Length;
        for (int i = 0; i < outline.Length; i++)
            lr.SetPosition(i, new Vector3(outline[i].x, outline[i].y, 0f));
        return lr;
    }

}
