using UnityEngine;

/// <summary>
/// Событийный мост между сущностями, спавнерами и VFX/Audio/Score.
/// Простые статические события без аллокаций — не абстрактная шина (YAGNI).
/// </summary>
public static class GameEvents
{
    public static System.Action<Vector3> AsteroidHit;        // попадание по астероиду (не убийство)
    public static System.Action<Vector3, AsteroidSize> AsteroidDestroyed;
    public static System.Action<AsteroidSize, Vector2, Vector2, float, float> SpawnAsteroid; // size, pos, dir, speed, angular
    public static System.Action<Vector3> AsteroidDespawned;  // деспавн по дистанции (не уничтожение)
    public static System.Action<Vector3> MissileDestroyed;
    public static System.Action<Vector3> MissileTimeout;     // самоуничтожение по тайм-ауту
    public static System.Action<Vector3> Combo;              // ракета + астероид

    public static void RaiseAsteroidHit(Vector3 p) => AsteroidHit?.Invoke(p);
    public static void RaiseAsteroidDestroyed(Vector3 p, AsteroidSize s) => AsteroidDestroyed?.Invoke(p, s);
    public static void RaiseAsteroidDespawned(Vector3 p) => AsteroidDespawned?.Invoke(p);
    public static void RaiseSpawnAsteroid(AsteroidSize s, Vector2 p, Vector2 d, float sp, float a)
        => SpawnAsteroid?.Invoke(s, p, d, sp, a);
    public static void RaiseMissileDestroyed(Vector3 p) => MissileDestroyed?.Invoke(p);
    public static void RaiseMissileTimeout(Vector3 p) => MissileTimeout?.Invoke(p);
    public static void RaiseCombo(Vector3 p) => Combo?.Invoke(p);
}
