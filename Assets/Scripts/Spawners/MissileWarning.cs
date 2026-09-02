using UnityEngine;

/// <summary>
/// Предупреждение о ракете (GDD §4.4 / VisualStyle §6.6; ТЗ v3 Доработка 2):
/// красный мигающий треугольник на орбите вокруг КОРАБЛЯ (привязан к нему,
/// двигается вместе с ним), радиус 0.55·ortho, вершина в направлении ракеты.
/// Исчезает НАВСЕГДА, как только ракета входит в кадр (WorldToViewportPoint).
/// Лимит missileWarnTime — max-время существования (страховка).
/// </summary>
public class MissileWarning : MonoBehaviour
{
    private Camera _cam;
    private GameConfig _config;
    private Missile _target;
    private float _life; // max-время существования (страховка по ТЗ)
    private float _blinkTimer;
    private bool _visible;
    private MeshRenderer _mr;

    // Реестр активных предупреждений: рестарт гасит их разом (HideAll)
    private static readonly System.Collections.Generic.List<MissileWarning> _live =
        new System.Collections.Generic.List<MissileWarning>();

    public static void Create(Camera cam, GameConfig config, Vector3 spawnPos, Missile target)
    {
        var go = new GameObject("MissileWarning");
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = GeometryFactory.Triangle(0.5f, 0.5f);
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = MaterialProvider.Shared;
        MaterialProvider.SetColor(mr, Palette.WarningRed);
        var warn = go.AddComponent<MissileWarning>();
        warn._cam = cam;
        warn._config = config;
        warn._target = target;
        warn._mr = mr;
        warn._life = config.missileWarnTime;
        warn.UpdateOrbit(spawnPos);
        _live.Add(warn);
    }

    /// <summary>Гасит все активные предупреждения (рестарт забега) — без событий/juice.</summary>
    public static void HideAll()
    {
        for (int i = _live.Count - 1; i >= 0; i--)
        {
            if (_live[i] != null) Destroy(_live[i].gameObject);
        }
        _live.Clear();
    }

    public void ShowFor(Vector3 spawnPos, Missile target)
    {
        _target = target;
        _life = _config.missileWarnTime;
        UpdateOrbit(spawnPos);
        _blinkTimer = 0f;
    }

    /// <summary>
    /// Орбита вокруг корабля (ТЗ v3): центр = позиция корабля, радиус = 0.55·ortho,
    /// направление на ракету. Кламп в кадр (0.85·полуразмер) — на случай корабля у края.
    /// </summary>
    private void UpdateOrbit(Vector3 missilePos)
    {
        Vector2 shipPos = ShipController.Instance != null
            ? (Vector2)ShipController.Instance.transform.position
            : (Vector2)_cam.transform.position;
        Vector2 dir = ((Vector2)missilePos - shipPos).normalized;
        if (dir == Vector2.zero) dir = Vector2.up;

        float ortho = _cam.orthographicSize;
        float halfW = ortho * _cam.aspect;
        float radius = ortho * 0.55f;

        Vector2 p = shipPos + dir * radius;

        // Кламп в кадр с запасом 10% от краёв (0.85·полуразмер)
        p.x = Mathf.Clamp(p.x, _cam.transform.position.x - halfW * 0.85f, _cam.transform.position.x + halfW * 0.85f);
        p.y = Mathf.Clamp(p.y, _cam.transform.position.y - ortho * 0.85f, _cam.transform.position.y + ortho * 0.85f);

        transform.position = new Vector3(p.x, p.y, 0f);
        // Вершина в сторону ракеты (угол от корабля к ракете)
        float angle = Mathf.Atan2(missilePos.y - shipPos.y, missilePos.x - shipPos.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
    }

    private void OnDestroy()
    {
        _live.Remove(this);
    }

    private void Update()
    {
        if (_target == null || _target.gameObject == null || !_target.gameObject.activeSelf)
        {
            Destroy(gameObject);
            return;
        }
        _life -= Time.deltaTime;
        if (_life <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        // ТЗ v3: ракета видна в кадре → индикатор исчезает НАВСЕГДА (не мигание)
        Vector3 vp = _cam.WorldToViewportPoint(_target.transform.position);
        bool missileVisible = vp.z > 0f && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f;
        if (missileVisible)
        {
            Destroy(gameObject);
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
