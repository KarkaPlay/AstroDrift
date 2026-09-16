using UnityEngine;

/// <summary>
/// Пикап (GDD §4.6, Poolable §0.8): висит pickupLifetime сек, последние N мигает,
/// подбор касанием хитбокса корабля (trigger). Пульсация scale 1.0↔1.15 (0.5 с).
/// Визуал — процедурный ромб (GeometryFactory), цвет — из PickupConfig.
/// </summary>
public class Pickup : Poolable
{
    [SerializeField] private GameConfig config; // не нужен; числа из PickupConfig через Init

    private PickupConfig _cfg;
    private PickupDef _def;
    private MeshRenderer _renderer;
    private CircleCollider2D _collider;
    private float _life;

    public PickupDef Def => _def;

    private void Awake()
    {
        _collider = GetComponent<CircleCollider2D>();
        if (_collider == null) _collider = gameObject.AddComponent<CircleCollider2D>();
        _collider.isTrigger = true;
        BuildMesh();
    }

    /// <summary>Процедурный ромб (для будущих форм можно расширить по типу).</summary>
    private void BuildMesh()
    {
        if (GetComponent<MeshFilter>() == null) gameObject.AddComponent<MeshFilter>();
        if (_renderer == null)
        {
            _renderer = GetComponent<MeshRenderer>();
            if (_renderer == null) _renderer = gameObject.AddComponent<MeshRenderer>();
        }
        var poly = GeometryFactory.FromPoints(new[]
        {
            new Vector2(0f, 0.28f), new Vector2(0.2f, 0f),
            new Vector2(0f, -0.28f), new Vector2(-0.2f, 0f),
        });
        GetComponent<MeshFilter>().sharedMesh = poly.Mesh;
        _renderer.sharedMaterial = MaterialProvider.Shared;
    }

    public void Init(PickupConfig cfg)
    {
        _cfg = cfg;
        if (_collider != null) _collider.radius = cfg.pickupRadius;
    }

    public void SpawnAt(PickupDef def, Vector3 pos)
    {
        _def = def;
        transform.position = pos;
        _life = _cfg != null ? _cfg.pickupLifetime : 6f;
        MaterialProvider.SetColor(_renderer, def.color); // без клонирования общего материала
        gameObject.SetActive(true);
    }

    private void Update()
    {
        float lifetime = _cfg != null ? _cfg.pickupLifetime : 6f;
        float blink = _cfg != null ? _cfg.pickupBlinkLastSeconds : 2f;
        _life -= Time.deltaTime;
        if (_life <= 0f) { Release(); return; }

        // Пульсация scale 1.0↔1.15 (0.5 с, GDD §4.6 juice)
        float pulse = 1f + 0.15f * (0.5f + 0.5f * Mathf.Sin(Time.time * (2f * Mathf.PI / 0.5f)));
        transform.localScale = Vector3.one * pulse;

        // Мигание последние N сек (альфа через материал — общий материал, поэтому гасим видимостью)
        if (_life < blink)
            _renderer.enabled = Mathf.Repeat(_life, 0.25f) > 0.1f;
        else
            _renderer.enabled = true;
    }

    private void ReleaseSelf() => Release();

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<ShipController>() == null) return;
        PickupManager.Instance?.OnPickedUp(this);
        ReleaseSelf();
    }
}
