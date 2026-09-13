using Foundation;
using StoreKit;

namespace MomentaryMomentos.Services;

/// <summary>
/// iOS in-app billing implementation using StoreKit.
/// Products must be configured in App Store Connect with IDs matching those in SubscriptionService.
/// </summary>
public class InAppBillingService : NSObject, ISKPaymentTransactionObserver, IInAppBillingService
{
    private TaskCompletionSource<bool>? _purchaseTcs;
    private TaskCompletionSource<bool>? _restoreTcs;
    private bool _hasRestoredPurchase;

    // StoreKit holds only a weak reference to the request delegate, and SKProductsRequest itself is
    // not rooted by the queue. Kept in fields (this service is a DI singleton) so the GC cannot
    // collect either one before ReceivedResponse/RequestFailed fires.
    private SKProductsRequest? _productsRequest;
    private ProductsRequestHandler? _productsHandler;

    // Same rooting hazard as the products request above: SKRequest holds only a weak
    // reference to its delegate, so both ends live in fields until the callback fires.
    private SKReceiptRefreshRequest? _receiptRequest;
    private ReceiptRefreshHandler? _receiptHandler;

    public InAppBillingService()
    {
        SKPaymentQueue.DefaultQueue.AddTransactionObserver(this);
    }

    public Task<bool> PurchaseAsync(string productId)
    {
        if (!SKPaymentQueue.CanMakePayments)
            throw new InvalidOperationException("In-app purchases are disabled on this device.");

        _purchaseTcs = new TaskCompletionSource<bool>();

        var handler = new ProductsRequestHandler(_purchaseTcs, ReleaseProductsRequest);
        var request = new SKProductsRequest(new NSSet<NSString>(new NSString(productId)));
        request.Delegate = handler;

        _productsHandler = handler;
        _productsRequest = request;

        request.Start();

        return _purchaseTcs.Task;
    }

    /// <summary>Drops the roots once StoreKit has called back and the request can no longer fire.</summary>
    private void ReleaseProductsRequest()
    {
        _productsRequest = null;
        _productsHandler = null;
    }

    public Task<bool> RestoreAsync()
    {
        _hasRestoredPurchase = false;
        _restoreTcs = new TaskCompletionSource<bool>();
        SKPaymentQueue.DefaultQueue.RestoreCompletedTransactions();
        return _restoreTcs.Task;
    }

    /// <summary>
    /// The base64 App Store receipt, for server-side validation. A fresh install has no
    /// receipt file until the app transacts, so ask StoreKit to fetch one and retry once.
    /// Returns null if there is still nothing — the caller treats that as "unknown" and
    /// leaves the existing entitlement alone rather than downgrading.
    /// </summary>
    public async Task<string?> GetPurchaseProofAsync()
    {
        if (ReadReceipt() is { } receipt)
            return receipt;

        try
        {
            await RefreshReceiptAsync();
        }
        catch (Exception ex)
        {
            // No receipt is a normal state (never purchased, or signed out of the store),
            // not an error worth surfacing to the user.
            System.Diagnostics.Debug.WriteLine($"Receipt refresh failed: {ex.Message}");
            return null;
        }

        return ReadReceipt();
    }

    private static string? ReadReceipt()
    {
        var url = NSBundle.MainBundle.AppStoreReceiptUrl;
        if (url?.Path is null || !File.Exists(url.Path))
            return null;

        using var data = NSData.FromUrl(url);
        return data?.GetBase64EncodedString(NSDataBase64EncodingOptions.None);
    }

    private Task RefreshReceiptAsync()
    {
        var tcs = new TaskCompletionSource<bool>();
        var handler = new ReceiptRefreshHandler(tcs, ReleaseReceiptRequest);
        var request = new SKReceiptRefreshRequest();
        request.Delegate = handler;

        _receiptHandler = handler;
        _receiptRequest = request;

        request.Start();

        // StoreKit can leave a refresh outstanding indefinitely when the device is offline
        // or the user dismisses the sign-in sheet. Entitlement refresh runs on app resume,
        // so it must not hang the caller.
        return tcs.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }

    private void ReleaseReceiptRequest()
    {
        _receiptRequest = null;
        _receiptHandler = null;
    }

    // ── ISKPaymentTransactionObserver ─────────────────────────────────────────

    public void UpdatedTransactions(SKPaymentQueue queue, SKPaymentTransaction[] transactions)
    {
        foreach (var t in transactions)
        {
            switch (t.TransactionState)
            {
                case SKPaymentTransactionState.Purchased:
                    queue.FinishTransaction(t);
                    _purchaseTcs?.TrySetResult(true);
                    break;

                case SKPaymentTransactionState.Restored:
                    queue.FinishTransaction(t);
                    _hasRestoredPurchase = true;
                    break;

                case SKPaymentTransactionState.Failed:
                    queue.FinishTransaction(t);
                    if (t.Error?.Code == (nint)SKError.PaymentCancelled)
                        _purchaseTcs?.TrySetCanceled();
                    else
                        _purchaseTcs?.TrySetException(new InvalidOperationException(
                            t.Error?.LocalizedDescription ?? "Purchase failed."));
                    break;
            }
        }
    }

    public void PaymentQueueRestoreCompletedTransactionsFinished(SKPaymentQueue queue)
    {
        _restoreTcs?.TrySetResult(_hasRestoredPurchase);
    }

    public void RestoreCompletedTransactionsFailedWithError(SKPaymentQueue queue, NSError error)
    {
        _restoreTcs?.TrySetException(new InvalidOperationException(error.LocalizedDescription));
    }
}

/// <summary>
/// Completes an <see cref="SKReceiptRefreshRequest"/> so the app can hand a receipt to the
/// verify-subscription Edge Function.
/// </summary>
internal sealed class ReceiptRefreshHandler : SKRequestDelegate
{
    private readonly TaskCompletionSource<bool> _tcs;
    private readonly Action _onFinished;

    internal ReceiptRefreshHandler(TaskCompletionSource<bool> tcs, Action onFinished)
    {
        _tcs = tcs;
        _onFinished = onFinished;
    }

    public override void RequestFinished(SKRequest request)
    {
        _onFinished();
        _tcs.TrySetResult(true);
    }

    public override void RequestFailed(SKRequest request, NSError error)
    {
        _onFinished();
        _tcs.TrySetException(new InvalidOperationException(error.LocalizedDescription));
    }
}

/// <summary>
/// Handles the App Store product lookup before submitting payment.
/// </summary>
internal sealed class ProductsRequestHandler : NSObject, ISKProductsRequestDelegate
{
    private readonly TaskCompletionSource<bool> _tcs;
    private readonly Action _onFinished;

    internal ProductsRequestHandler(TaskCompletionSource<bool> tcs, Action onFinished)
    {
        _tcs = tcs;
        _onFinished = onFinished;
    }

    public void ReceivedResponse(SKProductsRequest request, SKProductsResponse response)
    {
        _onFinished();

        if (response.Products.Length == 0)
        {
            // Name the rejected IDs. The store returns an empty product list for several unrelated
            // reasons — a genuinely wrong product ID, a product that isn't approved yet, or (as with
            // the build 12 rejection) no active Paid Applications Agreement on the account — and
            // without the IDs the message can't tell them apart.
            // Note: .NET for iOS binds Apple's `invalidProductIdentifiers` as `InvalidProducts`.
            var invalid = response.InvalidProducts is { Length: > 0 } ids
                ? string.Join(", ", ids)
                : "unknown";

            _tcs.TrySetException(new InvalidOperationException(
                $"Product not found in App Store ({invalid}). Verify the product ID is configured in " +
                "App Store Connect and that the Paid Applications Agreement is active."));
            return;
        }

        var payment = SKPayment.CreateFrom(response.Products[0]);
        SKPaymentQueue.DefaultQueue.AddPayment(payment);
        // TCS is resolved by UpdatedTransactions in InAppBillingService once the transaction completes.
    }

    public void RequestFailed(SKRequest request, NSError error)
    {
        _onFinished();
        _tcs.TrySetException(new InvalidOperationException(error.LocalizedDescription));
    }
}
