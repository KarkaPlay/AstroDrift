using System;
using UnityEngine;

/// <summary>
/// Пикапы (GDD §4.6): Rapid Fire, Spread Shot, Shield.
/// Ассет: Assets/Resources/PickupConfig.asset
/// </summary>
[CreateAssetMenu(fileName = "PickupConfig", menuName = "AstroDrift/PickupConfig")]
public class PickupConfig : ScriptableObject
{
    public float pickupLifetime = 6f;          // сек на земле до исчезновения
    public float pickupBlinkLastSeconds = 2f;  // мигать последние N сек
    public float pickupRadius = 0.35f;         // коллайдер
    public int maxActiveEffects = 2;           // одновременных временных эффектов
    [Tooltip("Лимит пикапов на земле одновременно (GDD §6). Спавнер ждёт деспавна.")]
    public int maxPickupsOnGround = 4;
    public PickupDef[] pickups = Array.Empty<PickupDef>();
}

[Serializable]
public class PickupDef
{
    public PickupType type;               // RapidFire, SpreadShot, Shield (+ будущие)
    public float duration = 5f;           // для Shield игнорируется (до использования)
    public float dropChanceAsteroid = 0f; // из среднего/крупного (0..1)
    public float dropChanceMissile = 0f;  // из ракеты (0..1)
    public Color color = Color.white;     // §9 палитра
    public bool unlockedByDefault;        // контракт §15.4: доступен ли тип ВООБЩЕ
    public int unlockPilotLevel;          // уровень пилота, с которого доступен (§15.4)
}

public enum PickupType { RapidFire, SpreadShot, Shield }
