// APPLICATION-SPECIFIC CODE - Property of Client (Corinne Kelley)
// This model represents configurable subscription settings as per contract Section 4.3

namespace MomentaryMomentos.Models;

/// <summary>
/// Configurable subscription settings that can be updated by the client
/// without requiring app updates (Contract Appendix A, Section 4.3).
/// These values are fetched from the backend admin configuration.
/// </summary>
public class SubscriptionConfig
{
    /// <summary>Maximum memories allowed on the free tier.</summary>
    public int FreeUserVideoLimit { get; set; } = 100;

    /// <summary>Maximum memories allowed on the paid tier.</summary>
    public int PaidUserVideoLimit { get; set; } = 1000;

    /// <summary>Monthly subscription price in USD.</summary>
    public decimal MonthlyPrice { get; set; } = 4.99m;

    /// <summary>Annual subscription price in USD.</summary>
    public decimal AnnualPrice { get; set; } = 49.99m;

    public bool MonthlyEnabled { get; set; } = true;
    public bool AnnualEnabled  { get; set; } = true;
}
