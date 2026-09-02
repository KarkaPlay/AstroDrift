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
    }

    /// <summary>Фазы по умолчанию — таблица GDD §6 (заполняется в ассете).</summary>
    public void ResetToGddDefaults()
    {
        phases = new[]
        {
            new PhaseParams { time = 0f,    shipSpeed = 5.0f, asteroidSpawnRate = 0.55f, bigAsteroidChance = 0f,  asteroidSpeedMin = 0.5f, asteroidSpeedMax = 1.5f, missileInterval = 0f,  missileSpeed = 7.5f, missileTurnRate = 35f,  maxMissiles = 0 },
            new PhaseParams { time = 30f,   shipSpeed = 5.5f, asteroidSpawnRate = 0.75f, bigAsteroidChance = 0.15f, asteroidSpeedMin = 0.8f, asteroidSpeedMax = 2.0f, missileInterval = 15f, missileSpeed = 7.7f, missileTurnRate = 40f,  maxMissiles = 1 },
            new PhaseParams { time = 60f,   shipSpeed = 6.0f, asteroidSpawnRate = 0.95f, bigAsteroidChance = 0.25f, asteroidSpeedMin = 1.0f, asteroidSpeedMax = 2.5f, missileInterval = 10f, missileSpeed = 7.9f, missileTurnRate = 45f,  maxMissiles = 2 },
            new PhaseParams { time = 120f,  shipSpeed = 6.5f, asteroidSpawnRate = 1.15f, bigAsteroidChance = 0.35f, asteroidSpeedMin = 1.2f, asteroidSpeedMax = 3.0f, missileInterval = 8f,  missileSpeed = 8.2f, missileTurnRate = 52f,  maxMissiles = 3 },
            new PhaseParams { time = 180f,  shipSpeed = 7.0f, asteroidSpawnRate = 1.3f, bigAsteroidChance = 0.40f, asteroidSpeedMin = 1.5f, asteroidSpeedMax = 3.5f, missileInterval = 6f,  missileSpeed = 8.5f, missileTurnRate = 60f,  maxMissiles = 5 },
        };
    }
}
