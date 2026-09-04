using System;

/// <summary>
/// Игровой контракт рекламы. Игра (AdsFlow) знает только этот интерфейс —
/// конкретный SDK подключает платформенная папка (_Platform/RuStore и т.д.).
/// </summary>
public interface IAdsService
{
    /// <summary>Закрытие interstitial ИЛИ ошибка показа — событие стреляет всегда,
    /// чтобы игра не подвисла (порт текущего HandleInterstitialDismissed/FailedToShow).</summary>
    event Action InterstitialClosed;

    /// <summary>Rewarded загружен и готов к показу. В заглушке — true.</summary>
    bool IsRewardedReady { get; }

    /// <summary>Raw-показ interstitial, если загружен (без частотной формулы — она в AdsFlow).
    /// false — не загружен / уже показывается; НЕ блокируем (порт текущего поведения).</summary>
    bool ShowInterstitial();

    /// <summary>Неблокирующий показ rewarded. onResult — РОВНО ОДИН раз:
    /// true — только при выдаче награды (OnRewarded), false — закрытие без награды /
    /// ошибка показа / отказ (не загружен, уже показывается). Порт FireRewardedResult.</summary>
    void ShowRewarded(Action<bool> onResult);

    void ShowBanner();
    void HideBanner();
}
