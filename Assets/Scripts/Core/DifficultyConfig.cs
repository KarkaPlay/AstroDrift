using System;
using UnityEngine;

/// <summary>
/// Таблица фаз сложности (GDD §6). Всё интерполируется линейно (Lerp) между
/// контрольными точками по elapsed time. Ассет: Assets/Settings/DifficultyConfig.asset
/// </summary>
[CreateAssetMenu(fileName = "DifficultyConfig", menuName = "AstroDrift/DifficultyConfig")]
public class DifficultyConfig : ScriptableObject
{
    [SerializeField] private PhaseParams[] phases = Array.Empty<PhaseParams>();

    public float ShipSpeedAt(float t) => Eval(t, p => p.shipSpeed);
    public float AsteroidSpawnRateAt(float t) => Eval(t, p => p.asteroidSpawnRate);
    public float BigAsteroidChanceAt(float t) => Eval(t, p => p.bigAsteroidChance);
    public float AsteroidSpeedMinAt(float t) => Eval(t, p => p.asteroidSpeedMin);
    public float AsteroidSpeedMaxAt(float t) => Eval(t, p => p.asteroidSpeedMax);
    public float MissileSpeedAt(float t) => Eval(t, p => p.missileSpeed);
    public float MissileTurnRateAt(float t) => Eval(t, p => p.missileTurnRate);
    public int MaxMissilesAt(float t) => Mathf.RoundToInt(Eval(t, p => p.maxMissiles));
    public float PickupDropMultiplierAt(float t) => Eval(t, p => p.pickupDropMultiplier);

    /// <summary>Максимум maxMissiles по всем фазам (UX5.8: прогрев пула индикаторов).</summary>
    public int MaxMissilesOverall()
    {
        int max = 0;
        for (int i = 0; i < phases.Length; i++)
            max = Mathf.Max(max, Mathf.RoundToInt(phases[i].maxMissiles));
        return max;
    }

    /// <summary>Интервал спавна ракет: фаза 1 — нет, далее Lerp по фазам 2–5.</summary>
    public float MissileIntervalAt(float t)
    {
        if (phases.Length < 2 || t <= phases[1].time) return -1f; // «нет»
        if (t >= phases[phases.Length - 1].time) return phases[phases.Length - 1].missileInterval;
        for (int i = 1; i < phases.Length - 1; i++)
        {
            float a = phases[i].time, b = phases[i + 1].time;
            if (t >= a && t < b)
            {
                float k = Mathf.InverseLerp(a, b, t);
                return Mathf.Lerp(phases[i].missileInterval, phases[i + 1].missileInterval, k);
            }
        }
        return phases[1].missileInterval;
    }

    private float Eval(float t, Func<PhaseParams, float> select)
    {
        if (phases.Length == 0) return 0f;
        if (phases.Length == 1 || t <= phases[0].time) return select(phases[0]);
        if (t >= phases[phases.Length - 1].time) return select(phases[phases.Length - 1]);

        for (int i = 0; i < phases.Length - 1; i++)
        {
            float a = phases[i].time, b = phases[i + 1].time;
            if (t >= a && t < b)
            {
                float k = Mathf.InverseLerp(a, b, t);
                return Mathf.Lerp(select(phases[i]), select(phases[i + 1]), k);
            }
        }
        return select(phases[0]);
    }

    [Serializable]
    public struct PhaseParams
    {
        public PhaseParams(float dropMult)
        {
            time = 0f; shipSpeed = 0f; asteroidSpawnRate = 0f; bigAsteroidChance = 0f;
            asteroidSpeedMin = 0f; asteroidSpeedMax = 0f;
            missileInterval = 0f; missileSpeed = 0f; missileTurnRate = 0f; maxMissiles = 0;
            perkThresholdScore = 0; pickupDropMultiplier = dropMult;
            droneWaveInterval = 0f; dronesPerWave = 0; turretCount = 0;
        }
        [Header("Начало фазы (сек)")] public float time;

        [Header("Корабль")] public float shipSpeed;

        [Header("Астероиды")] public float asteroidSpawnRate; // шт/сек
        [Range(0f, 1f)] public float bigAsteroidChance;
        public float asteroidSpeedMin;
        public float asteroidSpeedMax;

        [Header("Ракеты")] public float missileInterval; // сек; фаза 1 = 0 («нет»)
        public float missileSpeed;
        public float missileTurnRate; // °/с
        public float maxMissiles;

        [Header("Перки и пикапы (v3, GDD §6/§15)")]
        [Tooltip("Порог перк-левелапа — интро-справка; активные пороги в GameConfig.levelUpScoreThresholds")]
        public int perkThresholdScore;
        public float pickupDropMultiplier; // множитель шанса дропа пикапов
        [Tooltip("Будущая волна (§4.7): 0 = нет")] public float droneWaveInterval;
        [Tooltip("Будущая волна (§4.7)")] public int dronesPerWave;
        [Tooltip("Будущая волна (§4.8)")] public int turretCount;
    }

    /// <summary>Фазы по умолчанию — таблица GDD §6 v3 (заполняется в ассете; v2-тюнинг 1.0–1.8 и первая ракета ~15 с сохранён).</summary>
    public void ResetToGddDefaults()
    {
        phases = new[]
        {
            new PhaseParams { time = 0f,   shipSpeed = 5.0f, asteroidSpawnRate = 1.0f, bigAsteroidChance = 0f,    asteroidSpeedMin = 0.8f, asteroidSpeedMax = 1.8f, missileInterval = 15f, missileSpeed = 7.5f, missileTurnRate = 35f, maxMissiles = 1, perkThresholdScore = 1000,  pickupDropMultiplier = 1.0f,  droneWaveInterval = 0f,  dronesPerWave = 0, turretCount = 0 },
            new PhaseParams { time = 30f,  shipSpeed = 5.5f, asteroidSpawnRate = 1.2f, bigAsteroidChance = 0.15f, asteroidSpeedMin = 1.0f, asteroidSpeedMax = 2.2f, missileInterval = 11f, missileSpeed = 7.7f, missileTurnRate = 40f, maxMissiles = 2, perkThresholdScore = 2500,  pickupDropMultiplier = 1.0f,  droneWaveInterval = 0f,  dronesPerWave = 0, turretCount = 0 },
            new PhaseParams { time = 60f,  shipSpeed = 6.0f, asteroidSpawnRate = 1.4f, bigAsteroidChance = 0.25f, asteroidSpeedMin = 1.2f, asteroidSpeedMax = 2.7f, missileInterval = 9f,  missileSpeed = 7.9f, missileTurnRate = 45f, maxMissiles = 3, perkThresholdScore = 5000,  pickupDropMultiplier = 1.25f, droneWaveInterval = 25f, dronesPerWave = 3, turretCount = 0 },
            new PhaseParams { time = 120f, shipSpeed = 6.5f, asteroidSpawnRate = 1.6f, bigAsteroidChance = 0.35f, asteroidSpeedMin = 1.4f, asteroidSpeedMax = 3.2f, missileInterval = 7f,  missileSpeed = 8.2f, missileTurnRate = 52f, maxMissiles = 4, perkThresholdScore = 8000,  pickupDropMultiplier = 1.5f,  droneWaveInterval = 20f, dronesPerWave = 4, turretCount = 1 },
            new PhaseParams { time = 180f, shipSpeed = 7.0f, asteroidSpawnRate = 1.8f, bigAsteroidChance = 0.40f, asteroidSpeedMin = 1.6f, asteroidSpeedMax = 3.7f, missileInterval = 5f,  missileSpeed = 8.5f, missileTurnRate = 60f, maxMissiles = 5, perkThresholdScore = 12000, pickupDropMultiplier = 1.75f, droneWaveInterval = 15f, dronesPerWave = 5, turretCount = 2 },
            new PhaseParams { time = 300f, shipSpeed = 7.5f, asteroidSpawnRate = 1.8f, bigAsteroidChance = 0.45f, asteroidSpeedMin = 1.8f, asteroidSpeedMax = 4.2f, missileInterval = 5f,  missileSpeed = 8.8f, missileTurnRate = 65f, maxMissiles = 5, perkThresholdScore = 17000, pickupDropMultiplier = 2.0f,  droneWaveInterval = 12f, dronesPerWave = 5, turretCount = 2 },
        };
    }
}
