using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Предупреждение о ракете (GDD §4.4 / VisualStyle §6.6; ТЗ v3 Доработка 2; UX5.7/UX5.8):
/// красный мигающий треугольник НА РАДИУСЕ от корабля (GameConfig.warningOrbitRadius,
/// не край экрана), вершина в направлении угрозы (спавн / текущая позиция ракеты —
/// индикатор сопровождает летящую ракету, пока та вне кадра).
/// UX5.8: индикаторов СТОЛЬКО ЖЕ, сколько внеэкранных ракет — каждый экземпляр берётся
/// из пула (ObjectPool, прогрев по maxMissiles) и строго привязан к СВОЕЙ ракете:
/// ракета вошла в кадр / умерла / мир сброшен (рестарт, continue-очистка, Home) —
/// индикатор мгновенно возвращается в пул. Никаких «осиротевших» индикаторов.
/// Тайминги предупреждений (missileWarnTime, мигание 0.3/0.2) не менялись.
/// </summary>
public class MissileWarning : Poolable
{
    /// <summary>UX5.8: ширина треугольника ×0.7 (высота не тронута).</summary>
    public const float WidthScale = 0.7f;

    private Camera _cam;
    private GameConfig _config;
    private Missile _target;
    private float _life; // max-время существования (страховка по ТЗ)
    private float _blinkTimer;
    private bool _visible;
    private MeshRenderer _mr;

    // Реестр живых предупреждений + карта «ракета → её индикатор»
    // (переиспользование ракеты из пула гасит старый индикатор до ShowFor нового).
    private static readonly List<MissileWarning> _live = new List<MissileWarning>();
    private static readonly Dictionary<Missile, MissileWarning> _byMissile =
        new Dictionary<Missile, MissileWarning>();

    /// <summary>Фабрика для пула (UX5.8): строит структуру объекта. Вызывается при прогреве пула.</summary>
    public static MissileWarning Create(Camera cam, GameConfig config, Transform homeParent)
    {
        var go = new GameObject("MissileWarning");
        if (homeParent != null) go.transform.SetParent(homeParent, false);
        var mf = go.AddComponent<MeshFilter>();
        // UX5.8: ширина ×0.7 (0.5 → 0.35), высота 0.5 без изменений
        mf.sharedMesh = GeometryFactory.Triangle(0.5f * WidthScale, 0.5f);
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = MaterialProvider.Shared;
        MaterialProvider.SetColor(mr, Palette.WarningRed);
        var warn = go.AddComponent<MissileWarning>();
        warn._cam = cam;
        warn._config = config;
        warn._mr = mr;
        return warn;
    }

    /// <summary>Привязывает индикатор к ракете (UX5.8: один индикатор на ракету).</summary>
    public void ShowFor(Vector3 spawnPos, Missile target)
    {
        _target = target;
        _life = _config.missileWarnTime;
        _blinkTimer = 0f;
        _visible = false; // первый тогл мигания → ВКЛ (тайминги как раньше: 0.3 вкл / 0.2 выкл)
        if (_mr != null) _mr.enabled = false;
        UpdateOrbit(spawnPos);
        if (!_live.Contains(this)) _live.Add(this);
        _byMissile[target] = this;
    }

    /// <summary>Гасит индикатор конкретной ракеты (ракета умерла / переиспользована пулом).</summary>
    public static void HideFor(Missile target)
    {
        if (target != null && _byMissile.TryGetValue(target, out var warn) && warn != null)
            warn.ReleaseToPool();
    }

    /// <summary>Гасит ВСЕ живые индикаторы (сброс мира: рестарт / continue / Home).</summary>
    public static void HideAll()
    {
        for (int i = _live.Count - 1; i >= 0; i--)
            if (_live[i] != null) _live[i].ReleaseToPool();
        _live.Clear();
        _byMissile.Clear();
    }

    private void ReleaseToPool()
    {
        if (_target != null && _byMissile.TryGetValue(_target, out var w) && w == this)
            _byMissile.Remove(_target);
        _target = null;
        _live.Remove(this);
        if (Pool != null) Release();
        else Destroy(gameObject); // страховка: индикатор вне пула
    }

    /// <summary>Массовое гашение (ObjectPool.ReleaseAll при рестарте): чистим реестры.</summary>
    public override void OnPoolReleaseAll()
    {
        if (_target != null && _byMissile.TryGetValue(_target, out var w) && w == this)
            _byMissile.Remove(_target);
        _target = null;
        _live.Remove(this);
    }

    private void OnDestroy()
    {
        _live.Remove(this);
        if (_target != null && _byMissile.TryGetValue(_target, out var w) && w == this)
            _byMissile.Remove(_target);
    }

    /// <summary>
    /// Орбита вокруг корабля (UX5.7): центр = позиция корабля,
    /// радиус = GameConfig.warningOrbitRadius. Адаптив (любой аспект): точка орбиты
    /// клампится в видимый кадр (pad 0.5 ю) — на квадратных/альбомных экранах и
    /// при малых значениях радиуса индикатор гарантированно остаётся в кадре.
    /// Направление и поворот — на угрозу (спавн-точка, затем летящая ракета).
    /// </summary>
    private void UpdateOrbit(Vector3 missilePos)
    {
        Vector2 shipPos = ShipController.Instance != null
            ? (Vector2)ShipController.Instance.transform.position
            : (Vector2)_cam.transform.position;
        Vector2 dir = ((Vector2)missilePos - shipPos).normalized;
        if (dir == Vector2.zero) dir = Vector2.up;

        Vector2 p = shipPos + dir * _config.warningOrbitRadius;
        p = ScreenBounds.ClampToFrame(_cam, p, 0.5f);

        transform.position = new Vector3(p.x, p.y, 0f);
        // Вершина в сторону угрозы (угол от корабля к ракете) — семантика сохранена
        float angle = Mathf.Atan2(missilePos.y - shipPos.y, missilePos.x - shipPos.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
    }

    private void Update()
    {
        if (_target == null || _target.gameObject == null || !_target.gameObject.activeSelf)
        {
            ReleaseToPool();
            return;
        }
        _life -= Time.deltaTime;
        if (_life <= 0f)
        {
            ReleaseToPool();
            return;
        }

        // ТЗ v3: ракета видна в кадре → индикатор исчезает НАВСЕГДА (не мигание)
        Vector3 vp = _cam.WorldToViewportPoint(_target.transform.position);
        bool missileVisible = vp.z > 0f && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f;
        if (missileVisible)
        {
            ReleaseToPool();
            return;
        }

        // Каждый кадр: позиция на орбите вокруг корабля, в направлении ракеты
        UpdateOrbit(_target.transform.position);

        // Мигание: 0.3 вкл / 0.2 выкл (GDD §8)
        _blinkTimer -= Time.deltaTime;
        if (_blinkTimer <= 0f)
        {
            _visible = !_visible;
            _blinkTimer = _visible ? _config.missileWarnBlinkOn : _config.missileWarnBlinkOff;
            if (_mr != null) _mr.enabled = _visible;
        }
    }
}
