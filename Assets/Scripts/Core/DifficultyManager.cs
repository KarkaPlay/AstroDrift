using UnityEngine;

/// <summary>
/// Сложность по времени забега (GDD §6): все значения интерполируются линейно
/// между фазами (Lerp по elapsed time). Обёртка над DifficultyConfig.
/// </summary>
public class DifficultyManager : MonoBehaviour
{
    [SerializeField] private DifficultyConfig config;

    public static DifficultyManager Instance { get; private set; }

    public float Elapsed { get; private set; }

    private void Awake() => Instance = this;

    /// <summary>Инициализация, когда конфиг назначается кодом (Bootstrap).</summary>
    public void InitFrom(DifficultyConfig cfg) => config = cfg;

    public void Tick(float delta)
    {
        Elapsed += delta;
    }

    public void ResetRun() => Elapsed = 0f;

    public float ShipSpeed => config.ShipSpeedAt(Elapsed);
}
