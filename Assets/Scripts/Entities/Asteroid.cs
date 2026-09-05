using UnityEngine;

/// <summary>
/// Астероид (GDD §4.3): движение, вращение, HP, раскалывание, деспавн.
/// Пул на каждый размер. PolygonCollider2D по сгенерированным вершинам.
/// </summary>
public class Asteroid : Poolable
{
    [SerializeField] private GameConfig config;

    private AsteroidSize _size;
    private AsteroidSize _childSize;
    private int _childCount;
    private int _hp;
    private float _speed;
    private Vector2 _dir;
    private float _angularSpeed;
    private Color _restingColor = new Color(0.5f, 0.5f, 0.5f);
    private PolygonCollider2D _collider;
    private MeshFilter _mf;
    private MeshRenderer _mr;
    private float _flashTimer;
    private float _shakeTimer;
    private bool _active;

    public AsteroidSize Size => _size;

    public void Init(GameConfig cfg)
    {
        config = cfg;
        _collider = GetComponent<PolygonCollider2D>();
        _mf = GetComponent<MeshFilter>();
        _mr = GetComponent<MeshRenderer>();
    }

    /// <summary>Запуск астероида в пуле: генерация формы, физика, направление.</summary>
    public void Spawn(AsteroidSize size, Vector2 pos, Vector2 dir, float speed, float angularSpeed)
    {
        _size = size;
        _active = true;
        transform.position = pos;
        transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        // Генерация процедурного полигона (VisualStyle §5)
        GameConfig.AsteroidSizeDef def = GetDef(size);
        int verts = Random.Range(def.minVerts, def.maxVerts + 1);
        var data = GeometryFactory.IrregularPolygon(def.radius, verts);
        _mf.sharedMesh = data.Mesh;
        _collider.points = data.Outline;
        _mr.sharedMaterial = MaterialProvider.Shared;
        _restingColor = Palette.AsteroidShade(Random.value);
        MaterialProvider.SetColor(_mr, _restingColor);

        // Контур (LineRenderer) — тёмный #1F1F1F, толщина 0.02 (VisualStyle §5)
        var lr = GetComponent<LineRenderer>();
        if (lr == null) lr = GeometryFactory.CreateOutline(gameObject, data.Outline, Palette.AsteroidOutline, 0.02f);
        else
        {
            lr.positionCount = data.Outline.Length;
            for (int i = 0; i < data.Outline.Length; i++)
                lr.SetPosition(i, new Vector3(data.Outline[i].x, data.Outline[i].y, 0f));
        }

        _hp = def.hp;
        _childSize = def.childSize;
        _childCount = def.childCount;
        _dir = dir.normalized;
        _speed = speed;
        _angularSpeed = angularSpeed;
        _flashTimer = 0f;
        _shakeTimer = 0f;
    }

    private GameConfig.AsteroidSizeDef GetDef(AsteroidSize size)
    {
        switch (size)
        {
            case AsteroidSize.Large: return config.asteroidSizes[0];
            case AsteroidSize.Medium: return config.asteroidSizes[1];
            default: return config.asteroidSizes[2];
        }
    }

    /// <summary>Попадание снаряда. Возвращает true, если астероид уничтожен.</summary>
    public bool Hit(int damage, Vector3 hitPoint)
    {
        _hp -= damage;
        if (_hp <= 0)
        {
            Break();
            return true;
        }
        // Вспышка белым на 1 кадр + тряска позиции (GDD §4.3)
        _flashTimer = config.hitFlashTime;
        _shakeTimer = config.hitShakeDuration;
        AudioManager.Instance?.PlayHit();
        GameEvents.RaiseAsteroidHit(hitPoint);
        return false;
    }

    private void Break()
    {
        _active = false;
        Vector2 center = transform.position;

        // Осколки разлетаются из центра со скоростью 2–3 ю/с в противоположных направлениях (GDD §4.3)
        GameConfig.AsteroidSizeDef childDef = GetDef(_childSize);
        Vector2 splitDir = Random.insideUnitCircle.normalized;
        float splitSpeed = Random.Range(2f, 3f);
        for (int i = 0; i < _childCount; i++)
        {
            Vector2 d = Quaternion.Euler(0f, 0f, i * 180f + Random.Range(-25f, 25f)) * splitDir;
            Vector3 childPos = new Vector3(center.x, center.y, 0f) + (Vector3)(d * (childDef.radius + 0.25f));
            GameEvents.RaiseSpawnAsteroid(_childSize, childPos, d, splitSpeed, Random.Range(20f, 90f));
        }

        // Частицы + очки + звук (через события — слушают GameManager/VFX/Audio)
        GameEvents.RaiseAsteroidDestroyed(center, _size);
        Release();
    }

    private void Update()
    {
        if (!_active) return;

        // Деспавн (GDD §4.3, адаптив на любой аспект): астероид ушел за видимой
        // рамкой с запасом margin (по любой из осей), а не дальше радиуса ortho + 8:
        // круговой деспавн по радиусу на широких экранах откладывал исчезновение
        // «летящих мимо» астероидов, пока они не улетят на 20+ юнитов вбок.
        var cam = CameraFollow.Instance;
        if (cam != null && ScreenBounds.IsOutsideFrame(
                cam.GetComponent<Camera>(), transform.position, config.asteroidDespawnMargin))
        {
            GameEvents.RaiseAsteroidDespawned(transform.position);
            Release();
            return;
        }

        // Движение + вращение
        transform.position += (Vector3)(_dir * _speed * Time.deltaTime);
        transform.Rotate(0f, 0f, _angularSpeed * Time.deltaTime);

        // Вспышка белым (1 кадр) и тряска позиции (±0.05 на ~2 кадра)
        if (_flashTimer > 0f)
        {
            _flashTimer -= Time.deltaTime;
            MaterialProvider.SetColor(_mr, Color.white);
        }
        else if (_shakeTimer > 0f)
        {
            _shakeTimer -= Time.deltaTime;
            Vector2 jitter = Random.insideUnitCircle * config.hitShakeAmplitude;
            transform.position += new Vector3(jitter.x, jitter.y, 0f);
            MaterialProvider.SetColor(_mr, Color.white);
        }
        else
        {
            MaterialProvider.SetColor(_mr, _restingColor);
        }
    }
}
