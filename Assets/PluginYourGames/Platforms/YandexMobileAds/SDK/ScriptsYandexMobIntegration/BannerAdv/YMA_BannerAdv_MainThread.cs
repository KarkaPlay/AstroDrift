#if BannerAdv_yg
namespace YG
{
    using System;
    using UnityEngine;
    using YandexMobileAds;
    using YandexMobileAds.Base;

    public class YMA_BannerAdv_MainThread : MonoBehaviour
    {
        private Banner banner;

        public void RequestBanner(YG2.BannerPosition position)
        {
            string adUnitId = YG2.infoYG.common.yandexMobAdsTestingMode
                ? "demo-banner-yandex"
                : YG2.infoYG.common.yandexMobBannerAdID;

            DestroyBanner();

            AdPosition yPos = AdPosition.BottomCenter;

            switch (position)
            {
                case YG2.BannerPosition.Top:
                    yPos = AdPosition.TopCenter;
                    break;
                case YG2.BannerPosition.Left:
                    yPos = AdPosition.CenterLeft;
                    break;
                case YG2.BannerPosition.Right:
                    yPos = AdPosition.CenterRight;
                    break;
                case YG2.BannerPosition.Bottom:
                    yPos = AdPosition.BottomCenter;
                    break;
            }

            BannerAdSize bannerSize = BannerAdSize.Sticky(GetScreenWidthDp());
            banner = new Banner(bannerSize, yPos);

            banner.OnAdLoaded += HandleAdLoaded;
            banner.OnAdFailedToLoad += HandleAdFailedToLoad;
            banner.OnAdClicked += HandleAdClicked;

            banner.LoadAd(new AdRequest(adUnitId));
        }

        private int GetScreenWidthDp()
        {
            int screenWidth = (int)Screen.safeArea.width;
            return ScreenUtils.ConvertPixelsToDp(screenWidth);
        }

        public void HideBanner() => DestroyBanner();
        public void DestroyBanner()
        {
            if (banner == null)
                return;

            banner.Destroy();
            banner = null;
        }


        public void HandleAdLoaded(object sender, EventArgs args) => banner.Show();
        public void HandleAdFailedToLoad(object sender, AdFailureEventArgs args) => YG2.onBannerError?.Invoke();
        public void HandleAdClicked(object sender, EventArgs args) => YG2.onBannerClicked?.Invoke();
    }
}
#endif
