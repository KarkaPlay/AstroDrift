using UnityEngine;

/// <summary>
/// Мета-прогрессия (GDD §5bis/§12): XP копится навсегда, уровень ВСЕГДА вычисляется из XP
/// (формула: суммарный XP уровня N = 500 × N × (N+1) / 2; переход N−1 → N стоит 500×N).
/// Ключ сохранения: AstroDrift.PilotXp (int, суммарный; §0.4 — не переименовывать).
/// XP конвертируется один раз за забег — в момент финальной смерти (после Continue).
/// Другие системы (PerkManager/PickupManager/UI) читают PilotLevel/IsUnlocked отсюда,
/// о дереве напрямую не знают (GDD §15.4.3).
/// </summary>
public class PilotProgressManager : MonoBehaviour
{
    private const string XpKey = "AstroDrift.PilotXp";
    private const string StartShieldDateKey = "AstroDrift.StartShieldDate"; // добавление поля (§0.6 миграции)

    /// <summary>Уровень, с которого доступна rewarded-точка «стартовый щит» (GameConfig.startShieldUnlockPilotLevel).</summary>
    public int StartShieldGateLevel { get; set; } = 8;

    public static PilotProgressManager Instance { get; private set; }

    [SerializeField] private PilotProgressConfig config;

    public int Xp { get; private set; }
    public int PilotLevel { get; private set; }

    /// <summary>newLevel, unlocked записи дерева этого уровня (только уровень, не весь XP).</summary>
    public event System.Action<int, UnlockEntry[]> OnPilotLevelUp;

    /// <summary>Начислено XP при финальной смерти (для UI Death-экрана: «+Y XP»).</summary>
    public event System.Action<int> OnRunXpGranted;

    /// <summary>Результат последнего начисления XP (для UI Death-экрана: «+Y XP» — дельта этой смерти).</summary>
    public int LastRunXp { get; private set; }
    public int LevelBeforeLastRun { get; private set; }

    private int _grantedRunXp; // XP, уже конвертированный из очков текущего забега (на каждой смерти — только дельта)

    public void InitFrom(PilotProgressConfig cfg)
    {
        config = cfg;
        Xp = PlatformServices.Save.GetInt(XpKey, 0); // гейт совместимости: нет ключа → XP = 0, уровень = 0
        PilotLevel = LevelFromXp(Xp);
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Суммарный XP для достижения уровня N: 500 × N × (N+1) / 2.</summary>
    public static long XpForLevel(int level)
        => 500L * level * (level + 1) / 2;

    /// <summary>Уровень из суммарного XP (уровень никогда не понижается по определению формулы).</summary>
    public static int LevelFromXp(long xp)
    {
        int level = 0;
        while (XpForLevel(level + 1) <= xp) level++;
        return level;
    }

    /// <summary>
    /// Конверсия очков забега в XP — дельтой, на КАЖДОЙ смерти (фикс плейтеста Волны 1:
    /// Death-экран обязан показывать «+Y XP» сразу, а не после Retry/Home). Continue
    /// продолжает счёт — на следующей смерти выдаётся только дельта. «Один забег = один
    /// суммарный XP» сохраняется; новый забег сбрасывает конвертацию через ResetRunGrant()
    /// в GameManager.BeginRun. GDD §5/§5bis.1.
    /// </summary>
    public void GrantRunXp(int runScore)
    {
        if (config == null) return;
        int totalRunXp = runScore / Mathf.Max(1, config.xpPerScoreDivisor);
        int delta = System.Math.Max(0, totalRunXp - _grantedRunXp);
        LastRunXp = delta;
        LevelBeforeLastRun = PilotLevel;

        if (delta > 0)
        {
            Xp += delta;
            _grantedRunXp = totalRunXp;
            PlatformServices.Save.SetInt(XpKey, Xp);
            PlatformServices.Save.Flush(); // защита от рассинхрона (риск-таблица GDD §14), по образцу ScoreManager
        }
        OnRunXpGranted?.Invoke(delta);

        int newLevel = LevelFromXp(Xp);
        if (newLevel > PilotLevel)
        {
            var unlocked = GetUnlocksForRange(PilotLevel + 1, newLevel);
            PilotLevel = newLevel;
            OnPilotLevelUp?.Invoke(PilotLevel, unlocked);
        }
    }

    /// <summary>Сброс конвертации забега (новый забег — вызывается из GameManager.BeginRun).</summary>
    public void ResetRunGrant() => _grantedRunXp = 0;

    /// <summary>Гейт доступности (GDD §15.4): PerkManager/PickupManager/UI читают отсюда.</summary>
    public bool IsUnlocked(string id)
    {
        if (config == null || string.IsNullOrEmpty(id)) return false;
        var unlocks = config.unlocks;
        for (int i = 0; i < unlocks.Length; i++)
        {
            if (unlocks[i].id == id)
                return PilotLevel >= unlocks[i].pilotLevel;
        }
        return false;
    }

    /// <summary>Все записи дерева (UI-заглушка списка разблокировок на стартовом экране).</summary>
    public UnlockEntry[] AllUnlocks => config != null ? config.unlocks : System.Array.Empty<UnlockEntry>();

    /// <summary>Записи дерева для диапазона уровней [fromLevel..toLevel] (баннер «Разблокировано»).</summary>
    public UnlockEntry[] GetUnlocksForRange(int fromLevel, int toLevel)
    {
        if (config == null) return System.Array.Empty<UnlockEntry>();
        int count = 0;
        var unlocks = config.unlocks;
        for (int i = 0; i < unlocks.Length; i++)
            if (unlocks[i].pilotLevel >= fromLevel && unlocks[i].pilotLevel <= toLevel) count++;

        var result = new UnlockEntry[count];
        int k = 0;
        for (int i = 0; i < unlocks.Length; i++)
            if (unlocks[i].pilotLevel >= fromLevel && unlocks[i].pilotLevel <= toLevel) result[k++] = unlocks[i];
        return result;
    }

    /// <summary>Прогресс до следующего уровня 0..1 (UI: XP bar).</summary>
    public float ProgressToNextLevel()
    {
        long cur = XpForLevel(PilotLevel);
        long next = XpForLevel(PilotLevel + 1);
        if (next <= cur) return 1f;
        return Mathf.Clamp01((float)((Xp - cur) / (float)(next - cur)));
    }

    /// <summary>XP, недостающий до следующего уровня (UI).</summary>
    public int XpToNextLevel()
    {
        long next = XpForLevel(PilotLevel + 1);
        return (int)System.Math.Max(0, next - Xp);
    }

    /// <summary>XP внутри текущего уровня (UI: «1200 / 1500 XP»).</summary>
    public int XpIntoCurrentLevel()
    {
        long cur = XpForLevel(PilotLevel);
        return (int)(Xp - cur);
    }

    /// <summary>XP, требуемый для перехода PilotLevel → PilotLevel+1 (UI).</summary>
    public int XpForNextLevelStep()
    {
        return 500 * (PilotLevel + 1);
    }

    // ——— Rewarded: стартовый щит (GDD §10.1/§4.6): 1 раз в день, гейт уровня пилота ———

    /// <summary>Дата (UTC yyyy-MM-dd) последнего использования стартового щита; пусто — никогда.</summary>
    public string StartShieldLastDate => PlatformServices.Save.GetString(StartShieldDateKey, "");

    /// <summary>true = щит уже взят сегодня (disabled-состояние кнопки).</summary>
    public bool StartShieldUsedToday => StartShieldLastDate == System.DateTime.UtcNow.ToString("yyyy-MM-dd");

    /// <summary>Отметить использование стартового щита сегодня (вызывается после успешного rewarded).</summary>
    public void MarkStartShieldUsed()
    {
        PlatformServices.Save.SetString(StartShieldDateKey, System.DateTime.UtcNow.ToString("yyyy-MM-dd"));
        PlatformServices.Save.Flush();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) PlatformServices.Save.Flush();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) PlatformServices.Save.Flush();
    }
}
