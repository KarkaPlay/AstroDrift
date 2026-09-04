using UnityEngine;

/// <summary>
/// Очки, комбо, множитель, рекорд (GDD §5 / DevTask шаг 7).
/// Очки только за уничтожение. Комбо-таймер 3 с, множитель x1–x5 (таблица GDD §5).
/// Рекорд в PlayerPrefs.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    [SerializeField] private GameConfig config;

    private const string BestKey = "AstroDrift.Best";

    public static ScoreManager Instance { get; private set; }

    public int Score { get; private set; }
    public int Multiplier { get; private set; } = 1;
    public int Best { get; private set; }
    public bool NewBest { get; private set; }

    private int _combo;
    private float _comboTimer;

    public System.Action<int, int> OnScoreChanged; // score, multiplier
    public System.Action OnComboReset;

    private void Awake()
    {
        Instance = this;
        Best = PlatformServices.Save.GetInt(BestKey, 0);
    }

    /// <summary>Инициализация, когда конфиг назначается кодом (Bootstrap).</summary>
    public void InitFrom(GameConfig cfg)
    {
        config = cfg;
        Best = PlatformServices.Save.GetInt(BestKey, 0);
    }

    private void OnEnable()
    {
        GameEvents.AsteroidDestroyed += OnAsteroidDestroyed;
        GameEvents.MissileDestroyed += OnMissileDestroyed;
        GameEvents.MissileTimeout += OnMissileTimeout;
        GameEvents.Combo += OnComboEvent;
    }

    private void OnDisable()
    {
        GameEvents.AsteroidDestroyed -= OnAsteroidDestroyed;
        GameEvents.MissileDestroyed -= OnMissileDestroyed;
        GameEvents.MissileTimeout -= OnMissileTimeout;
        GameEvents.Combo -= OnComboEvent;
    }

    private void Update()
    {
        if (_combo > 0)
        {
            _comboTimer -= Time.deltaTime;
            if (_comboTimer <= 0f)
            {
                _combo = 0;
                Multiplier = 1;
                OnComboReset?.Invoke();
            }
        }
    }

    private void OnAsteroidDestroyed(Vector3 pos, AsteroidSize size)
    {
        int points = size switch
        {
            AsteroidSize.Large => config.largeAsteroidScore,
            AsteroidSize.Medium => config.mediumAsteroidScore,
            _ => config.smallAsteroidScore,
        };
        AddKill(points);
    }

    private void OnMissileDestroyed(Vector3 pos) => AddKill(config.missileScore);

    private void OnMissileTimeout(Vector3 pos) { /* тайм-аут не даёт очков */ }

    private void OnComboEvent(Vector3 pos) => AddKill(config.comboMissileAsteroid, comboBonus: true);

    private void AddKill(int basePoints, bool comboBonus = false)
    {
        // Каждое уничтожение увеличивает combo counter на 1 (множитель — таблица GDD §5)
        _combo++;
        int oldMultiplier = Multiplier;
        Multiplier = _combo switch
        {
            >= 15 => 5,
            >= 10 => 4,
            >= 6 => 3,
            >= 3 => 2,
            _ => 1,
        };
        Score += basePoints * Multiplier;
        _comboTimer = config.comboWindow;
        OnScoreChanged?.Invoke(Score, Multiplier);

        // Проверка рекорда
        if (Score > Best)
        {
            Best = Score;
            NewBest = true;
            PlatformServices.Save.SetInt(BestKey, Best);
            PlatformServices.Save.Flush();
            // ТЗ §3: сегментация профиля (новички < 500 / середина / опытные > 2000)
            Analytics.ProfileSetNumber("best_score", Best);
        }
    }

    /// <summary>
    /// Continue (GDD_DeathScreen_Continue §3/§10.5): счёт и множитель сохраняются,
    /// комбо-таймер перезапускается на полное окно.
    /// </summary>
    public void RestartComboWindow()
    {
        _comboTimer = config != null ? config.comboWindow : 3f;
    }

    public void ResetRun()
    {
        Score = 0;
        _combo = 0;
        Multiplier = 1;
        NewBest = false;
        _comboTimer = 0f;
    }

    /// <summary>
    /// Надёжность Android (ТЗ v3, Доработка 4): приложение может быть убито
    /// без корректного завершения — сохраняем рекорд при сворачивании.
    /// </summary>
    private void OnApplicationPause(bool paused)
    {
        if (paused) PlatformServices.Save.Flush();
    }
}
