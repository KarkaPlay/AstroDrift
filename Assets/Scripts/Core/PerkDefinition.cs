using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// Перк как ассет (GDD §15.2): данные живут в Assets/Resources/Perks/, владелец правит
/// их в инспекторе и добавляет новые перки без правки кода. Ключи локализации —
/// в таблице GameTexts (RU+EN), значения — в PerkConfig.asset (ссылки на эти ассеты).
/// </summary>
[CreateAssetMenu(fileName = "PerkDefinition", menuName = "AstroDrift/Perk Definition")]
public class PerkDefinition : ScriptableObject
{
    public PerkId id;
    public LocalizedString title;       // Table: GameTexts, Entry: perk_*_title
    public LocalizedString desc;        // Table: GameTexts, Entry: perk_*_desc
    public Sprite icon;                 // иконка карты; пусто → плашка-заглушка
    public Rarity rarity = Rarity.Common;
    public int maxStacks = 3;
    public bool unlockedByDefault;      // Bullet Speed+, Fire Rate+ = true (уровень 0)
    public int unlockPilotLevel;        // контракт §15.4
    public float valuePerStack;         // +0.20 / −0.15 / +0.25 / +0.50 / +2.0с / +0.25 / −0.40 / +1
}
