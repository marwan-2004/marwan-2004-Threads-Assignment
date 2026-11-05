using System.Diagnostics;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public static class Phase1Runner
{
    public static async Task RunAsync(CancellationToken ct = default)
    {
        Log.Info("=== Starting Phase 1 ===");

        // Reset data
        await DataSeeder.ResetAsync(ct);

        var incomingRepo = new IncomingOrderRepository();
        var customerRepo = new CustomerRepository();
        var productRepo = new ProductRepository();
        var couponRepo = new CouponRepository();
        var orderRepo = new OrderRepository();

        var validator = new OrderValidator(customerRepo, productRepo, couponRepo, orderRepo);

        List<Order> orders = await incomingRepo.GetAllAsync(ct);

        // Async Run
        var swAsync = Stopwatch.StartNew();
        foreach (var order in orders)
        {
            await validator.ValidateOrderAsync(order, ct);
        }
        swAsync.Stop();

        // Print Summary read directly from orders.json
        var savedOrders = await orderRepo.GetAllAsync(ct);
        var validated = savedOrders.Where(o => o.Status == OrderStatus.Validated).ToList();
        var rejected = savedOrders.Where(o => o.Status == OrderStatus.Rejected).ToList();

        Console.WriteLine();
        Console.WriteLine("===== Validation Summary =====");
        Console.WriteLine($"Total Orders: {savedOrders.Count}");
        Console.WriteLine($"Validated:    {validated.Count}");
        Console.WriteLine($"Rejected:     {rejected.Count}");

        foreach (var rej in rejected.OrderBy(o => o.Id))
        {
            Console.WriteLine($" - Order #{rej.Id}: {rej.FailureReason}");
        }

        Console.WriteLine($"Async Time: {swAsync.Elapsed.TotalSeconds:F1} seconds");
        Console.WriteLine($"orders.json now contains {savedOrders.Count} records.");
    }
}