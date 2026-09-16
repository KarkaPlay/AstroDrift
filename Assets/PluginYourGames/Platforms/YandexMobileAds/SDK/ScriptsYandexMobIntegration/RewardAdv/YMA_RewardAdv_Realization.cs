#if RewardedAdv_yg
namespace YG
{
    public partial class PlatformYG2 : IPlatformsYG2
    {
        public void RewardedAdvShow(string id) => YMA_MainThread.Instance.rewardAdv.ShowRewardedAd(); 
        public void LoadRewardedAdv() => YMA_MainThread.Instance.rewardAdv.RequestAd();
    }
}
#endif