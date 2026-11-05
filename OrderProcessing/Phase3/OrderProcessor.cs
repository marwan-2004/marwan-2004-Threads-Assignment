using System.Diagnostics;
using OrderProcessing.Phase1;
using OrderProcessing.Phase2;
using OrderProcessing.Phase3.Services;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public class OrderProcessor
{
    private readonly CustomerService _customerService;
    private readonly InventoryService _inventoryService;
    private readonly PricingService _pricingService;
    private readonly IPaymentGateway _paymentGateway;
    private readonly Phase3OrderRepository _orderRepo;
    private readonly IEmailService _emailService;

    public OrderProcessor(
        CustomerService customerService,
        InventoryService inventoryService,
        PricingService pricingService,
        IPaymentGateway paymentGateway,
        Phase3OrderRepository orderRepo,
        IEmailService emailService)
    {
        _customerService = customerService;
        _inventoryService = inventoryService;
        _pricingService = pricingService;
        _paymentGateway = paymentGateway;
        _orderRepo = orderRepo;
        _emailService = emailService;
    }

    public async Task<bool> ProcessOrderAsync(Order order, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        Customer? customer = null;

        try
        {
            // Step 1: Customer Validation
            Log.Info($"Order #{order.Id} -> Step 1/6 Get Customer...");
            try
            {
                customer = await _customerService.GetCustomerAsync(order.CustomerId, ct);
                if (!customer.IsActive)
                {
                    order.Status = OrderStatus.Rejected;
                    order.FailureReason = "Customer is inactive";
                    await _orderRepo.SaveAsync(order, ct);
                    return false;
                }
            }
            catch (CustomerNotFoundException ex)
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason = ex.Message;
                await _orderRepo.SaveAsync(order, ct);
                return false;
            }

            // Step 2: Reserve Stock
            Log.Info($"Order #{order.Id} -> Step 2/6 Reserve Stock...");
            bool stockReserved = await _inventoryService.ReserveStockAsync(order, ct);
            if (!stockReserved)
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason = "Insufficient stock for items";
                await _orderRepo.SaveAsync(order, ct);
                return false;
            }

            // Step 3: Calculate Pricing
            Log.Info($"Order #{order.Id} -> Step 3/6 Pricing...");
            try
            {
                var pricer = new OrderPricer(_pricingService);
                await pricer.PriceOrderAsync(order, ct);
            }
            catch (InvalidCouponException ex)
            {
                await _inventoryService.ReleaseStockAsync(order, ct);
                order.Status = OrderStatus.Rejected;
                order.FailureReason = ex.Message;
                await _orderRepo.SaveAsync(order, ct);
                return false;
            }

            // Step 4: Payment Gateway (with 3-second timeout)
            Log.Info($"Order #{order.Id} -> Step 4/6 Payment...");
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            PaymentResult paymentResult;
            try
            {
                paymentResult = await _paymentGateway.ProcessPaymentAsync(order.Id, order.TotalAmount, linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                if (ct.IsCancellationRequested)
                {
                    // Global run cancellation
                    await _inventoryService.ReleaseStockAsync(order, CancellationToken.None);
                    order.Status = OrderStatus.Pending;
                    order.FailureReason = "Cancelled";
                    await _orderRepo.SaveAsync(order, CancellationToken.None);
                    return false;
                }

                // Payment Timeout (3s)
                Log.Error($"Order #{order.Id} -> Payment TIMEOUT after 3000 ms");
                await _inventoryService.ReleaseStockAsync(order, ct);
                order.Status = OrderStatus.PaymentFailed;
                order.FailureReason = "Payment gateway timeout (3s)";
                await _orderRepo.SaveAsync(order, ct);
                return false;
            }

            if (!paymentResult.Success)
            {
                Log.Error($"Order #{order.Id} -> Payment Declined");
                await _inventoryService.ReleaseStockAsync(order, ct);
                order.Status = OrderStatus.PaymentFailed;
                order.FailureReason = paymentResult.Message;
                await _orderRepo.SaveAsync(order, ct);
                return false;
            }

            order.TransactionId = paymentResult.TransactionId;

            // Step 5: Complete & Save Order
            Log.Info($"Order #{order.Id} -> Step 5/6 Saving Order Completed...");
            try
            {
                order.Status = OrderStatus.Completed;
                await _orderRepo.SaveAsync(order, ct);
            }
            catch (DatabaseWriteException ex)
            {
                Log.Error($"Order #{order.Id} -> DB Save failed after payment! Reverting...");
                if (order.TransactionId != null)
                {
                    await _paymentGateway.RefundAsync(order.TransactionId, ct);
                }
                await _inventoryService.ReleaseStockAsync(order, ct);
                order.Status = OrderStatus.Failed;
                order.FailureReason = $"Database save failed after payment; payment {order.TransactionId} refunded";
                await _orderRepo.SaveAsync(order, CancellationToken.None);
                return false;
            }

            // Step 6: Email Confirmation with Retry
            if (customer != null)
            {
                Log.Info($"Order #{order.Id} -> Step 6/6 Send Confirmation Email...");
                await SendConfirmationWithRetryAsync(order, customer, ct);
            }

            sw.Stop();
            Log.Success($"Order #{order.Id} Completed in {sw.ElapsedMilliseconds} ms");
            return true;
        }
        catch (OperationCanceledException)
        {
            await _inventoryService.ReleaseStockAsync(order, CancellationToken.None);
            order.Status = OrderStatus.Pending;
            order.FailureReason = "Cancelled";
            await _orderRepo.SaveAsync(order, CancellationToken.None);
            return false;
        }
    }

    private async Task SendConfirmationWithRetryAsync(Order order, Customer customer, CancellationToken ct)
    {
        int[] delays = { 500, 1000, 2000 };
        string lastError = string.Empty;

        for (int attempt = 1; attempt <= 4; attempt++)
        {
            try
            {
                await _emailService.SendConfirmationAsync(order, customer, ct);
                return;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                Log.Error($"Order #{order.Id} Email Attempt {attempt} failed: {ex.Message}");
                if (attempt < 4)
                {
                    await Task.Delay(delays[attempt - 1], ct);
                }
            }
        }

        // Attempt 4 failed -> Save to email_retry.json
        var failedEmail = new EmailMessage
        {
            OrderId = order.Id,
            To = customer.Email,
            Subject = $"Order #{order.Id} Confirmation",
            Body = $"Thank you {customer.Name}, your order total is {order.TotalAmount:C}",
            SentAt = DateTime.Now,
            Attempts = 4,
            LastError = lastError
        };

        var retries = await JsonFileStore.ReadAsync<EmailMessage>(DataFiles.EmailRetry, ct);
        retries.Add(failedEmail);
        await JsonFileStore.WriteAsync(DataFiles.EmailRetry, retries, ct);
        Log.Error($"Email for Order #{order.Id} queued for retry later");
    }
}