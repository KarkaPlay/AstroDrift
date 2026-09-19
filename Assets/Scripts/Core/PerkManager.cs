using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Перки и левелап-выбор (GDD §15). Перки — модификаторы поверх GameConfig в рантайме
/// (GameConfig.asset НИКОГДА не мутируется, GDD §12). Читатели модификаторов:
/// ShipWeapon (fireInterval/bulletSpeed/bulletRadius/Piercing), ShipController (angular),
/// ScoreManager (score mult), ScoreManager.comboWindow, MissileSpawner→Missile (turnRate).
/// Архитектурно перк применим в любой момент — задел под Wave 3 «Loadout» (Roadmap).
/// </summary>
public class PerkManager : MonoBehaviour
{
    public static PerkManager Instance { get; private set; }

    [SerializeField] private PerkConfig config;
    [SerializeField] private GameConfig gameConfig;

    private readonly Dictionary<PerkId, int> _stacks = new Dictionary<PerkId, int>();
    private int _nextThresholdIndex;
    private int _thresholdsLeftDiff; // «последняя разница» для генерации порогов после конца массива
    private int _nextThresholdValue;
    private int _lastThresholdValue; // порог, уже пройденный в текущем забеге (база для прогресс-бара HUD)
    private bool _offerPending;      // оверлей открыт/ожидает выбора — порог не тикает повторно
    private int _rerollsLeft;
    private int _freeRerollsLeft;

    /// <summary>Порог пересечён, оверлей должен открыться (GameManager → GameUI).</summary>
    public event System.Action<PerkDefinition[]> OnLevelUpOffer;

    /// <summary>Перк применён по тапу игрока (звук/флоатинг-текст).</summary>
    public event System.Action<PerkDefinition> OnPerkChosen;

    public void InitFrom(PerkConfig cfg, GameConfig gcfg)
    {
        config = cfg;
        gameConfig = gcfg;
        ResetRun();
    }

    private void Awake() => Instance = this;
    private void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>Сброс забега: стаки перков, порог, реролл (счётчик рероллов — за забег, GDD §15.3).</summary>
    public void ResetRun()
    {
        _stacks.Clear();
        _nextThresholdIndex = 0;
        _thresholdsLeftDiff = 0;
        _nextThresholdValue = ThresholdAt(0);
        _lastThresholdValue = 0;
        _offerPending = false;
        _rerollsLeft = config != null ? config.rerollPerRun : 1;
        _freeRerollsLeft = config != null ? Mathf.Max(0, config.freeRerollsPerRun) : 1;
    }

    /// <summary>Порог по индексу; после конца массива — последний + (последняя разница + 1000), GDD §6.</summary>
    private int ThresholdAt(int index)
    {
        int[] t = gameConfig != null ? gameConfig.levelUpScoreThresholds : null;
        if (t == null || t.Length == 0) return int.MaxValue;
        if (index < t.Length) return t[index];
        int lastDiff = Mathf.Max(1000, t[t.Length - 1] - t[t.Length - 2]);
        int extra = index - t.Length + 1;
        return t[t.Length - 1] + extra * (lastDiff + 1000);
    }

    private int NextThresholdValue()
    {
        int value = _nextThresholdValue;
        int nextIdx = _nextThresholdIndex + 1;
        _thresholdsLeftDiff = ThresholdAt(nextIdx) - value; // для шага вперёд
        _nextThresholdIndex = nextIdx;
        _lastThresholdValue = value;                        // HUD: база прогресса между порогами
        _nextThresholdValue = ThresholdAt(nextIdx);
        return value;
    }

    // ——— HUD: прогресс до следующего перка (GDD §15.3) ———

    /// <summary>Очки следующего порога перка (int.MaxValue — пороги не заданы).</summary>
    public int NextPerkThreshold => _nextThresholdValue;

    /// <summary>Очки предыдущего порога (0 — перк ещё не взят ни разу).</summary>
    public int LastPerkThreshold => _lastThresholdValue;

    /// <summary>true = есть осмысленный интервал до следующего перка (иначе бар в HUD скрыт).</summary>
    public bool PerkProgressAvailable => _nextThresholdValue != int.MaxValue && _nextThresholdValue > _lastThresholdValue;

    /// <summary>Прогресс очков внутри текущего интервала порогов, 0..1.</summary>
    public float PerkProgress(int score)
    {
        if (!PerkProgressAvailable) return 0f;
        return Mathf.Clamp01((score - _lastThresholdValue) / (float)(_nextThresholdValue - _lastThresholdValue));
    }

    /// <summary>
    /// Проверка порога — ТОЛЬКО в состоянии Playing (GameManager.Update). Не в slow-mo
    /// смерти, не во время фриза. Два порога за кадр невозможны (мин. разница 1000).
    /// </summary>
    public void CheckThreshold(int score)
    {
        if (_offerPending || score < _nextThresholdValue) return;

        int threshold = NextThresholdValue();
        var offers = GenerateOffers();
        if (offers == null || offers.Length == 0)
        {
            // Пустой пул: заглушка «+500 очков» мгновенно, БЕЗ оверлея и БЕЗ фриза (GDD §15.3)
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.AddStubScore(config.stubScoreBonus > 0 ? config.stubScoreBonus : 500);
            return;
        }
        _offerPending = true;
        TimeFreeze.Freeze();
        OnLevelUpOffer?.Invoke(offers);
        _ = threshold;
    }

    /// <summary>3 карты: без повторов, максимизированные не попадают, доступных < 3 — сколько есть (GDD §15.3).</summary>
    public PerkDefinition[] GenerateOffers()
    {
        if (config == null) return null;
        var pool = new List<PerkDefinition>();
        var perks = config.perks;
        int pilotLevel = PilotProgressManager.Instance != null ? PilotProgressManager.Instance.PilotLevel : 0;
        for (int i = 0; i < perks.Length; i++)
        {
            var p = perks[i];
            bool levelOk = p.unlockedByDefault || pilotLevel >= p.unlockPilotLevel; // двойной гейт: пилот-уровень (§15.4)
            if (!levelOk) continue;
            _stacks.TryGetValue(p.id, out int st);
            if (st >= p.maxStacks) continue;
            pool.Add(p);
        }

        int offerCount = Mathf.Min(config.offerCount, pool.Count);
        var result = new PerkDefinition[offerCount];
        for (int i = 0; i < offerCount; i++)
        {
            int j = Random.Range(i, pool.Count);
            (pool[i], pool[j]) = (pool[j], pool[i]);
            result[i] = pool[i];
        }
        return result;
    }

    /// <summary>Выбор карты игроком: стак+1, модификаторы пересчитаны, разморозка мгновенная.</summary>
    public void Choose(PerkDefinition perk)
    {
        if (perk == null) return;
        _stacks.TryGetValue(perk.id, out int st);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string fieldName = FieldName(perk.id);
        string oldVal = FieldValue(perk.id);
#endif
        _stacks[perk.id] = st + 1;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[Perk] {perk.id} stack {st + 1}: {fieldName} {oldVal} → {FieldValue(perk.id)}");
#endif
        _offerPending = false;
        TimeFreeze.Unfreeze();
        OnPerkChosen?.Invoke(perk);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static string FieldName(PerkId id) => id switch
    {
        PerkId.BulletSpeed => "bulletSpeed",
        PerkId.FireRate => "fireInterval",
        PerkId.TurnSpeed => "shipAngularSpeed",
        PerkId.BiggerBullets => "bulletRadius",
        PerkId.ComboExtension => "comboWindow",
        PerkId.ScoreMultiplier => "scoreMultiplier",
        PerkId.MissileJammer => "missileTurnMultiplier",
        PerkId.Piercing => "pierceCount",
        _ => "unknown",
    };

    private string FieldValue(PerkId id) => id switch
    {
        PerkId.BulletSpeed => BulletSpeed.ToString("0.00"),
        PerkId.FireRate => FireInterval.ToString("0.00"),
        PerkId.TurnSpeed => ShipAngularSpeed.ToString("0.0"),
        PerkId.BiggerBullets => BulletRadius.ToString("0.000"),
        PerkId.ComboExtension => ComboWindow.ToString("0.0"),
        PerkId.ScoreMultiplier => ScoreMultiplier.ToString("0.00"),
        PerkId.MissileJammer => MissileTurnMultiplier.ToString("0.00"),
        PerkId.Piercing => PierceCount.ToString(),
        _ => "?",
    };
#endif

    /// <summary>Реролл: сначала бесплатный за забег, затем за рекламу. Карты — из ТОГО ЖЕ пула
    /// (пул не расширяется, §15.3). null — рероллов не осталось.</summary>
    public PerkDefinition[] Reroll()
    {
        if (_freeRerollsLeft > 0) _freeRerollsLeft--;
        else if (_rerollsLeft > 0) _rerollsLeft--;
        else return null;
        return GenerateOffers();
    }

    /// <summary>true — следующий реролл бесплатный (реклама не нужна).</summary>
    public bool RerollIsFree => _freeRerollsLeft > 0;

    public bool RerollAvailable => _freeRerollsLeft > 0 || _rerollsLeft > 0;

    // ——— Агрегированные модификаторы (читатели: ShipWeapon/ShipController/ScoreManager/MissileSpawner) ———

    private int Stacks(PerkId id) => _stacks.TryGetValue(id, out int s) ? s : 0;

    /// <summary>fireInterval с перком FireRate (−15%/стак). Базовое — из GameConfig (не мутируется).</summary>
    public float FireInterval => gameConfig != null
        ? gameConfig.fireInterval * Mathf.Pow(1f + PerkValue(PerkId.FireRate), Stacks(PerkId.FireRate))
        : 0.35f;

    /// <summary>bulletSpeed (+20%/стак).</summary>
    public float BulletSpeed => gameConfig != null
        ? gameConfig.bulletSpeed * Mathf.Pow(1f + PerkValue(PerkId.BulletSpeed), Stacks(PerkId.BulletSpeed))
        : 18f;

    /// <summary>bulletRadius (+50%/стак, визуал крупнее).</summary>
    public float BulletRadius => gameConfig != null
        ? gameConfig.bulletRadius * Mathf.Pow(1f + PerkValue(PerkId.BiggerBullets), Stacks(PerkId.BiggerBullets))
        : 0.08f;

    /// <summary>shipAngularSpeed (+25%/стак).</summary>
    public float ShipAngularSpeed => gameConfig != null
        ? gameConfig.shipAngularSpeed * Mathf.Pow(1f + PerkValue(PerkId.TurnSpeed), Stacks(PerkId.TurnSpeed))
        : 180f;

    /// <summary>comboWindow (+2 с/стак, аддитивно).</summary>
    public float ComboWindow => gameConfig != null
        ? gameConfig.comboWindow + PerkValue(PerkId.ComboExtension) * Stacks(PerkId.ComboExtension)
        : 3f;

    /// <summary>Множитель очков за убийство (+25%/стак).</summary>
    public float ScoreMultiplier => 1f + PerkValue(PerkId.ScoreMultiplier) * Stacks(PerkId.ScoreMultiplier);

    /// <summary>Множитель угловой скорости наведения ракет (−40%/стак) — MissileSpawner подставляет в Spawn.</summary>
    public float MissileTurnMultiplier => Mathf.Pow(1f + PerkValue(PerkId.MissileJammer), Stacks(PerkId.MissileJammer));

    /// <summary>Сколько врагов пробивает снаряд (+1/стак).</summary>
    public int PierceCount => Stacks(PerkId.Piercing);

    private float PerkValue(PerkId id)
    {
        if (config == null) return 0f;
        var perks = config.perks;
        for (int i = 0; i < perks.Length; i++)
            if (perks[i].id == id) return perks[i].valuePerStack;
        return 0f;
    }

    /// <summary>Стаки конкретного перка (UI/дебаг).</summary>
    public int GetStacks(PerkId id) => Stacks(id);
}
