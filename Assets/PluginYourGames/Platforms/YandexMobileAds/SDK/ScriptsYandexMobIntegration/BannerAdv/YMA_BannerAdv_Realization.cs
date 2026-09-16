#if BannerAdv_yg
namespace YG
{
    public partial class PlatformYG2 : IPlatformsYG2
    {
        public void LoadBanner(YG2.BannerPosition position) { }
        public void ShowBanner(YG2.BannerPosition position) => YMA_MainThread.Instance.bannerAdv.RequestBanner(position);
        public void HideBanner() => YMA_MainThread.Instance.bannerAdv.HideBanner();
        public void DestroyBanner() => YMA_MainThread.Instance.bannerAdv.DestroyBanner();
    }
}
#endif