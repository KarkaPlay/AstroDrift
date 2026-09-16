using System;
using UnityEngine;

/// <summary>
/// Перки и левелап-выбор (GDD §15). Ассет: Assets/Resources/PerkConfig.asset
/// Применение — модификаторы поверх GameConfig в рантайме (PerkManager); ассет GameConfig не мутируется (GDD §12).
/// </summary>
[CreateAssetMenu(fileName = "PerkConfig", menuName = "AstroDrift/PerkConfig")]
public class PerkConfig : ScriptableObject
{
    public int offerCount = 3;          // карт в окне выбора
    public int stubScoreBonus = 500;    // заглушка при пустом пуле (без оверлея, без фриза)
    public int rerollPerRun = 1;        // rewarded-рероллов за ЗАБЕГ (не за левелап), GDD §10.1/§15.3
    public PerkDef[] perks = Array.Empty<PerkDef>();
}

[Serializable]
public class PerkDef
{
    public PerkId id;
    public string titleKey = "";        // локализация (RU+EN)
    public string descKey = "";
    public Sprite icon;                 // иконка карты; пусто → плашка-заглушка цветом редкости
    public Rarity rarity = Rarity.Common;
    public int maxStacks = 3;
    public bool unlockedByDefault;      // Bullet Speed+, Fire Rate+ = true (уровень 0)
    public int unlockPilotLevel;        // контракт §15.4
    public float valuePerStack;         // +0.20 / −0.15 / +0.25 / +0.50 / +2.0с / +0.25 / −0.40 / +1
}

public enum PerkId
{
    BulletSpeed, FireRate, TurnSpeed, BiggerBullets,
    ComboExtension, ScoreMultiplier, MissileJammer, Piercing
}

public enum Rarity { Common, Rare }
