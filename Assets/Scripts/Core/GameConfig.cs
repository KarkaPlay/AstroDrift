using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Все тюнящиеся числа игры (DevTask правило 5). Значения — из GDD/VisualStyle.
/// Ассет: Assets/Resources/GameConfig.asset
/// </summary>
[CreateAssetMenu(fileName = "GameConfig", menuName = "AstroDrift/GameConfig")]
public class GameConfig : ScriptableObject
{
    [Header("Корабль (GDD §4.1 / §2)")]
    public float shipSpeed = 5f;            // 5 ю/с (стартовая; дальше — Difficulty)
    public float shipAngularSpeed = 180f;   // 180°/с (тюнинг 120–240)
    public float shipRadius = 0.25f;        // прощающий хитбокс
    [Tooltip("НЕ ИСПОЛЬЗУЕТСЯ: старт без неуязвимости/мигания (решение владельца). Поле оставлено для совместимости ассета; мигает только неуязвимость после continue")]
    public float shipInvulnerableTime = 0.5f;

    [Header("Оружие (GDD §4.2)")]
    public float fireInterval = 0.35f;      // сек между выстрелами
    public float bulletSpeed = 18f;         // ю/с
    public float bulletLife = 1.5f;         // сек
    public float bulletRadius = 0.08f;      // trigger-коллайдер
    public float bulletKickback = 0.02f;    // микро-откат корабля при выстреле

    [Header("Астероиды (GDD §4.3)")]
    public AsteroidSizeDef[] asteroidSizes = Array.Empty<AsteroidSizeDef>();
    public float asteroidSpawnMargin = 3f;      // за экраном: orthoSize + 3
    public float asteroidDespawnMargin = 8f;    // деспавн: orthoSize + 8
    public float firstAsteroidDelay = 3f;       // первый астероид на 3-й секунде по курсу
    public float minSpawnDistance = 2.5f;       // мин. дистанция между спавнами
    public int maxAsteroids = 15;               // лимит
    public float hitFlashTime = 0.03f;          // вспышка белым (≈1 кадр)
    public float hitShakeAmplitude = 0.05f;     // тряска позиции
    public float hitShakeDuration = 0.04f;      // ≈2 кадра

    [Header("Ракеты (GDD §4.4)")]
    public float missileLife = 12f;             // тайм-аут самоуничтожения
    public float missileWarnTime = 1.5f;        // предупреждение до входа в кадр
    public int missileHp = 1;
    public int missileScore = 100;
    public float missileWarnBlinkOn = 0.3f;     // мигание предупреждения
    public float missileWarnBlinkOff = 0.2f;

    [Header("Очки и комбо (GDD §5)")]
    public float comboWindow = 3f;              // таймер сброса комбо
    public int comboMissileAsteroid = 200;      // комбо-бонус
    public int smallAsteroidScore = 10;
    public int mediumAsteroidScore = 25;
    public int largeAsteroidScore = 50;

    [Header("Смерть (GDD §7)")]
    public float deathSlowmoScale = 0.3f;       // Time.timeScale
    public float deathSlowmoDuration = 0.9f;    // реальных секунд в slow-mo

    [Header("Menu & Transitions v2 (ArtDirection §4–§6) — цифры хореографии, тюнятся здесь")]
    [Tooltip("§2: во сколько раз меню-зум крупнее игрового (2–2.5). Орто меню = орто игры / это значение")]
    public float menuZoomDivisor = 2.25f;
    [Tooltip("§2: ScreenPosition.y композера в меню-кадре (корабль ~60% высоты, между Best и CTA). Игровой = 0.2")]
    public float menuShipScreenY = 0.11f;
    [Tooltip("§5: длительность отъезда камеры меню → игровой зум, сек")]
    public float startFlyDuration = 2.0f;
    [Tooltip("Старт §5: единое t активации систем — стрельба/спавн угроз/HUD (конец сейф-зоны). Управление разблокировано с t=0. Держать равным startAccelerateTime и startFlyDuration")]
    public float startSystemsTime = 2.0f;
    [Tooltip("Старт §5: разгон корабля 0 → shipSpeed за это время (ease-out), сек")]
    public float startAccelerateTime = 2.0f;
    [Tooltip("§6.1: перелёт камеры «место смерти (zoom-in +15%)» → стартовый кадр, сек")]
    public float restartFlyDuration = 0.6f;
    [Tooltip("§6.1: дожим зума «стартовый кадр → игровой» после перелёта, сек")]
    public float restartZoomOutDuration = 1.4f;
    [Tooltip("§6.1: разблокировка управления при рестарте, сек")]
    public float restartUnlockTime = 0.5f;
    [Tooltip("§6.1: безопасная зона рестарта — спавн угроз не раньше, сек")]
    public float restartSafeZone = 0.6f;
    [Tooltip("§4.5: возвращение камеры в меню-кадр (Home), сек")]
    public float homeFlyDuration = 0.8f;
    [Tooltip("§4.2: zoom-in к месту смерти — на столько делим орто (+15% зума)")]
    public float deathZoomInFactor = 1.15f;
    [Tooltip("§4.2: задержка zoom-in после вспышки взрыва, сек")]
    public float deathZoomInDelay = 0.25f;
    [Tooltip("§4.2: длительность zoom-in к месту смерти, сек")]
    public float deathZoomInDuration = 0.4f;
    [Tooltip("§5/§6.1: длительность fade-in HUD, сек")]
    public float hudFadeDuration = 0.35f;

    [Header("Death-экран v2: Continue (GDD_DeathScreen_Continue §3–§7)")]
    [Tooltip("§3: таймер предложения «ПРОДОЛЖИТЬ» на Death-экране, сек")]
    public float continueOfferDuration = 5f;
    [Tooltip("§3/§6: радиус чистки угроз вокруг места смерти при continue, ю")]
    public float continueClearRadius = 8f;
    [Tooltip("§6: неуязвимость с миганием после continue, сек")]
    public float continueInvulnerableTime = 2f;
    [Tooltip("§6: safe zone спавна угроз после continue, сек")]
    public float continueSafeZone = 1.5f;
    [Tooltip("§5.2: разблокировка управления после continue, сек")]
    public float continueUnlockTime = 0.5f;
    [Tooltip("§5.2: камера — zoom-out «кадр смерти → игровой» на месте, сек")]
    public float continueZoomOutDuration = 1.2f;
    [Tooltip("Тихое окно после ЛЮБОЙ рекламы (interstitial или rewarded): interstitial " +
             "не показывается раньше, чем через это время, сек. Rewarded — не гейтится.")]
    [FormerlySerializedAs("rewardedQuietSeconds")]
    public float adsQuietSeconds = 60f;

    [Header("Screen shake (GDD §8 / DevTask шаг 10)")]
    public ShakePreset shakeHit = new ShakePreset(0.03f, 0.05f);       // попадание по астероиду
    public ShakePreset shakeAsteroidBreak = new ShakePreset(0.06f, 0.1f);
    public ShakePreset shakeMissileDestroy = new ShakePreset(0.08f, 0.12f);
    public ShakePreset shakeCombo = new ShakePreset(0.12f, 0.2f);
    public ShakePreset shakeDeath = new ShakePreset(0.12f, 0.2f);

    [Serializable]
    public class AsteroidSizeDef
    {
        public string sizeName = "Средний";
        public float radius = 0.5f;         // базовый радиус
        public int minVerts = 5, maxVerts = 6;
        public int hp = 2;
        public AsteroidSize childSize = AsteroidSize.Small; // во что раскалывается
        public int childCount = 2;
    }

    [Serializable]
    public class ShakePreset
    {
        public float amplitude;
        public float duration;
        public ShakePreset(float a, float d) { amplitude = a; duration = d; }
    }
}

/// <summary>Три размера астероида (GDD §4.3).</summary>
public enum AsteroidSize { Large, Medium, Small }
