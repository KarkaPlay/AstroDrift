namespace YG.Insides
{
    using YandexMobileAds;

    public partial class OptionalPlatform
    {
        /// <summary>
        /// Sets whether the user allowed personal data collection for analytics and ad targeting.
        /// </summary>
        public void SetUserConsent(bool consent) => YandexAds.SetUserConsent(consent);

        /// <summary>
        /// Sets whether the user is age-restricted and personal data collection must be disabled.
        /// </summary>
        public void SetAgeRestricted(bool ageRestricted) => YandexAds.SetAgeRestricted(ageRestricted);
    }
}