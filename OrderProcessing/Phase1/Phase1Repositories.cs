using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public class CustomerRepository
{
    public async Task<Customer?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        Log.Info($"Customer Repository -> Opening connection for Customer #{id}...");
        await Task.Delay(200, ct);
        Log.Info($"Customer Repository -> Executing query for Customer #{id}...");
        await Task.Delay(400, ct);

        var customers = await JsonFileStore.ReadAsync<Customer>(DataFiles.Customers, ct);
        var customer = customers.FirstOrDefault(c => c.Id == id);
        if (customer != null)
            Log.Success($"Customer Repository -> Customer #{id} found ({customer.Name})");
        return customer;
    }
}

public class ProductRepository
{
    public async Task<Product?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        Log.Info($"Product Repository -> Opening connection for Product #{id}...");
        await Task.Delay(200, ct);
        Log.Info($"Product Repository -> Executing query for Product #{id}...");
        await Task.Delay(400, ct);

        var products = await JsonFileStore.ReadAsync<Product>(DataFiles.Products, ct);
        return products.FirstOrDefault(p => p.Id == id);
    }

    public async Task<bool> ReserveStockAsync(int productId, int qty, CancellationToken ct = default)
    {
        Log.Info($"Product Repository -> Reserving stock for Product #{productId} (Qty: {qty})...");
        await Task.Delay(200, ct); 
        await Task.Delay(100, ct); 

        var products = await JsonFileStore.ReadAsync<Product>(DataFiles.Products, ct);
        var product = products.FirstOrDefault(p => p.Id == productId);

        if (product == null || product.StockQuantity < qty)
        {
            await Task.Delay(200, ct);  
            return false;
        }

        await Task.Delay(400, ct); 
        product.StockQuantity -= qty;
        await JsonFileStore.WriteAsync(DataFiles.Products, products, ct);
        await Task.Delay(200, ct); 

        return true;
    }
}

public class CouponRepository
{
    public async Task<Coupon?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        Log.Info($"Coupon Repository -> Opening connection for Coupon '{code}'...");
        await Task.Delay(200, ct);
        Log.Info($"Coupon Repository -> Executing query for Coupon '{code}'...");
        await Task.Delay(300, ct);

        var coupons = await JsonFileStore.ReadAsync<Coupon>(DataFiles.Coupons, ct);
        return coupons.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));
    }
}

public class OrderRepository
{
    public async Task SaveAsync(Order order, CancellationToken ct = default)
    {
        Log.Info($"Order Repository -> Saving Order #{order.Id}...");
        await Task.Delay(200, ct); 
        await Task.Delay(100, ct); 
        await Task.Delay(400, ct); 

        var orders = await JsonFileStore.ReadAsync<Order>(DataFiles.Orders, ct);
        int index = orders.FindIndex(o => o.Id == order.Id);
        if (index >= 0)
            orders[index] = order;
        else
            orders.Add(order);

        await JsonFileStore.WriteAsync(DataFiles.Orders, orders, ct);
        await Task.Delay(200, ct); 
    }

    public async Task<List<Order>> GetAllAsync(CancellationToken ct = default)
    {
        await Task.Delay(200, ct);
        await Task.Delay(300, ct);
        return await JsonFileStore.ReadAsync<Order>(DataFiles.Orders, ct);
    }

    public async Task<List<Order>> GetByStatusAsync(OrderStatus s, CancellationToken ct = default)
    {
        var orders = await GetAllAsync(ct);
        return orders.Where(o => o.Status == s).ToList();
    }
}

public class IncomingOrderRepository
{
    public async Task<List<Order>> GetAllAsync(CancellationToken ct = default)
    {
        await Task.Delay(200, ct);
        await Task.Delay(300, ct);
        return await JsonFileStore.ReadAsync<Order>(DataFiles.IncomingOrders, ct);
    }
}