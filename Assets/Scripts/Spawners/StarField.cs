using UnityEngine;

/// <summary>
/// Фоновые звёзды (VisualStyle §6.3/шаг 0) — конфиг-ориентированные слои
/// (StarFieldConfig: количество, цвет/яркость, разброс размеров, параллакс,
/// sorting-слои). Параллакс ОТНОСИТЕЛЬНЫЙ (дельтой движения камеры), поэтому
/// звёзды никогда не «отстают навсегда» от камеры при дальнем полёте (баг 2).
/// Звёзды НЕ попадают под bloom (серый < threshold).
/// </summary>
public class StarField : MonoBehaviour
{
    [SerializeField] private StarFieldConfig config;

    private Transform _cam;
    private Camera _camComponent;
    private float _halfW;
    private float _ortho;
    private float _margin = 1f;
    private float _belt = 10f;
    private Vector3 _prevCamPos;

    private class Star
    {
        public Transform T;
        public float Parallax;
        public Vector3 ScreenOffset; // офсет от камеры в юнитах экрана (относительный параллакс)
    }

    private readonly System.Collections.Generic.List<Star> _stars = new System.Collections.Generic.List<Star>();

    private void Start()
    {
        _camComponent = Camera.main != null ? Camera.main : GetComponentInParent<Camera>();
        _cam = _camComponent != null ? _camComponent.transform : transform;
        RefreshBounds();
        _prevCamPos = _cam.position;

        if (config == null)
        {
            Debug.LogError("StarField: не назначен StarFieldConfig — звёзды не созданы. " +
                           "Назначь ассет Assets/Resources/StarFieldConfig.asset в инспекторе.");
            return;
        }
        _margin = config.margin;
        _belt = config.respawnBelt;

        foreach (var layer in config.layers)
        {
            var mesh = GeometryFactory.Quad(layer.baseSize);
            for (int i = 0; i < layer.count; i++)
            {
                var go = new GameObject("Star_" + layer.name);
                go.transform.SetParent(transform);
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = MaterialProvider.Shared;
                MaterialProvider.SetColor(mr, layer.color);
                ApplySorting(mr, layer);

                var star = new Star
                {
                    T = go.transform,
                    Parallax = layer.parallax,
                    ScreenOffset = RandomScreenOffset()
                };
                // Индивидуальный размер: ×min – ×max от базы (баг 1)
                float scale = Random.Range(layer.sizeMultiplierMin, layer.sizeMultiplierMax);
                go.transform.localScale = new Vector3(scale, scale, 1f);
                _stars.Add(star);
                go.transform.position = _cam.position + star.ScreenOffset;
            }
        }
    }

    /// <summary>Sorting Layer / Order in Layer из конфига; слой создаётся, если отсутствует.</summary>
    private static void ApplySorting(Renderer mr, StarFieldConfig.StarLayer layer)
    {
        int layerId = int.MinValue;
        foreach (var sl in SortingLayer.layers)
            if (sl.name == layer.sortingLayerName) { layerId = sl.id; break; }
        if (layerId == int.MinValue)
        {
            // Слоя нет — падаем на Default (создавать слои кодом в рантайме нельзя)
            layerId = SortingLayer.NameToID("Default");
        }
        mr.sortingLayerID = layerId;
        mr.sortingOrder = layer.orderInLayer;
    }

    /// <summary>
    /// Границы кадра пересчитываются при каждом использовании: aspect зависит от
    /// разрешения/ориентации окна и может меняться в рантайме (десктоп-окно,
    /// поворот телефона, ТВ) — кэш из Start() рассыпался после смены аспекта.
    /// </summary>
    private void RefreshBounds()
    {
        _ortho = _camComponent != null ? _camComponent.orthographicSize : 12f;
        _halfW = ScreenBounds.HalfWidth(_camComponent);
    }

    private Vector3 RandomScreenOffset()
    {
        RefreshBounds();
        // ТЗ v3 (Доработка 1): переспавн ТОЛЬКО в кольце ЗА экраном —
        // звезда, переспавненная на глазах у игрока, выглядела как вспышка в кадре.
        // X: от края за боковую границу, Y: за верх/низ; по одной оси — всегда вне кадра.
        float x, y;
        if (Random.value < 0.5f)
        {
            x = (Random.value < 0.5f ? -1f : 1f) * Random.Range(_halfW + _margin, _halfW + _margin + _belt);
            y = Random.Range(-_ortho - _margin, _ortho + _margin);
        }
        else
        {
            x = Random.Range(-_halfW - _margin, _halfW + _margin);
            y = (Random.value < 0.5f ? -1f : 1f) * Random.Range(_ortho + _margin, _ortho + _margin + _belt);
        }
        return new Vector3(x, y, 0f);
    }

    private void LateUpdate()
    {
        if (_cam == null) return;
        Vector3 camPos = _cam.position;
        Vector3 delta = camPos - _prevCamPos; // движение камеры за кадр
        _prevCamPos = camPos;
        RefreshBounds(); // аспект/орто актуальны на каждый кадр (смена разрешения/ориентации)

        foreach (var s in _stars)
        {
            // Относительный параллакс: множитель применяется только к ДЕЛЬТЕ движения,
            // а не к абсолютной позиции камеры. Звёзды всегда «следуют» за камерой
            // с опозданием, а не «отстают навсегда».
            s.ScreenOffset -= delta * (1f - s.Parallax);

            Vector3 world = camPos + s.ScreenOffset;
            world.z = 0f; // звёзды на плоскости мира (z=0)
            s.T.position = world;

            // Переспавн: звезда ушла из кольца вокруг экрана — вернуть ЗА экран
            if (Mathf.Abs(s.ScreenOffset.x) > _halfW + _margin + _belt ||
                Mathf.Abs(s.ScreenOffset.y) > _ortho + _margin + _belt)
            {
                s.ScreenOffset = RandomScreenOffset();
            }
        }
    }
}
