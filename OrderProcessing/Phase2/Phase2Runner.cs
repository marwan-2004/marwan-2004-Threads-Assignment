using System.Diagnostics;
using OrderProcessing.Phase1;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public static class Phase2Runner
{
    public static async Task RunAsync(CancellationToken ct = default)
    {
        Log.Info("=== Starting Phase 2 ===");

        // 1. Unsafe Race Condition Demo
        await DataSeeder.ResetAsync(ct);
        await RaceConditionDemo.RunAsync(ct);

        // 2. Pricing Comparison on Order #1
        await DataSeeder.ResetAsync(ct);
        var customerRepo = new CustomerRepository();
        var productRepo = new ProductRepository();
        var couponRepo = new CouponRepository();
        var orderRepo = new OrderRepository();
        var incomingRepo = new IncomingOrderRepository();

        var pricingService = new PricingService(customerRepo, productRepo, couponRepo, new[] { 7 });
        var pricer = new OrderPricer(pricingService);

        var orders = await incomingRepo.GetAllAsync(ct);
        var order1 = orders.First(o => o.Id == 1);

        var swSeq = Stopwatch.StartNew();
        await pricer.PriceOrderSequentialAsync(order1, ct);
        swSeq.Stop();

        var swConc = Stopwatch.StartNew();
        await pricer.PriceOrderAsync(order1, ct);
        swConc.Stop();

        Log.Info($"Pricing Order #1 -> Sequential Time: {swSeq.Elapsed.TotalSeconds:F2}s | Concurrent Time: {swConc.Elapsed.TotalSeconds:F2}s");

        // 3. Validate and Save all orders concurrently
        await DataSeeder.ResetAsync(ct);
        var validator = new OrderValidator(customerRepo, productRepo, couponRepo, orderRepo);

        var validationTasks = orders.Select(o => validator.ValidateOrderAsync(o, ct)).ToList();
        await Task.WhenAll(validationTasks);

        var validatedOrders = await orderRepo.GetByStatusAsync(OrderStatus.Validated, ct);
        var invoiceGen = new InvoiceGenerator();

        // Process Validated Orders Concurrently
        List<Task<bool>> processTasks = new();
        foreach (var ord in validatedOrders)
        {
            processTasks.Add(ProcessValidatedOrderAsync(ord, productRepo, pricer, invoiceGen, orderRepo, ct));
        }

        Log.Info($"Immediately after creation, Task[0] Status: {processTasks[0].Status}");
        await Task.Delay(1000, ct);

        for (int i = 0; i < processTasks.Count; i++)
        {
            Log.Info($"Running Task[{i}] (Order #{validatedOrders[i].Id}) Status: {processTasks[i].Status}");
        }

        for (int i = 0; i < processTasks.Count; i++)
        {
            try
            {
                await processTasks[i];
            }
            catch (Exception ex)
            {
                var failedOrd = validatedOrders[i];
                Log.Error($"Task Order #{failedOrd.Id} Faulted with: {ex.Message}");
                failedOrd.Status = OrderStatus.Failed;
                failedOrd.FailureReason = ex.Message;
                await orderRepo.SaveAsync(failedOrd, ct);
            }
        }

        // Generate Report
        Log.Info("Generating report...");
        var reportGen = new ReportGenerator(orderRepo, productRepo);
        var report = await reportGen.GenerateAsync(ct);
        Log.Success($"Report Created: Top Product = {report.TopSellingProduct} ({report.TopSellingQuantity} units)");
    }

    private static async Task<bool> ProcessValidatedOrderAsync(
        Order order,
        ProductRepository productRepo,
        OrderPricer pricer,
        InvoiceGenerator invoiceGen,
        OrderRepository orderRepo,
        CancellationToken ct)
    {
        foreach (var item in order.Items)
        {
            bool reserved = await productRepo.ReserveStockAsync(item.ProductId, item.Quantity, ct);
            if (!reserved)
            {
                var prod = await productRepo.GetByIdAsync(item.ProductId, ct);
                order.Status = OrderStatus.Rejected;
                order.FailureReason = $"Insufficient stock for '{prod?.Name ?? "Unknown"}'";
                await orderRepo.SaveAsync(order, ct);
                return false;
            }
        }

        await pricer.PriceOrderAsync(order, ct);
        await invoiceGen.GenerateAsync(order, ct);

        order.Status = OrderStatus.Completed;
        await orderRepo.SaveAsync(order, ct);
        return true;
    }
}