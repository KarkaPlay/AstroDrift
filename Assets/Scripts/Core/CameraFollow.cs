using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Камера-следование (GDD §3) — стандартный Cinemachine 3 (Unity 6, CM3):
/// отдельный vcam-объект (CinemachineCamera + CinemachinePositionComposer +
/// CinemachineImpulseListener), CinemachineBrain + CinemachineImpulseSource на
/// Main Camera. ВСЁ сериализовано в сцене Game.unity — код только связывает.
///
/// • Follow = корабль (SetTarget из GameManager.BeginRun); пока забег не идёт,
///   Follow пуст — это штатно, композер показывает предупреждение «Tracking
///   Target is required», которое исчезает на старте забега.
/// • Framing «корабль в нижней трети» — Composition.ScreenPosition (0, +0.2)
///   (CM3: 0 = центр экрана, ±0.5 = край; ось Y экранная, +вниз; 0.2 = 30% от низа)
///   CameraDistance = 10 → камера на z=-10, ортографический размер из GameConfig.
/// • Look-ahead — встроенный Lookahead композера (ведёт цель по её скорости,
///   т.е. по forward корабля): no ручных якорей и ручного SmoothDamp.
/// • Позицию камеры пишет ТОЛЬКО CinemachineBrain — один писатель, без
///   рассинхрона update-циклов (джиттер «на кадр» устранён стандартным пайплайном).
///   Исключение — CameraDirector: на время хореографических полётов (§5/§6)
///   Brain выключается, позицию пишет он; в конце полёта кадр фиксируется
///   через ForceFrame и Brain включается обратно в том же кадре.
/// • Shake — CinemachineImpulseSource на камере (см. CameraShake), офсет применяет
///   CinemachineImpulseListener на CM_VCam (в CM3 listener должен жить на vcam).
/// </summary>
public class CameraFollow : MonoBehaviour
{
    private Camera _cam;
    private CinemachineCamera _vcam;
    private CinemachinePositionComposer _composer;

    public static CameraFollow Instance { get; private set; }
    public float OrthoSize => _cam != null ? _cam.orthographicSize : 12f;

    private void Awake()
    {
        Instance = this;
        _cam = GetComponent<Camera>();
        BindVcam();
    }

    /// <summary>
    /// Все параметры Cinemachine выставлены вручную в сцене Game.unity
    /// (CM_VCam: CinemachineCamera + CinemachinePositionComposer +
    /// CinemachineImpulseListener; Main Camera: CinemachineBrain +
    /// CinemachineImpulseSource). Код ничего не настраивает и не перезаписывает —
    /// только связывает ссылки на vcam/composer для SetTarget/SnapToShip.
    /// </summary>
    private void BindVcam()
    {
        if (_vcam != null) return;
        var existing = FindFirstObjectByType<CinemachineCamera>();
        if (existing == null)
        {
            Debug.LogError("CameraFollow: в сцене нет объекта с CinemachineCamera " +
                           "(ожидался CM_VCam). Добавь его в сцену вручную.");
            return;
        }
        _vcam = existing;
        _composer = existing.GetComponent<CinemachinePositionComposer>();
        // Follow назначается ТОЛЬКО через SetTarget (GameManager.BeginRun).
        // Самоследование (Follow = камера) даёт расходящуюся петлю → NaN-позиция.
    }

    public void SetTarget(Transform t)
    {
        BindVcam(); // порядок Awake не гарантирован (Bootstrap может быть раньше) — ленивая привязка
        if (_vcam != null) _vcam.Follow = t;
    }

    /// <summary>Текущая цель композера (для CameraDirector).</summary>
    public Transform FollowTarget
    {
        get
        {
            BindVcam();
            return _vcam != null ? _vcam.Follow : null;
        }
    }

    /// <summary>Сменить орто-размер линзы vcam (меню-зум ↔ игровой, ArtDirection §2/§5).</summary>
    public void SetLensOrtho(float ortho)
    {
        BindVcam();
        if (_vcam == null) return;
        var lens = _vcam.Lens;
        lens.OrthographicSize = ortho;
        _vcam.Lens = lens;
    }

    /// <summary>
    /// Композиция по вертикали: где цель сидит в кадре (Composition.ScreenPosition.y).
    /// Меню ≈ 0.11 (корабль ~60 % высоты, между Best и CTA — §2), игра = 0.2 (нижняя треть).
    /// </summary>
    public void SetComposerScreenY(float y)
    {
        BindVcam();
        if (_composer == null) return;
        var comp = _composer.Composition;
        comp.ScreenPosition = new Vector3(comp.ScreenPosition.x, y, 0f);
        _composer.Composition = comp;
    }

    /// <summary>
    /// Зафиксировать камеру в кадре цели с заданным орто (вход/выход из ручного
    /// пролёта CameraDirector): та же математика, что SnapToShip.
    /// </summary>
    public void ForceFrame(float ortho)
    {
        BindVcam();
        if (_vcam == null || _vcam.Follow == null || _composer == null) return;
        Vector3 pos = FramePosFor(_vcam.Follow.position, ortho);
        _vcam.ForceCameraPosition(pos, Quaternion.identity);
    }

    /// <summary>Сброс кэша lookahead/дэмпинга после телепорта цели (§6.1, рестарт).</summary>
    public void NotifyTargetWarped(Vector3 delta)
    {
        BindVcam();
        if (_vcam == null || _vcam.Follow == null) return;
        _vcam.OnTargetObjectWarped(_vcam.Follow, delta);
    }

    /// <summary>
    /// Позиция камеры, при которой точка anchor лежит в целевой точке кадра
    /// (Composition.ScreenPosition) при орто-размере ortho. Общая математика
    /// для SnapToShip и CameraDirector.
    /// </summary>
    public Vector3 FramePosFor(Vector3 anchor, float ortho)
    {
        BindVcam();
        if (_composer == null || _cam == null) return anchor + Vector3.back * 10f;
        float screenH = 2f * ortho;
        anchor.x -= _composer.Composition.ScreenPosition.x * screenH * _cam.aspect;
        anchor.y += _composer.Composition.ScreenPosition.y * screenH; // Y экранный: +вниз
        anchor.z = -_composer.CameraDistance;
        return anchor;
    }

    /// <summary>
    /// Мгновенный снап камеры на корабль (начало забега) — без бленда/дэмпинга.
    /// Учитывает Composition.ScreenPosition: камера ставится так, чтобы корабль
    /// СРАЗУ оказался в целевой точке кадра — снап не «борется» с дэмпингом композера.
    /// </summary>
    public void SnapToShip()
    {
        BindVcam();
        if (_vcam == null || _vcam.Follow == null || _composer == null) return;
        Vector3 pos = _vcam.Follow.position;

        // Телепорт цели (рестарт = прыжок в origin): сбрасываем кэш lookahead/дэмпинга
        // композера штатным способом CM3, иначе он тянет старую позицию цели.
        _vcam.OnTargetObjectWarped(_vcam.Follow, pos - _vcam.transform.position);
        ForceFrame(_vcam.Lens.OrthographicSize);
    }
}
