using UnityEngine;

/// <summary>
/// Ракета (GDD §4.4): ромб 0.3×0.7, красный, наведение с ограниченной угловой
/// скоростью (всегда ниже игрока), тайм-аут 12 с (последние 2 с мигает быстрее),
/// комбо с астероидом +200. Rigidbody2D Kinematic.
/// </summary>
public class Missile : Poolable
{
    [SerializeField] private GameConfig config;

    private int _hp;
    private float _speed;
    private float _turnRate;
    private float _life;
    private Rigidbody2D _rb;
    private MeshRenderer _mr;
    private bool _active;
    private bool _restoreColorPending;

    public void Init(GameConfig cfg)
    {
        config = cfg;
        _rb = GetComponent<Rigidbody2D>();
        _mr = GetComponent<MeshRenderer>();
    }

    public void Spawn(Vector3 pos, float speed, float turnRate, float life)
    {
        transform.position = pos;
        _speed = speed;
        _turnRate = turnRate;
        _life = life;
        _hp = config.missileHp;
        _active = true;
        if (ShipController.Instance != null)
            transform.up = ((Vector2)(ShipController.Instance.transform.position - pos)).normalized;
        var trail = GetComponent<TrailRenderer>();
        if (trail != null) trail.Clear();
        MaterialProvider.SetColor(_mr, Palette.Missile);
        _mr.enabled = true;
    }

    private void Update()
    {
        if (!_active) return;
        _life -= Time.deltaTime;

        if (_life <= 0f)
        {
            // Тайм-аут 12 с — самоуничтожение (GDD §4.4)
            _active = false;
            GameEvents.RaiseMissileTimeout(transform.position);
            Release();
            return;
        }

        // Последние 2 сек — мигает быстрее (0.3/0.2 → 0.1/0.1)
        if (_life < 2f)
            _mr.enabled = Mathf.Sin(Time.time * 20f) > 0f;
        else if (_restoreColorPending)
        {
            _restoreColorPending = false;
            MaterialProvider.SetColor(_mr, Palette.Missile);
        }

        // Наведение: поворот forward в сторону корабля с лимитом угловой скорости
        if (ShipController.Instance != null && !ShipController.Instance.IsDead)
        {
            Vector2 toShip = (Vector2)(ShipController.Instance.transform.position - transform.position);
            float targetAngle = Mathf.Atan2(toShip.y, toShip.x) * Mathf.Rad2Deg - 90f; // forward = +Y
            float current = transform.eulerAngles.z;
            float delta = Mathf.DeltaAngle(current, targetAngle);
            float maxTurn = _turnRate * Time.deltaTime;
            transform.rotation = Quaternion.Euler(0f, 0f, current + Mathf.Clamp(delta, -maxTurn, maxTurn));
        }

        _rb.linearVelocity = (Vector2)transform.up * _speed;
    }

    /// <summary>Попадание снаряда. Возвращает true, если ракета уничтожена.</summary>
    public bool Hit(int damage, Vector3 hitPoint)
    {
        if (!_active) return false;
        _hp -= damage;
        if (_hp <= 0)
        {
            _active = false;
            GameEvents.RaiseMissileDestroyed(transform.position);
            Release();
            return true;
        }
        // Вспышка белым на мгновение
        MaterialProvider.SetColor(_mr, Color.white);
        _restoreColorPending = true;
        AudioManager.Instance?.PlayHit();
        return false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_active) return;
        // Компонентная проверка вместо тега (надёжнее: тег может потеряться).
        var ast = other.GetComponentInParent<Asteroid>();
        if (ast != null)
        {
            _active = false;
            // Комбо: ракета + астероид уничтожены оба, крупный взрыв, +200 (GDD §4.4)
            GameEvents.RaiseCombo(transform.position);
            ast.Release();
            Release();
        }
    }
}
