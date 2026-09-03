using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Камерная хореография «Menu & Transitions v2» (ArtDirection §2, §4–§6).
///
/// • Меню-кадр (§2): корабль в игровой стартовой позиции, орто = игровой / menuZoomDivisor
///   (2–2.5× крупнее), композер держит корабль в нижней трети (Composition.ScreenPosition).
/// • Полёты (§5/§6/§4.5) — вручную: CinemachineBrain отключается, камера и орто анимируются
///   по EaseOutSoft в unscaled time; на завершение Brain включается ровно в том же кадре
///   (lens + ForceCameraPosition + сброс кэша цели) — ни одного телепорта на глазах.
/// • Конец каждого полёта считается LIVE по позиции цели: если управление разблокировалось
///   на середине полёта (t = 1.6 s при 2.0 s зума), кадр сходится к кораблю без скачка.
/// • Все длительности — в GameConfig («Menu & Transitions v2»), чтобы тюнить без кода.
/// </summary>
public class CameraDirector : MonoBehaviour
{
    private GameConfig _cfg;
    private Camera _cam;
    private CameraFollow _follow;
    private CinemachineBrain _brain;
    private float _gameOrtho;
    private float _menuOrtho;
    private float _gameScreenY = 0.05f; // композиция «корабль в нижней трети» (геймплей)
    private float _menuScreenY = 0.11f; // композиция меню: корабль ~60 % высоты, между Best и CTA (§2)

    public void Init(GameConfig cfg, Camera cam, CameraFollow follow)
    {
        _cfg = cfg;
        _cam = cam;
        _follow = follow;
        if (_cam != null) _brain = _cam.GetComponent<CinemachineBrain>();
        _gameOrtho = _cam != null ? _cam.orthographicSize : 12f;
        float div = cfg != null ? Mathf.Max(1.05f, cfg.menuZoomDivisor) : 2.25f;
        _menuOrtho = _gameOrtho / div;
        if (cfg != null) _menuScreenY = cfg.menuShipScreenY;
    }

    /// <summary>Композиция кадра: где корабль сидит по вертикали (ScreenPosition.y композера).</summary>
    private void SetComposerY(float y)
    {
        if (_follow != null) _follow.SetComposerScreenY(y);
    }

    public float GameOrtho => _gameOrtho;
    public float MenuOrtho => _menuOrtho;

    /// <summary>Мгновенный вход в меню-кадр (инициализация сцены / первый кадр игры).</summary>
    public void SnapMenu()
    {
        if (_follow == null || _follow.FollowTarget == null) return;
        SetComposerY(_menuScreenY);
        _follow.SetLensOrtho(_menuOrtho);
        _follow.ForceFrame(_menuOrtho);
        if (_brain != null) _brain.enabled = true;
    }

    /// <summary>§5: отъезд «меню → игровой зум», startFlyDuration (2.0 s) EaseOutSoft.
    /// Композиция плавно возвращается к игровой (корабль в нижней трети).</summary>
    public IEnumerator FlyToGameRoutine(float duration)
    {
        SetComposerY(_gameScreenY);
        return FlyRoutine(() => TargetFrame(_gameOrtho), _gameOrtho, duration);
    }

    /// <summary>§4.5: возвращение в меню-кадр, homeFlyDuration (0.8 s) EaseOutSoft.</summary>
    public IEnumerator FlyToMenuRoutine(float duration)
    {
        SetComposerY(_menuScreenY);
        return FlyRoutine(() => TargetFrame(_menuOrtho), _menuOrtho, duration);
    }

    /// <summary>
    /// §6.1: перелёт «место смерти (zoom-in +15 %) → стартовый кадр (меню-зум)»
    /// за restartFlyDuration (0.6 s) EaseOutSoft, затем дожим зума «меню → игра»
    /// за restartZoomOutDuration при включённом Brain (композер ведёт уже движущийся корабль).
    /// </summary>
    public IEnumerator FlyRestartRoutine(float flightDuration, float zoomOutDuration)
    {
        SetComposerY(_menuScreenY); // стартовый кадр = меню-кадр (§6.1: визуальная рифма)
        yield return FlyRoutine(() => TargetFrame(_menuOrtho), _menuOrtho, flightDuration);

        float from = _cam.orthographicSize;
        float t = 0f;
        while (t < zoomOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = UiAnim.EaseOutSoft.Evaluate(Mathf.Clamp01(t / zoomOutDuration));
            _follow.SetLensOrtho(Mathf.LerpUnclamped(from, _gameOrtho, k));
            yield return null;
        }
        _follow.SetLensOrtho(_gameOrtho);
    }

    /// <summary>
    /// Continue (GDD_DeathScreen_Continue §5.2/§10.3): zoom-out «кадр смерти → игровой»
    /// НА МЕСТЕ — позиция камеры не летит, только орто растёт до игрового, кадр
    /// «открывается» вокруг корабля (якорь композиции живёт на FollowTarget — до
    /// разблокировки это место смерти). В конце Brain включается ровно в том же кадре:
    /// ForceFrame(gameOrtho) даёт ту же позицию, что последний кадр цикла — без скачка.
    /// </summary>
    public IEnumerator ContinueZoomOutRoutine(float duration)
    {
        if (_cam == null || _follow == null || duration <= 0f) yield break;
        if (_brain != null) _brain.enabled = false;

        float startOrtho = _cam.orthographicSize;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = UiAnim.EaseOutSoft.Evaluate(Mathf.Clamp01(t / duration));
            float ortho = Mathf.LerpUnclamped(startOrtho, _gameOrtho, k);
            _cam.orthographicSize = ortho;
            Vector3 anchor = _follow.FollowTarget != null ? _follow.FollowTarget.position : Vector3.zero;
            _cam.transform.position = _follow.FramePosFor(anchor, ortho);
            yield return null;
        }
        _follow.SetLensOrtho(_gameOrtho);
        _follow.ForceFrame(_gameOrtho);
        if (_brain != null) _brain.enabled = true;
    }

    /// <summary>§4.2: лёгкий zoom-in к месту смерти (+15 % зума), без последующего движения.</summary>
    public void DeathZoomIn(Vector3 deathPos)
    {
        StopCoroutine(nameof(DeathZoomRoutine));
        StartCoroutine(DeathZoomRoutine(deathPos));
    }

    private IEnumerator DeathZoomRoutine(Vector3 deathPos)
    {
        yield return new WaitForSecondsRealtime(_cfg != null ? _cfg.deathZoomInDelay : 0.25f);
        float factor = _cfg != null ? Mathf.Max(1f, _cfg.deathZoomInFactor) : 1.15f;
        float ortho = _gameOrtho / factor;
        float dur = _cfg != null ? _cfg.deathZoomInDuration : 0.4f;
        yield return FlyRoutine(() => _follow.FramePosFor(deathPos, ortho), ortho, dur);
    }

    // ——— Ядро: ручной пролёт с выключенным Brain, возврат в Cinemachine без скачка ———

    private IEnumerator FlyRoutine(Func<Vector3> endFramePos, float endOrtho, float duration)
    {
        if (_cam == null || _follow == null || duration <= 0f) yield break;
        if (_brain != null) _brain.enabled = false;

        Transform follow = _follow.FollowTarget;
        Vector3 targetPosAtStart = follow != null ? follow.position : Vector3.zero;

        Vector3 startPos = _cam.transform.position;
        float startOrtho = _cam.orthographicSize;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = UiAnim.EaseOutSoft.Evaluate(Mathf.Clamp01(t / duration));
            // Конец кадра считаем live: цель могла сместиться (unlock на середине полёта).
            _cam.transform.position = Vector3.LerpUnclamped(startPos, endFramePos(), k);
            _cam.orthographicSize = Mathf.LerpUnclamped(startOrtho, endOrtho, k);
            yield return null;
        }
        _cam.transform.position = endFramePos();
        _cam.orthographicSize = endOrtho;

        // Возврат в Cinemachine ровно в текущем кадре: lens = текущий орто,
        // кэш цели сбрасываем штатным OnTargetObjectWarped (корабль телепортировался
        // во время полёта — §6.1), позиция фиксируется ForceCameraPosition.
        _follow.SetLensOrtho(endOrtho);
        if (follow != null) _follow.NotifyTargetWarped(follow.position - targetPosAtStart);
        _follow.ForceFrame(endOrtho);
        if (_brain != null) _brain.enabled = true;
    }

    /// <summary>Кадр «камера смотрит на цель с орто ortho» — та же математика, что SnapToShip.</summary>
    private Vector3 TargetFrame(float ortho)
    {
        Transform follow = _follow != null ? _follow.FollowTarget : null;
        Vector3 anchor = follow != null ? follow.position : Vector3.zero;
        return _follow.FramePosFor(anchor, ortho);
    }
}
