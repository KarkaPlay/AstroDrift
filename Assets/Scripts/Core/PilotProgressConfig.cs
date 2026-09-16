using System;
using UnityEngine;

/// <summary>
/// Мета-прогрессия: XP → уровень пилота → дерево разблокировок (GDD §5bis).
/// Ассет: Assets/Resources/PilotProgressConfig.asset
/// Ключ сохранения: AstroDrift.PilotXp (int, суммарный). Уровень всегда вычисляется из XP.
/// </summary>
[CreateAssetMenu(fileName = "PilotProgressConfig", menuName = "AstroDrift/PilotProgressConfig")]
public class PilotProgressConfig : ScriptableObject
{
    [Tooltip("XP = floor(очки / divisor)")]
    public int xpPerScoreDivisor = 10;

    // Порог-переход на уровень N стоит 500*N XP (формула в коде PilotProgressManager, не в данных)
    public UnlockEntry[] unlocks = Array.Empty<UnlockEntry>();
}

[Serializable]
public class UnlockEntry
{
    public int pilotLevel;          // 0 = стартовый пул
    public UnlockType type;         // Perk, Pickup, Enemy, Cosmetic, Mechanic
    public string id;               // PerkId / PickupType / EnemyType / skin id
    public bool implementedInWave1; // гейт для баннера «Разблокировано» (GDD §5bis.2)
}

public enum UnlockType { Perk, Pickup, Enemy, Cosmetic, Mechanic }
