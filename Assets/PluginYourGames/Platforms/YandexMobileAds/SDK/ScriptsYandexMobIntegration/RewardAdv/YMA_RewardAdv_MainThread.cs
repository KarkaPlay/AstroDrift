#if RewardedAdv_yg
namespace YG
{
    using System;
    using UnityEngine;
    using YandexMobileAds;
    using YandexMobileAds.Base;
    using YG.Insides;

    public class YMA_RewardAdv_MainThread : MonoBehaviour
    {
        private RewardedAdLoader rewardedAdLoader;
        private RewardedAd rewardedAd;
        private bool isLoading;
        private bool showAfterLoad;

        public void Setup()
        {
            rewardedAdLoader = new RewardedAdLoader();
        }

        public void RequestAd()
        {
            RequestAd(false);
        }

        private void RequestAd(bool showAfterLoad)
        {
            string adUnitId = YG2.infoYG.common.yandexMobAdsTestingMode
                ? "demo-rewarded-yandex"
                : YG2.infoYG.common.yandexMobRewardAdID;

            if (rewardedAd != null)
            {
                if (showAfterLoad)
                    ShowRewardedAd();

                return;
            }

            this.showAfterLoad |= showAfterLoad;

            if (isLoading)
                return;

            isLoading = true;
            rewardedAdLoader.LoadAd(new AdRequest(adUnitId), HandleAdLoaded, HandleAdFailedToLoad);
        }

        public void ShowRewardedAd()
        {
            if (rewardedAd == null)
            {
                if (YG2.infoYG.common.yandexMobAutoLoadAds)
                    RequestAd(true);

                return;
            }

            rewardedAd.OnAdClicked += HandleAdClicked;
            rewardedAd.OnAdShown += HandleAdShown;
            rewardedAd.OnAdFailedToShow += HandleAdFailedToShow;
            rewardedAd.OnAdDismissed += HandleAdDismissed;
            rewardedAd.OnRewarded += HandleRewarded;

            rewardedAd.Show();
        }

        #region Rewarded callback handlers

        private void HandleAdLoaded(RewardedAd rewardedAd)
        {
            isLoading = false;
            this.rewardedAd = rewardedAd;
            YG2.optionalPlatform.onLoadedRewardedAdv?.Invoke();

            if (showAfterLoad)
            {
                showAfterLoad = false;
                ShowRewardedAd();
            }
        }

        private void HandleAdFailedToLoad(AdFailedToLoadEventArgs args)
        {
            isLoading = false;
            showAfterLoad = false;
            YGInsides.ErrorRewardedAdv();
        }
        private void HandleAdClicked(object sender, EventArgs args) => YG2.optionalPlatform.onClickedRewardedAdv?.Invoke();

        private void HandleAdShown(object sender, EventArgs args) => YGInsides.OpenRewardedAdv();

        public void HandleRewarded(object sender, Reward args) => YGInsides.RewardAdv();

        private void HandleAdDismissed(object sender, EventArgs args)
        {
            DestroyAd();
            YGInsides.CloseRewardedAdv();
        }

        private void HandleAdFailedToShow(object sender, AdFailureEventArgs args)
        {
            DestroyAd();
            YGInsides.ErrorRewardedAdv();
        }

        private void DestroyAd()
        {
            if (rewardedAd != null)
            {
                rewardedAd.Destroy();
                rewardedAd = null;
            }
        }

        #endregion
    }
}
#endif
