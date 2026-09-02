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
        if (_vcam != null) _vcam.Follow = t;
    }

    /// <summary>
    /// Мгновенный снап камеры на корабль (начало забега) — без бленда/дэмпинга.
    /// Учитывает Composition.ScreenPosition: камера ставится так, чтобы корабль
    /// СРАЗУ оказался в целевой точке кадра — снап не «борется» с дэмпингом композера.
    /// </summary>
    public void SnapToShip()
    {
        if (_vcam == null || _vcam.Follow == null || _composer == null) return;
        Vector3 pos = _vcam.Follow.position;

        // Телепорт цели (рестарт = прыжок в origin): сбрасываем кэш lookahead/дэмпинга
        // композера штатным способом CM3, иначе он тянет старую позицию цели.
        _vcam.OnTargetObjectWarped(_vcam.Follow, pos - _vcam.transform.position);

        // ScreenPosition в долях полного размера экрана: в ортографии
        // полная высота = 2 * orthoSize. Смещаем камеру в противоположную сторону,
        // чтобы цель легла в заданную точку кадра.
        float screenH = 2f * _vcam.Lens.OrthographicSize;
        pos.x -= _composer.Composition.ScreenPosition.x * screenH * _cam.aspect;
        pos.y += _composer.Composition.ScreenPosition.y * screenH; // Y экранный: +вниз
        pos.z = -_composer.CameraDistance;
        _vcam.ForceCameraPosition(pos, Quaternion.identity);
    }
}
