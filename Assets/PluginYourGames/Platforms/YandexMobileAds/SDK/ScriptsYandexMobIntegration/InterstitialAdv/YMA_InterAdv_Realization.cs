#if InterstitialAdv_yg
namespace YG
{
    public partial class PlatformYG2 : IPlatformsYG2
    {
        public void InterstitialAdvShow() => YMA_MainThread.Instance.interAdv.ShowInterstitial(); 
        public void LoadInterAdv() => YMA_MainThread.Instance.interAdv.RequestInterstitial();
    }
}
#endif