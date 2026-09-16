#if InterstitialAdv_yg
namespace YG
{
    using System;
    using UnityEngine;
    using YandexMobileAds;
    using YandexMobileAds.Base;
    using YG.Insides;

    public class YMA_MainThread_InterAdv : MonoBehaviour
    {
        private InterstitialAdLoader interstitialAdLoader;
        private Interstitial interstitial;
        private bool isLoading;
        private bool showAfterLoad;

        public void Setup()
        {
            if (YG2.infoYG.InterstitialAdv.showFirstAdv || YG2.infoYG.common.yandexMobAppOpenAdEnable)
            {
                YMA_AppOpen_MainThread appOpen;
                appOpen = gameObject.AddComponent<YMA_AppOpen_MainThread>();
                appOpen.Setup();
            }

            interstitialAdLoader = new InterstitialAdLoader();
        }

        public void RequestInterstitial()
        {
            RequestInterstitial(false);
        }

        private void RequestInterstitial(bool showAfterLoad)
        {
            string adUnitId = YG2.infoYG.common.yandexMobAdsTestingMode 
                ? "demo-interstitial-yandex" 
                : YG2.infoYG.common.yandexMobInterAdID;

            if (interstitial != null)
            {
                if (showAfterLoad)
                    ShowInterstitial();

                return;
            }

            this.showAfterLoad |= showAfterLoad;

            if (isLoading)
                return;

            isLoading = true;
            interstitialAdLoader.LoadAd(new AdRequest(adUnitId), HandleAdLoaded, HandleAdFailedToLoad);
        }

        public void ShowInterstitial()
        {
            if (interstitial == null)
            {
                if (YG2.infoYG.common.yandexMobAutoLoadAds)
                    RequestInterstitial(true);

                return;
            }

            interstitial.OnAdClicked += HandleAdClicked;
            interstitial.OnAdShown += HandleAdShown;
            interstitial.OnAdFailedToShow += HandleAdFailedToShow;
            interstitial.OnAdDismissed += HandleAdDismissed;

            interstitial.Show();
        }

        #region Interstitial callback handlers

        private void HandleAdLoaded(Interstitial interstitial)
        {
            isLoading = false;
            this.interstitial = interstitial;
            YG2.optionalPlatform.onLoadedInterAdv?.Invoke();

            if (showAfterLoad)
            {
                showAfterLoad = false;
                ShowInterstitial();
            }
        }

        private void HandleAdFailedToLoad(AdFailedToLoadEventArgs args)
        {
            isLoading = false;
            showAfterLoad = false;
            YGInsides.ErrorInterAdv();
        }
        private void HandleAdClicked(object sender, EventArgs args) => YG2.optionalPlatform.onClickedInterAdv?.Invoke();

        private void HandleAdShown(object sender, EventArgs args) => YGInsides.OpenInterAdv();

        private void HandleAdDismissed(object sender, EventArgs args)
        {
            DestroyAd();
            YGInsides.CloseInterAdv();
        }

        private void HandleAdFailedToShow(object sender, AdFailureEventArgs args)
        {
            DestroyAd();
            YGInsides.ErrorInterAdv();
        }

        private void DestroyAd()
        {
            if (interstitial != null)
            {
                interstitial.Destroy();
                interstitial = null;
            }
        }

        #endregion
    }
}
#endif
