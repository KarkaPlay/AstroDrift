using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Пикапы (GDD §4.6/§6): дроп из убитых врагов (один ролл на таблицу дропа × pickupDropMultiplier
/// фазы), гейт PilotProgressManager.IsUnlocked (двойной гейт §15.4), максимум 4 на земле,
/// максимум maxActiveEffects временных эффектов (новый заменяет самый старый), Shield — стак 1.
/// Эффекты: Rapid Fire = fireInterval ×0.5; Spread Shot = 3 снаряда ±15°; Shield = 1 удар.
/// </summary>
public class PickupManager : MonoBehaviour
{
    public static PickupManager Instance { get; private set; }

    [SerializeField] private PickupConfig config;
    [SerializeField] private DifficultyConfig difficulty;

    private ObjectPool _pool;
    private readonly List<Pickup> _onGround = new List<Pickup>();
    private readonly List<(PickupType type, float timeLeft)> _effects = new List<(PickupType, float)>();

    public bool HasShield { get; private set; } // механический щит (пикап или rewarded-стартовый, §4.6)

    public void Init(PickupConfig cfg, DifficultyConfig diff, Transform poolParent)
    {
        config = cfg;
        difficulty = diff;
        _pool = new ObjectPool(CreatePickup, poolParent, 6); // лимит 4 на земле + запас
        GameEvents.AsteroidDestroyed += OnAsteroidDestroyed;
        GameEvents.MissileDestroyed += OnMissileDestroyed;
        ResetRun();
    }

    private void Awake() => Instance = this;
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        GameEvents.AsteroidDestroyed -= OnAsteroidDestroyed;
        GameEvents.MissileDestroyed -= OnMissileDestroyed;
    }

    private Poolable CreatePickup()
    {
        var go = new GameObject("Pickup");
        go.transform.SetParent(transform);
        var pickup = go.AddComponent<Pickup>();
        pickup.Init(config);
        return pickup;
    }

    public void ResetRun()
    {
        _pool.ReleaseAll();
        _onGround.Clear();
        _effects.Clear();
        HasShield = false;
    }

    /// <summary>Rewarded-стартовый щит (§4.2): тот же механический щит, что пикап Shield.</summary>
    public void GrantStartShield() => HasShield = true;

    // ——— Дроп: один ролл на таблицу дропа убитого врага (GDD §4.6) ———

    private void OnAsteroidDestroyed(Vector3 pos, AsteroidSize size)
    {
        if (size == AsteroidSize.Small) return; // только средний/крупный
        foreach (var def in config.pickups)
        {
            float chance = def.dropChanceAsteroid * DropMultiplier();
            if (def.type == PickupType.Shield) continue; // Shield только из ракет
            if (TryRoll(def, chance, pos)) return;       // один ролл на врага
        }
    }

    private void OnMissileDestroyed(Vector3 pos)
    {
        foreach (var def in config.pickups)
        {
            if (def.type != PickupType.Shield) continue;
            if (TryRoll(def, def.dropChanceMissile * DropMultiplier(), pos)) return;
        }
    }

    /// <summary>Множитель дропа по фазе (DifficultyConfig.pickupDropMultiplier, GDD §6).</summary>
    private float DropMultiplier()
        => difficulty != null && DifficultyManager.Instance != null
            ? difficulty.PickupDropMultiplierAt(DifficultyManager.Instance.Elapsed)
            : 1f;

    private bool TryRoll(PickupDef def, float chance, Vector3 pos)
    {
        bool hit = Random.value < chance;
#if UNITY_EDITOR
        // Инструментация плейтеста Волны 1 (баг 3): шанс vs результат на каждом ролле.
        Debug.Log($"[PickupDebug] roll {def.type}: chance={chance:0.###} → {(hit ? "DROP" : "miss")}");
#endif
        if (!hit) return false;
        // Гейт доступности (§15.4.3): PickupManager проверяет через PilotProgressManager
        var pilot = PilotProgressManager.Instance;
        bool unlocked = def.unlockedByDefault || (pilot != null && pilot.IsUnlocked(def.type.ToString()));
#if UNITY_EDITOR
        if (!unlocked)
            Debug.Log($"[PickupDebug] {def.type}: заблокирован гейтом (pilotLevel={pilot?.PilotLevel ?? -1}, unlockedByDefault={def.unlockedByDefault})");
#endif
        if (!unlocked) return false;
        // Shield: стак максимум 1 — пока щит активен, второй не выпадает (§4.6)
        if (def.type == PickupType.Shield && HasShield) return false;
        TrySpawnPickup(def, pos);
        return true;
    }

    /// <summary>Лимит 4 на земле: спавнер ждёт деспавна (GDD §6).</summary>
    private void TrySpawnPickup(PickupDef def, Vector3 pos)
    {
        if (_onGround.Count >= config.maxPickupsOnGround)
        {
#if UNITY_EDITOR
            Debug.Log($"[PickupDebug] {def.type}: лимит на земле {_onGround.Count}/{config.maxPickupsOnGround} — спавн пропущен");
#endif
            return;
        }
        var pickup = _pool.Get() as Pickup;
        if (pickup == null) return;
        pickup.SpawnAt(def, pos);
        _onGround.Add(pickup);
#if UNITY_EDITOR
        Debug.Log($"[PickupDebug] spawn {def.type} at {pos} (onGround={_onGround.Count})");
#endif
    }

    private void LateUpdate()
    {
        // Чистим список от вернувшихся в пул (истёкшие/подобранные)
        for (int i = _onGround.Count - 1; i >= 0; i--)
            if (_onGround[i] == null || !_onGround[i].gameObject.activeInHierarchy)
                _onGround.RemoveAt(i);

        // Временные эффекты тикают только в Playing (фриз/пауза стоят — timeScale=0 останавливает ниже)
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing) return;

        for (int i = _effects.Count - 1; i >= 0; i--)
        {
            var e = _effects[i];
            e.timeLeft -= Time.deltaTime;
            if (e.timeLeft <= 0f)
            {
                ApplyEffectState(e.type, false);
                _effects.RemoveAt(i);
            }
            else _effects[i] = e;
        }
    }

    // ——— Подбор (§4.6): эффект + juice ———

    public void OnPickedUp(Pickup pickup)
    {
        var def = pickup.Def;
        if (def == null) return;

        // Juice: вспышка цвета пикапа кольцом вокруг корабля + floating text + звук 7
        var ship = ShipController.Instance;
        Vector3 pos = ship != null ? ship.transform.position : pickup.transform.position;
        if (ParticlePool.Instance != null)
            ParticlePool.Instance.Burst(pos, def.color, 8, 1.2f, 2.5f, 0.5f, 0.15f, 0.3f);
        if (FloatingTextPool.Instance != null)
        {
            string name = def.name.GetLocalizedString(); // синхронное чтение (§3.3); "" — проверка N3
            FloatingTextPool.Instance.Spawn(pos + Vector3.up * 1.0f,
                string.IsNullOrEmpty(name) ? def.type.ToString() : name, def.color, 3.8f, 0.9f);
        }
        AudioManager.Instance?.PlayPickup();

        if (def.type == PickupType.Shield)
        {
            HasShield = true; // до использования, стак 1
            return;
        }

        // Не более maxActiveEffects временных эффектов; новый заменяет самый старый (§4.6)
        int limit = Mathf.Max(1, config.maxActiveEffects);
        if (_effects.Count >= limit)
        {
            var oldest = _effects[0];
            ApplyEffectState(oldest.type, false);
            _effects.RemoveAt(0);
        }
        // Повторный подбор того же типа продлевает (перезапись таймера)
        for (int i = 0; i < _effects.Count; i++)
        {
            if (_effects[i].type == def.type)
            {
                _effects[i] = (def.type, def.duration);
                ApplyEffectState(def.type, true);
                return;
            }
        }
        _effects.Add((def.type, def.duration));
        ApplyEffectState(def.type, true);
    }

    /// <summary>Поглощение удара щитом: true — удар поглощён (корабль не умирает).</summary>
    public bool TryAbsorbHitWithShield()
    {
        if (!HasShield) return false;
        HasShield = false; // щит ломается (стак 1)
        var ship = ShipController.Instance;
        if (ship != null && ParticlePool.Instance != null)
            ParticlePool.Instance.Burst(ship.transform.position, Palette.PickupShield, 10, 1.5f, 3f, 0.5f, 0.12f, 0.25f);
        return true;
    }

    private void ApplyEffectState(PickupType type, bool active)
    {
        var weapon = ShipController.Instance != null ? ShipController.Instance.GetComponent<ShipWeapon>() : null;
        if (weapon == null) return;
        switch (type)
        {
            case PickupType.RapidFire:
                weapon.SetRapidFire(active);
                break;
            case PickupType.SpreadShot:
                weapon.SetSpread(active ? 15f : 0f); // веер ±15° (GDD §4.6)
                break;
        }
    }
}
