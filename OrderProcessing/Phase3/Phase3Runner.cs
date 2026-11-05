using OrderProcessing.Phase1;
using OrderProcessing.Phase2;
using OrderProcessing.Phase3.Services;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public static class Phase3Runner
{
    public static async Task RunAsync(CancellationToken ct = default)
    {
        Log.Info("=== Starting Phase 3 - Full Checkout Workflow ===");

        await DataSeeder.ResetAsync(ct);

        var customerRepo = new CustomerRepository();
        var productRepo = new ProductRepository();
        var couponRepo = new CouponRepository();
        var incomingRepo = new IncomingOrderRepository();

        // Controlled Failures Injection (Task 9.2)
        var customerService = new CustomerService(customerRepo);
        var inventoryService = new InventoryService(productRepo);
        var pricingService = new PricingService(customerRepo, productRepo, couponRepo, Array.Empty<int>());

        var paymentGateway = new PaymentGateway(
            timeoutOrderIds: new[] { 5 },
            declineOrderIds: new[] { 8 }
        );

        var orderRepo = new Phase3OrderRepository(failSaveForOrderIds: new[] { 13 });

        var emailService = new EmailService(failAttempts: new Dictionary<int, int>
        {
            [3] = 2,
            [17] = int.MaxValue
        });

        var processor = new OrderProcessor(
            customerService,
            inventoryService,
            pricingService,
            paymentGateway,
            orderRepo,
            emailService
        );

        var incomingOrders = await incomingRepo.GetAllAsync(ct);

        
        var tasks = incomingOrders.Select(o => processor.ProcessOrderAsync(o, ct)).ToList();
        await Task.WhenAll(tasks);

        
        var finalOrders = await JsonFileStore.ReadAsync<Order>(DataFiles.Orders, ct);

        Console.WriteLine();
        Console.WriteLine("================================ FINAL ORDERS TABLE ================================");
        Console.WriteLine(string.Format("{0,-8} | {1,-15} | {2,-10} | {3}", "Order", "Status", "Total", "Reason"));
        Console.WriteLine(new string('-', 85));

        foreach (var ord in finalOrders.OrderBy(o => o.Id))
        {
            Console.WriteLine(string.Format("{0,-8} | {1,-15} | {2,-10:C} | {3}",
                $"#{ord.Id}", ord.Status, ord.TotalAmount, ord.FailureReason ?? "-"));
        }
    }
}