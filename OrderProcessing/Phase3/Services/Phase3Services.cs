using OrderProcessing.Phase1;
using OrderProcessing.Phase2;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3.Services;

public class CustomerService
{
    private readonly CustomerRepository _customerRepo;

    public CustomerService(CustomerRepository customerRepo)
    {
        _customerRepo = customerRepo;
    }

    public async Task<Customer> GetCustomerAsync(int id, CancellationToken ct = default)
    {
        var customer = await _customerRepo.GetByIdAsync(id, ct);
        if (customer == null)
            throw new CustomerNotFoundException($"Customer #{id} not found");

        return customer;
    }
}

public class InventoryService
{
    private readonly ProductRepository _productRepo;

    public InventoryService(ProductRepository productRepo)
    {
        _productRepo = productRepo;
    }

    public async Task<bool> ReserveStockAsync(Order order, CancellationToken ct = default)
    {
        List<int> reservedProductIds = new();
        foreach (var item in order.Items)
        {
            bool success = await _productRepo.ReserveStockAsync(item.ProductId, item.Quantity, ct);
            if (!success)
            {
                
                foreach (var resId in reservedProductIds)
                {
                    var itemToRelease = order.Items.First(i => i.ProductId == resId);
                    var prod = await _productRepo.GetByIdAsync(resId, ct);
                    if (prod != null)
                    {
                        prod.StockQuantity += itemToRelease.Quantity;
                    }
                }
                return false;
            }
            reservedProductIds.Add(item.ProductId);
        }
        return true;
    }

    public async Task ReleaseStockAsync(Order order, CancellationToken ct = default)
    {
        Log.Info($"InventoryService -> Releasing stock for Order #{order.Id}...");
        var products = await JsonFileStore.ReadAsync<Product>(DataFiles.Products, ct);

        foreach (var item in order.Items)
        {
            var prod = products.FirstOrDefault(p => p.Id == item.ProductId);
            if (prod != null)
            {
                prod.StockQuantity += item.Quantity;
            }
        }
        await JsonFileStore.WriteAsync(DataFiles.Products, products, ct);
    }
}

public class Phase3OrderRepository
{
    private readonly HashSet<int> _failSaveForOrderIds;

    public Phase3OrderRepository(IEnumerable<int>? failSaveForOrderIds = null)
    {
        _failSaveForOrderIds = new HashSet<int>(failSaveForOrderIds ?? Array.Empty<int>());
    }

    public async Task SaveAsync(Order order, CancellationToken ct = default)
    {
        Log.Info($"OrderRepository -> Saving Order #{order.Id} with Status '{order.Status}'...");
        await Task.Delay(200, ct);
        await Task.Delay(100, ct);

        if (order.Status == OrderStatus.Completed && _failSaveForOrderIds.Contains(order.Id))
        {
            throw new DatabaseWriteException("Connection lost during commit");
        }

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
}