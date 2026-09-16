using UnityEngine;

/// <summary>
/// Автострельба (GDD §4.2): снаряд летит по направлению носа в момент выстрела,
/// траекторию не корректирует. Пул обязателен (макс. 15). Микро-откат корабля.
/// </summary>
public class ShipWeapon : MonoBehaviour
{
    [SerializeField] private GameConfig config;
    [SerializeField] private GameObject bulletPrefab; // Assets/Resources/Prefabs/Bullet.prefab

    private ObjectPool _pool;
    private float _cooldown;
    private ShipController _ship;
    private Transform _poolParent;
    private float _spread; // пикап Spread Shot (§4.6): угол веера, 0 = выключен
    private bool _rapidFire; // пикап Rapid Fire (§4.6): fireInterval ×0.5

    public void Init(GameConfig cfg, ShipController ship, Transform poolParent)
    {
        config = cfg;
        _ship = ship;
        _poolParent = poolParent;

        // Префаб — источник структуры; радиус пули для коллайдера переустанавливаем
        // из конфига (числа — из GameConfig, а не из префаба).
        if (bulletPrefab == null)
            bulletPrefab = Resources.Load<GameObject>("Prefabs/Bullet");

        _pool = new ObjectPool(Create, poolParent, 15);
    }

    private Poolable Create()
    {
        var go = Instantiate(bulletPrefab, _poolParent);
        go.name = "Bullet";
        var col = go.GetComponent<CircleCollider2D>();
        col.radius = PerkManager.Instance != null ? PerkManager.Instance.BulletRadius : config.bulletRadius;
        return go.GetComponent<Bullet>();
    }

    private void Update()
    {
        if (_ship == null || _pool == null || _ship.IsDead) return;
        var gm = GameManager.Instance;
        // WeaponEnabled (старт §5): стрельба — на t = startSystemsTime, когда включаются
        // все системы (спавн угроз, HUD). Управление при этом уже с t = 0.
        if (gm == null || gm.State != GameState.Playing || !gm.WeaponEnabled) return;

        _cooldown -= Time.deltaTime;
        if (_cooldown <= 0f)
        {
            Fire();
            var pk = PerkManager.Instance;
            float interval = pk != null ? pk.FireInterval : config.fireInterval;
            _cooldown = _rapidFire ? interval * 0.5f : interval;
        }
    }

    private void Fire()
    {
        var perks = PerkManager.Instance;
        float speed = perks != null ? perks.BulletSpeed : config.bulletSpeed;
        int pierce = perks != null ? perks.PierceCount : 0;

        // Spread Shot (пикап §4.6): 3 снаряда веером ±15°; иначе одиночный
        float[] angles = _spread > 0f ? new[] { 0f, _spread, -_spread } : new[] { 0f };
        Vector2 baseDir = _ship.transform.up;
        for (int i = 0; i < angles.Length; i++)
        {
            var bullet = _pool.Get() as Bullet;
            Vector2 dir = Rotate(baseDir, angles[i]);
            bullet.Spawn(_ship.transform.position + (Vector3)(baseDir * 0.35f), dir, speed, config.bulletLife, pierce);
        }
        AudioManager.Instance?.PlayShot();

        // Микро-откат корабля назад (GDD §8)
        _ship.transform.position -= (Vector3)(baseDir * config.bulletKickback);
    }

    private static Vector2 Rotate(Vector2 v, float deg)
    {
        float r = deg * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    /// <summary>Пикап Spread Shot (§4.6): 3 снаряда веером ±spreadDeg. 0 = одиночный выстрел.</summary>
    public void SetSpread(float spreadDeg) => _spread = spreadDeg;

    /// <summary>Пикап Rapid Fire (§4.6): интервал стрельбы ×0.5. Оптимизация поверх перков FireRate.</summary>
    public void SetRapidFire(bool active) => _rapidFire = active;

    /// <summary>Перезапуск забега: гасим активные пули (иначе улетают в бесконечность), сброс кулдауна.</summary>
    public void ResetWeapon()
    {
        _pool.ReleaseAll();
        _cooldown = 0f;
        _spread = 0f;
    }
}
