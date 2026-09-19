using System;
using UnityEngine;

/// <summary>
/// Перки и левелап-выбор (GDD §15). Ассет: Assets/Resources/PerkConfig.asset
/// Применение — модификаторы поверх GameConfig в рантайме (PerkManager); ассет GameConfig не мутируется (GDD §12).
/// Сами перки — ассеты PerkDefinition (Assets/Resources/Perks/), добавление перка не требует правки кода.
/// </summary>
[CreateAssetMenu(fileName = "PerkConfig", menuName = "AstroDrift/PerkConfig")]
public class PerkConfig : ScriptableObject
{
    public int offerCount = 3;          // карт в окне выбора
    public int stubScoreBonus = 500;    // заглушка при пустом пуле (без оверлея, без фриза)
    public int rerollPerRun = 1;        // rewarded-рероллов за ЗАБЕГ (не за левелап), GDD §10.1/§15.3
    public int freeRerollsPerRun = 1;   // бесплатных рероллов за ЗАБЕГ (первый — без рекламы), GDD §15.3
    public PerkDefinition[] perks = Array.Empty<PerkDefinition>();
}

public enum PerkId
{
    BulletSpeed, FireRate, TurnSpeed, BiggerBullets,
    ComboExtension, ScoreMultiplier, MissileJammer, Piercing
}

public enum Rarity { Common, Rare }
