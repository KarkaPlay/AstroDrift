using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Screen shake (GDD §8) — тонкая обёртка над Cinemachine Impulse (CM3, Unity 6).
/// Источник импульса (CinemachineImpulseSource) и офсет-приёмник
/// (CinemachineImpulseListener на CM_VCam) сериализованы в сцене Game.unity —
/// код только запускает импульс. Писатель позиции камеры — CinemachineBrain.
/// Пресеты GameConfig.shake* остаются источником амплитуды/длительности.
/// </summary>
public class CameraShake : MonoBehaviour
{
    private CinemachineImpulseSource _impulse;

    private const float ImpulseAmplitudeScale = 20f; // юниты/сек из пресета → заметный импульс

    private CinemachineImpulseSource Impulse
    {
        get
        {
            if (_impulse == null) _impulse = GetComponent<CinemachineImpulseSource>();
            return _impulse;
        }
    }

    public void Shake(float amplitude, float duration)
    {
        var src = Impulse;
        if (src == null) return;

        // Амплитуда — через velocity (по пресетам GameConfig.shake*), масштаб — константа,
        // чтобы пресеты 0.03–0.12 давали заметный импульс. Длительность — огибающая
        // ImpulseSource (настраивается под пресет).
        var env = src.ImpulseDefinition.TimeEnvelope;
        env.AttackTime = 0f;
        env.SustainTime = Mathf.Max(duration * 0.35f, 0.01f);
        env.DecayTime = Mathf.Max(duration * 0.65f, 0.01f);
        src.ImpulseDefinition.TimeEnvelope = env;
        src.GenerateImpulse(new Vector3(amplitude * ImpulseAmplitudeScale, amplitude * ImpulseAmplitudeScale, 0f));
    }
}
