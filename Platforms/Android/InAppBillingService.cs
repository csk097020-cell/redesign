using Plugin.InAppBilling;

namespace MomentaryMomentos.Services;

public class InAppBillingService : IInAppBillingService
{
    public async Task<bool> PurchaseAsync(string productId)
    {
        var billing = CrossInAppBilling.Current;
        try
        {
            if (!await billing.ConnectAsync())
                throw new InvalidOperationException("Could not connect to Google Play Billing.");

            var purchase = await billing.PurchaseAsync(productId, ItemType.Subscription);

            if (purchase is null)
                throw new OperationCanceledException("Purchase was cancelled.");

            if (purchase.State == PurchaseState.Purchased || purchase.State == PurchaseState.Restored)
            {
                // Acknowledge the purchase so Google doesn't refund it after 3 days
                await billing.FinalizePurchaseAsync([purchase.PurchaseToken]);
                return true;
            }

            if (purchase.State != PurchaseState.Purchased && purchase.State != PurchaseState.Restored)
                throw new OperationCanceledException("Purchase was cancelled.");

            throw new InvalidOperationException($"Purchase ended in unexpected state: {purchase.State}");
        }
        finally
        {
            await billing.DisconnectAsync();
        }
    }

    /// <summary>
    /// Android keeps the existing client-side entitlement path until Play closed testing
    /// clears. Returning null makes SubscriptionService skip server verification, so
    /// behavior on Android is unchanged. When Play is ready this returns the purchase
    /// token and the Edge Function calls purchases.subscriptionsv2.get.
    /// </summary>
    public Task<string?> GetPurchaseProofAsync() => Task.FromResult<string?>(null);

    public async Task<bool> RestoreAsync()
    {
        var billing = CrossInAppBilling.Current;
        try
        {
            if (!await billing.ConnectAsync())
                throw new InvalidOperationException("Could not connect to Google Play Billing.");

            var purchases = await billing.GetPurchasesAsync(ItemType.Subscription);
            return purchases?.Any(p =>
                p.State == PurchaseState.Purchased || p.State == PurchaseState.Restored) ?? false;
        }
        finally
        {
            await billing.DisconnectAsync();
        }
    }
}
