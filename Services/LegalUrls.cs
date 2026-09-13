namespace MomentaryMomentos.Services;

/// <summary>
/// Hosted on GitHub Pages (public repo: jsnewtonian/momo-privacy). Edit the pages by editing
/// index.html / terms.html in that repo — changes go live without an app update. Source copies
/// also live at docs/privacy_policy.html and docs/terms_of_service.html.
///
/// App Store Review Guideline 3.1.2 requires apps with auto-renewable subscriptions to carry
/// functional links to BOTH the privacy policy and the terms of use inside the binary, and
/// specifically within the purchase flow. Build 12 was rejected under 3.1.2(c) because the links
/// existed only on the Profile page, so they are shared here and used from both places.
/// </summary>
public static class LegalUrls
{
    public const string PrivacyPolicy = "https://jsnewtonian.github.io/momo-privacy/";
    public const string TermsOfUse    = "https://jsnewtonian.github.io/momo-privacy/terms.html";
}
