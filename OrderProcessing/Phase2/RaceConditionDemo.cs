using OrderProcessing.Phase1;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public static class RaceConditionDemo
{
    public static async Task ReserveStockUnsafeAsync(int productId, int qty, CancellationToken ct = default)
    {
        await Task.Delay(200, ct); 
        await Task.Delay(100, ct); 

        var products = await JsonFileStore.ReadAsync<Product>(DataFiles.Products, ct);
        var product = products.FirstOrDefault(p => p.Id == productId);

        if (product != null && product.StockQuantity >= qty)
        {
            await Task.Delay(400, ct); 
            product.StockQuantity -= qty;
            await JsonFileStore.WriteAsync(DataFiles.Products, products, ct);
        }
    }

    public static async Task RunAsync(CancellationToken ct = default)
    {
        Log.Info("--- Running Unsafe Race Condition Demo ---");
        var products = await JsonFileStore.ReadAsync<Product>(DataFiles.Products, ct);
        var kb = products.First(p => p.Id == 4);
        kb.StockQuantity = 1;
        await JsonFileStore.WriteAsync(DataFiles.Products, products, ct);

        var task1 = ReserveStockUnsafeAsync(4, 1, ct);
        var task2 = ReserveStockUnsafeAsync(4, 1, ct);

        await Task.WhenAll(task1, task2);

        var updatedProducts = await JsonFileStore.ReadAsync<Product>(DataFiles.Products, ct);
        Log.Error($"Unsafe Stock Result: Keyboard Stock = {updatedProducts.First(p => p.Id == 4).StockQuantity} (Oversold!)");
    }
}