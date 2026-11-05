using OrderProcessing.Phase1;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public class InvoiceGenerator
{
    public async Task GenerateAsync(Order order, CancellationToken ct = default)
    {
        // CPU-bound rendering simulation inside Task.Run
        await Task.Run(() =>
        {
            double sum = 0;
            for (int i = 0; i < 20_000_000; i++)
            {
                sum += Math.Sqrt(i);
            }
        }, ct);

        string invoicePath = Path.Combine(DataFiles.InvoicesFolder, $"invoice_{order.Id}.txt");
        string content = $"Invoice for Order #{order.Id}\nTotal: {order.TotalAmount:C}";
        await File.WriteAllTextAsync(invoicePath, content, ct);
    }
}

public class ReportGenerator
{
    private readonly OrderRepository _orderRepo;
    private readonly ProductRepository _productRepo;

    public ReportGenerator(OrderRepository orderRepo, ProductRepository productRepo)
    {
        _orderRepo = orderRepo;
        _productRepo = productRepo;
    }

    public async Task<Report> GenerateAsync(CancellationToken ct = default)
    {
        var orders = await _orderRepo.GetAllAsync(ct);

        var completed = orders.Where(o => o.Status == OrderStatus.Completed).ToList();
        var rejected = orders.Where(o => o.Status == OrderStatus.Rejected).ToList();
        var failed = orders.Where(o => o.Status == OrderStatus.Failed).ToList();
        var paymentFailed = orders.Where(o => o.Status == OrderStatus.PaymentFailed).ToList();

        decimal totalRevenue = completed.Sum(o => o.TotalAmount);
        decimal avgValue = completed.Count > 0 ? totalRevenue / completed.Count : 0m;

        // Top Selling Product
        var topProductGroup = completed
            .SelectMany(o => o.Items)
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, TotalQty = g.Sum(x => x.Quantity) })
            .OrderByDescending(x => x.TotalQty)
            .ThenBy(x => x.ProductId)
            .FirstOrDefault();

        string topProductName = "None";
        int topQty = 0;

        if (topProductGroup != null)
        {
            var prod = await _productRepo.GetByIdAsync(topProductGroup.ProductId, ct);
            topProductName = prod?.Name ?? "Unknown";
            topQty = topProductGroup.TotalQty;
        }

        var report = new Report
        {
            TotalOrders = orders.Count,
            TotalRevenue = Math.Round(totalRevenue, 2),
            CompletedCount = completed.Count,
            RejectedCount = rejected.Count,
            FailedCount = failed.Count,
            PaymentFailedCount = paymentFailed.Count,
            TopSellingProduct = topProductName,
            TopSellingQuantity = topQty,
            AverageOrderValue = Math.Round(avgValue, 2),
            GeneratedAt = DateTime.Now
        };

        string reportPath = Path.Combine(DataFiles.ReportsFolder, $"report_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        await JsonFileStore.WriteAsync(reportPath, new List<Report> { report }, ct);

        return report;
    }
}