using System.Collections.Concurrent;
using System.Diagnostics;
using OrderProcessing.Phase1;
using OrderProcessing.Phase2;
using OrderProcessing.Phase3;
using OrderProcessing.Phase3.Services;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase4;

public static class Phase4Runner
{
    public static async Task RunAsync(CancellationToken ct = default)
    {
        Log.Info("=== Starting Phase 4 - Scale-Up ===");

        // --- Task 10.1: Live Checkout Progress (Task.WhenEach) ---
        Log.Info("\n--- Task 10.1: Live Checkout Progress ---");
        await DataSeeder.ResetAsync(ct);

        var customerRepo = new CustomerRepository();
        var productRepo = new ProductRepository();
        var couponRepo = new CouponRepository();
        var incomingRepo = new IncomingOrderRepository();

        var customerService = new CustomerService(customerRepo);
        var inventoryService = new InventoryService(productRepo);
        var pricingService = new PricingService(customerRepo, productRepo, couponRepo, Array.Empty<int>());
        var paymentGateway = new PaymentGateway(timeoutOrderIds: new[] { 5 }, declineOrderIds: new[] { 8 });
        var orderRepo = new Phase3OrderRepository(failSaveForOrderIds: new[] { 13 });
        var emailService = new EmailService(failAttempts: new Dictionary<int, int> { [3] = 2, [17] = int.MaxValue });

        var processor = new OrderProcessor(customerService, inventoryService, pricingService, paymentGateway, orderRepo, emailService);
        var incomingOrders = await incomingRepo.GetAllAsync(ct);

        var orderTasks = incomingOrders.Select(async o =>
        {
            var sw = Stopwatch.StartNew();
            await processor.ProcessOrderAsync(o, ct);
            sw.Stop();
            var orders = await JsonFileStore.ReadAsync<Order>(DataFiles.Orders, ct);
            var fresh = orders.First(ord => ord.Id == o.Id);
            return (Order: fresh, ElapsedSec: sw.Elapsed.TotalSeconds);
        }).ToList();

        int completedCount = 0;
        int totalCount = orderTasks.Count;

        
        await foreach (var completedTask in Task.WhenEach(orderTasks))
        {
            var result = await completedTask;
            completedCount++;
            string reason = string.IsNullOrEmpty(result.Order.FailureReason) ? "" : $" ({result.Order.FailureReason})";
            Console.WriteLine($"[{completedCount}/{totalCount}] Order #{result.Order.Id} -> {result.Order.Status}{reason} after {result.ElapsedSec:F1} s");
        }

        // --- Task 10.2: Customer Dashboard ---
        Log.Info("\n--- Task 10.2: Customer Dashboard ---");
        var dashboardService = new CustomerDashboardService(failPaymentsForCustomerIds: new[] { 99 });

        var swDash = Stopwatch.StartNew();
        var dash3 = await dashboardService.LoadAsync(3, ct);
        swDash.Stop();
        Log.Success($"Customer #3 ({dash3.Customer.Name}) loaded in {swDash.ElapsedMilliseconds} ms: {dash3.Orders.Count} orders, {dash3.Payments.Count} payments, {dash3.Emails.Count} emails");

        try
        {
            await dashboardService.LoadAsync(99, ct);
        }
        catch (AggregateException ex)
        {
            Log.Error($"Customer #99 Dashboard loaded with {ex.InnerExceptions.Count} failures:");
            foreach (var inner in ex.InnerExceptions)
            {
                Log.Error($" - Failure: {inner.Message}");
            }
        }

        // --- Task 10.3: Fastest Shipping Quote ---
        Log.Info("\n--- Task 10.3: Fastest Shipping Quote ---");
        var carriers = new List<IShippingCarrier>
        {
            new ShippingCarrier("FastShip", 500, 1500, 1.00m),
            new ShippingCarrier("CairoExpress", 300, 2000, 0.95m, failForOrderIds: new[] { 2, 4 }),
            new ShippingCarrier("NileCourier", 800, 1200, 1.10m)
        };
        var quoteService = new ShippingQuoteService(carriers);

        for (int i = 1; i <= 5; i++)
        {
            var ord = incomingOrders.First(o => o.Id == i);
            var quote = await quoteService.GetFastestQuoteAsync(ord, ct);
            Log.Info($"Order #{i} -> Winner: {quote.Carrier} | Price: {quote.Price:C} | Time: {quote.ElapsedMs:F0} ms");
        }

        // --- Task 10.4 A: Fraud Scoring (CPU-Bound) ---
        Log.Info("\n--- Task 10.4 A: Fraud Scoring (CPU-Bound) ---");
        int totalFraudOrders = 10_000;

        
        var swSeq = Stopwatch.StartNew();
        List<int> seqFlagged = new();
        for (int id = 1; id <= totalFraudOrders; id++)
        {
            if (Score(id) >= 90) seqFlagged.Add(id);
        }
        swSeq.Stop();

        
        var swPar = Stopwatch.StartNew();
        var parFlagged = new ConcurrentBag<int>();
        var distinctThreads = new ConcurrentDictionary<int, byte>();

        Parallel.For(1, totalFraudOrders + 1, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = ct }, id =>
        {
            distinctThreads.TryAdd(Environment.CurrentManagedThreadId, 0);
            if (Score(id) >= 90) parFlagged.Add(id);
        });
        swPar.Stop();

        double speedUp = swSeq.Elapsed.TotalMilliseconds / Math.Max(1, swPar.Elapsed.TotalMilliseconds);
        Log.Success($"Sequential Time: {swSeq.ElapsedMilliseconds} ms | Parallel Time: {swPar.ElapsedMilliseconds} ms | Speed-up: {speedUp:F2}x");
        Log.Info($"Flagged Count: {parFlagged.Count} | Distinct Threads Used: {distinctThreads.Count} | Cores: {Environment.ProcessorCount}");

        // --- Task 10.4 B: Email Re-Send (I/O-Bound) ---
        Log.Info("\n--- Task 10.4 B: Email Re-Send (I/O-Bound) ---");
        int maxConcurrentSends = 0;
        int currentSends = 0;
        object lockObj = new();

        var testEmails = Enumerable.Range(1001, 30).Select(id => new Order { Id = id, CustomerId = 1 }).ToList();

        var swEmail = Stopwatch.StartNew();
        await Parallel.ForEachAsync(testEmails, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct }, async (order, token) =>
        {
            lock (lockObj)
            {
                currentSends++;
                if (currentSends > maxConcurrentSends) maxConcurrentSends = currentSends;
            }

            await emailService.SendConfirmationAsync(order, new Customer { Email = "test@example.com", Name = "Test" }, token);

            lock (lockObj)
            {
                currentSends--;
            }
        });
        swEmail.Stop();

        Log.Success($"All 30 Emails sent in {swEmail.Elapsed.TotalSeconds:F1} s | Max Concurrent Sends Recorded: {maxConcurrentSends}");
    }

    private static int Score(int orderId)
    {
        long h = orderId;
        for (int i = 0; i < 100_000; i++)
        {
            h = (h * 31 + 1) % 1_000_000_007;
        }
        return (int)(h % 100);
    }
}