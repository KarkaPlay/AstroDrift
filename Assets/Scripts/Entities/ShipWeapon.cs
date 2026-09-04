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
        col.radius = config.bulletRadius;
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
            _cooldown = config.fireInterval;
        }
    }

    private void Fire()
    {
        var bullet = _pool.Get() as Bullet;
        Vector2 dir = _ship.transform.up;
        bullet.Spawn(_ship.transform.position + (Vector3)(dir * 0.35f), dir, config.bulletSpeed, config.bulletLife);
        AudioManager.Instance?.PlayShot();

        // Микро-откат корабля назад (GDD §8)
        _ship.transform.position -= (Vector3)(dir * config.bulletKickback);
    }

    /// <summary>Перезапуск забега: гасим активные пули (иначе улетают в бесконечность), сброс кулдауна.</summary>
    public void ResetWeapon()
    {
        _pool.ReleaseAll();
        _cooldown = 0f;
    }
}
