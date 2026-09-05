#if YandexGamesPlatform_yg && (UNITY_WEBGL || UNITY_EDITOR) // AstroDrift baseline fix (ТЗ §2.6)
using System.Runtime.InteropServices;

namespace YG
{
    public partial class PlatformYG2 : IPlatformsYG2
    {
        [DllImport("__Internal")]
        private static extern void StickyAdActivity_js(bool activity);

        public void StickyAdActivity(bool activity)
        {
            if (activity)
            {
                YG2.Message("StickyAdv Show");
            }
            else
            {
                YG2.Message("StickyAdv Hide");
            }
#if !UNITY_EDITOR
            StickyAdActivity_js(activity);
#endif
        }
    }
}
#endif