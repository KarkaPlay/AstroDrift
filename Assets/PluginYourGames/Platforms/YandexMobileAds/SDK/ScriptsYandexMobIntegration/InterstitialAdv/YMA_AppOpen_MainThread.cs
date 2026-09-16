#if InterstitialAdv_yg
namespace YG
{
    using System;
    using UnityEngine;
    using YandexMobileAds;
    using YandexMobileAds.Base;
    using YG.Insides;

    public class YMA_AppOpen_MainThread : MonoBehaviour
    {
        private AppOpenAdLoader appOpenAdLoader;
        private AppOpenAd appOpenAd;
        private bool isAdShowOnColdStart;
        private bool isAdLoadingProcess;

        public void Setup()
        {
            appOpenAdLoader = new AppOpenAdLoader();

            RequestInterstitial();

            if (YG2.infoYG.common.yandexMobAppOpenAdEnable)
                AppStateObserver.OnAppStateChanged += HandleAppStateChanged;
        }

        public void OnDestroy()
        {
            if (YG2.infoYG.common.yandexMobAppOpenAdEnable)
                AppStateObserver.OnAppStateChanged -= HandleAppStateChanged;
        }

        public void RequestInterstitial()
        {
            string adUnitId = YG2.infoYG.common.yandexMobAdsTestingMode 
                ? "demo-appopenad-yandex"
                : YG2.infoYG.common.yandexMobAppOpenAdID;

            if (appOpenAd != null)
            {
                appOpenAd.Destroy();
            }

            appOpenAdLoader.LoadAd(new AdRequest(adUnitId), HandleAdLoaded, HandleAdFailedToLoad);
            isAdLoadingProcess = true;
        }

        private void ShowAppOpenAd()
        {
            if (appOpenAd != null) appOpenAd.Show();
        }

        #region Interstitial callback handlers

        public void HandleAdLoaded(AppOpenAd appOpenAd)
        {
            isAdLoadingProcess = false;
            this.appOpenAd = appOpenAd;

            this.appOpenAd.OnAdClicked += HandleAdClicked;
            this.appOpenAd.OnAdShown += HandleAdShown;
            this.appOpenAd.OnAdFailedToShow += HandleAdFailedToShow;
            this.appOpenAd.OnAdDismissed += HandleAdDismissed;

            if (!isAdShowOnColdStart
                && YG2.infoYG.InterstitialAdv.showFirstAdv)
            {
                ShowAppOpenAd();
            }

            isAdShowOnColdStart = true;
        }

        public void HandleAppStateChanged(object sender, AppStateChangedEventArgs args)
        {
            if (isAdShowOnColdStart && args.IsInBackground == false)
            {
                ShowAppOpenAd();
            }
        }

        private void HandleAdFailedToLoad(AdFailedToLoadEventArgs args) => YGInsides.ErrorInterAdv();
        private void HandleAdClicked(object sender, EventArgs args) => YG2.optionalPlatform.onClickedInterAdv?.Invoke();

        private void HandleAdShown(object sender, EventArgs args) => YGInsides.OpenInterAdv();

        private void HandleAdDismissed(object sender, EventArgs args)
        {
            AdvertisementWasShown();
            YGInsides.CloseInterAdv();
        }

        private void HandleAdFailedToShow(object sender, AdFailureEventArgs args)
        {
            AdvertisementWasShown();
            YGInsides.ErrorInterAdv();
        }

        private void AdvertisementWasShown()
        {
            if (appOpenAd != null)
            {
                appOpenAd.Destroy();
                appOpenAd = null;
            }

            if (!isAdLoadingProcess
                && YG2.infoYG.common.yandexMobAppOpenAdEnable)
            {
                RequestInterstitial();
            }
        }

        #endregion
    }
}
#endif
