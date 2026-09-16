namespace YG.Insides
{
    public partial class CommonOptions
    {
        public bool yandexMobAdsTestingMode = true;
        public bool yandexMobAutoLoadAds = true;
#if InterstitialAdv_yg
        public string yandexMobInterAdID;
#endif
#if RewardedAdv_yg
        public string yandexMobRewardAdID;
#endif
#if BannerAdv_yg
        public string yandexMobBannerAdID;
#endif
        public bool yandexMobAppOpenAdEnable = false;
        public string yandexMobAppOpenAdID;
    }
}
