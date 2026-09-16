using UnityEngine;

/// <summary>
/// Единый владелец фриза timeScale = 0 (ТЗ §0.5, GDD §15.3): пауза и перк-выбор
/// идут через этот контроллер. Вход/выход резкий (1 кадр), без slow-mo компромиссов.
/// Во время фриза запрещён повторный вход (вложенные фризы) и пауза поверх перка.
/// </summary>
public static class TimeFreeze
{
    public static bool Frozen { get; private set; }

    public static void Freeze()
    {
        // Повторный вход безвреден: пишем timeScale каждый раз, чтобы slow-mo смерти
        // (OnShipHit) не «залип» поверх фриза, если смерть случилась при активном фризе.
        Frozen = true;
        Time.timeScale = 0f;
    }

    public static void Unfreeze()
    {
        if (!Frozen) return;
        Frozen = false;
        Time.timeScale = 1f;
    }
}
