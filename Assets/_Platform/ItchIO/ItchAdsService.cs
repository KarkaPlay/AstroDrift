#if STORE_ITCH
using System;
using UnityEngine;

/// <summary>
/// Реклама на itch.io: политика «рекламы нет».
///
/// Отличие от NullAdsService (заглушка редактора): здесь НЕТ фейк-показа и фейк-награды.
/// NullAdsService.IsRewardedReady == true, а ShowRewarded через 2 с выдаёт награду —
/// в шипнутой WebGL-сборке это значило бы, что ad-gated кнопки («Продолжить» на Death-экране,
/// «Реролл» перков) видны и награды выдаются без рекламы. На itch это запрещено.
///
/// Следствие (осознанное): ad-gated кнопки не показываются — IsRewardedReady == false,
/// ShowRewarded синхронно отдаёт false, ShowInterstitial возвращает false (переход мгновенный,
/// аудио не глушится, т.к. AdsFlow выходит до SetMuted при false). Баннеры — no-op с логом.
/// </summary>
public sealed class ItchAdsService : IAdsService
{
#pragma warning disable CS0067 // interstitial не показывается by design — событие не стреляет никогда
    public event Action InterstitialClosed;
#pragma warning restore CS0067

    public bool IsRewardedReady => false;

    public bool ShowInterstitial()
    {
        Debug.Log("[Platform] itch.io: interstitial отключён — показ не выполняется.");
        return false;
    }

    public void ShowRewarded(Action<bool> onResult)
    {
        Debug.Log("[Platform] itch.io: rewarded отключён — награда не выдаётся.");
        onResult?.Invoke(false);
    }

    public void ShowBanner() => Debug.Log("[Platform] itch.io: баннеры отключены (ShowBanner).");

    public void HideBanner() => Debug.Log("[Platform] itch.io: баннеры отключены (HideBanner).");
}
#endif
