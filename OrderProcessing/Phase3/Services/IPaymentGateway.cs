using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3.Services;

public interface IPaymentGateway
{
    Task<PaymentResult> ProcessPaymentAsync(int orderId, decimal amount, CancellationToken ct = default);
    Task RefundAsync(string transactionId, CancellationToken ct = default);
}

public class PaymentGateway : IPaymentGateway
{
    private readonly HashSet<int> _timeoutOrderIds;
    private readonly HashSet<int> _declineOrderIds;

    public PaymentGateway(IEnumerable<int>? timeoutOrderIds = null, IEnumerable<int>? declineOrderIds = null)
    {
        _timeoutOrderIds = new HashSet<int>(timeoutOrderIds ?? Array.Empty<int>());
        _declineOrderIds = new HashSet<int>(declineOrderIds ?? Array.Empty<int>());
    }

    public async Task<PaymentResult> ProcessPaymentAsync(int orderId, decimal amount, CancellationToken ct = default)
    {
        Log.Info($"Payment Gateway -> Authorizing Order #{orderId}...");
        await Task.Delay(800, ct);

        Log.Info($"Payment Gateway -> Fraud check for Order #{orderId}...");
        await Task.Delay(500, ct);

        if (_declineOrderIds.Contains(orderId))
        {
            var declineRecord = new PaymentTransaction
            {
                OrderId = orderId,
                Amount = amount,
                Status = PaymentStatus.Declined,
                TransactionId = null,
                CreatedAt = DateTime.Now
            };
            await SavePaymentAsync(declineRecord, ct);

            return new PaymentResult
            {
                Success = false,
                Status = PaymentStatus.Declined,
                Message = "Card declined by issuer"
            };
        }

        if (_timeoutOrderIds.Contains(orderId))
        {
            
            await Task.Delay(5000, ct);
        }
        else
        {
            await Task.Delay(700, ct); 
        }

        string txnId = "TXN-" + Guid.NewGuid().ToString("N")[..12].ToUpper();
        var captureRecord = new PaymentTransaction
        {
            OrderId = orderId,
            Amount = amount,
            Status = PaymentStatus.Captured,
            TransactionId = txnId,
            CreatedAt = DateTime.Now
        };
        await SavePaymentAsync(captureRecord, ct);

        return new PaymentResult
        {
            Success = true,
            TransactionId = txnId,
            Status = PaymentStatus.Captured,
            Message = "Payment captured successfully"
        };
    }

    public async Task RefundAsync(string transactionId, CancellationToken ct = default)
    {
        Log.Info($"Payment Gateway -> Refunding transaction {transactionId}...");
        await Task.Delay(500, ct);

        var payments = await JsonFileStore.ReadAsync<PaymentTransaction>(DataFiles.Payments, ct);
        var payment = payments.FirstOrDefault(p => p.TransactionId == transactionId);
        if (payment != null)
        {
            payment.Status = PaymentStatus.Refunded;
            await JsonFileStore.WriteAsync(DataFiles.Payments, payments, ct);
        }
    }

    private static async Task SavePaymentAsync(PaymentTransaction payment, CancellationToken ct)
    {
        var payments = await JsonFileStore.ReadAsync<PaymentTransaction>(DataFiles.Payments, ct);
        payments.Add(payment);
        await JsonFileStore.WriteAsync(DataFiles.Payments, payments, ct);
    }
}