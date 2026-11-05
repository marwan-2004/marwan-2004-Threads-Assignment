using OrderProcessing.Phase1;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase4;

// --- Task 10.2: Customer Dashboard Service ---
public class CustomerDashboardService
{
    private readonly HashSet<int> _failPaymentsForCustomerIds;

    public CustomerDashboardService(IEnumerable<int>? failPaymentsForCustomerIds = null)
    {
        _failPaymentsForCustomerIds = new HashSet<int>(failPaymentsForCustomerIds ?? Array.Empty<int>());
    }

    public async Task<CustomerDashboard> LoadAsync(int customerId, CancellationToken ct = default)
    {
        var customerTask = LoadCustomerAsync(customerId, ct);
        var ordersTask = LoadOrdersAsync(customerId, ct);
        var paymentsTask = LoadPaymentsAsync(customerId, ct);
        var emailsTask = LoadEmailsAsync(customerId, ct);

        var allTasks = Task.WhenAll(customerTask, ordersTask, paymentsTask, emailsTask);

        try
        {
            await allTasks;
        }
        catch
        {
            
            if (allTasks.Exception != null)
            {
                throw allTasks.Exception; 
            }
            throw;
        }

        return new CustomerDashboard
        {
            Customer = await customerTask,
            Orders = await ordersTask,
            Payments = await paymentsTask,
            Emails = await emailsTask
        };
    }

    private async Task<Customer> LoadCustomerAsync(int customerId, CancellationToken ct)
    {
        await Task.Delay(200, ct);
        await Task.Delay(400, ct);
        var customers = await JsonFileStore.ReadAsync<Customer>(DataFiles.Customers, ct);
        var customer = customers.FirstOrDefault(c => c.Id == customerId);
        if (customer == null) throw new CustomerNotFoundException($"Customer #{customerId} not found");
        return customer;
    }

    private async Task<List<Order>> LoadOrdersAsync(int customerId, CancellationToken ct)
    {
        await Task.Delay(200, ct);
        await Task.Delay(300, ct);
        var orders = await JsonFileStore.ReadAsync<Order>(DataFiles.Orders, ct);
        return orders.Where(o => o.CustomerId == customerId).ToList();
    }

    private async Task<List<PaymentTransaction>> LoadPaymentsAsync(int customerId, CancellationToken ct)
    {
        await Task.Delay(200, ct);
        await Task.Delay(300, ct);

        if (_failPaymentsForCustomerIds.Contains(customerId))
        {
            throw new TimeoutException("Payments store timeout");
        }

        var orders = await JsonFileStore.ReadAsync<Order>(DataFiles.Orders, ct);
        var customerOrderIds = orders.Where(o => o.CustomerId == customerId).Select(o => o.Id).ToHashSet();

        var payments = await JsonFileStore.ReadAsync<PaymentTransaction>(DataFiles.Payments, ct);
        return payments.Where(p => customerOrderIds.Contains(p.OrderId)).ToList();
    }

    private async Task<List<EmailMessage>> LoadEmailsAsync(int customerId, CancellationToken ct)
    {
        await Task.Delay(200, ct);
        await Task.Delay(300, ct);
        var emails = await JsonFileStore.ReadAsync<EmailMessage>(DataFiles.Emails, ct);
        var customer = (await JsonFileStore.ReadAsync<Customer>(DataFiles.Customers, ct)).FirstOrDefault(c => c.Id == customerId);
        if (customer == null) return new List<EmailMessage>();
        return emails.Where(e => e.To.Equals(customer.Email, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}

// --- Task 10.3: Shipping Carriers ---
public interface IShippingCarrier
{
    string Name { get; }
    Task<decimal> GetQuoteAsync(Order order, CancellationToken ct = default);
}

public class ShippingCarrier : IShippingCarrier
{
    public string Name { get; }
    private readonly int _minDelay;
    private readonly int _maxDelay;
    private readonly decimal _priceFactor;
    private readonly HashSet<int> _failForOrderIds;

    public ShippingCarrier(string name, int minDelay, int maxDelay, decimal priceFactor, IEnumerable<int>? failForOrderIds = null)
    {
        Name = name;
        _minDelay = minDelay;
        _maxDelay = maxDelay;
        _priceFactor = priceFactor;
        _failForOrderIds = new HashSet<int>(failForOrderIds ?? Array.Empty<int>());
    }

    public async Task<decimal> GetQuoteAsync(Order order, CancellationToken ct = default)
    {
        await Task.Delay(150, ct); 

        if (_failForOrderIds.Contains(order.Id))
        {
            throw new ShippingProviderUnavailableException($"{Name} unavailable for order #{order.Id}");
        }

        int delay = Random.Shared.Next(_minDelay, _maxDelay + 1);
        await Task.Delay(delay, ct);

        int totalQty = order.Items.Sum(i => i.Quantity);
        decimal basePrice = 5.00m + (1.50m * totalQty);
        return Math.Round(basePrice * _priceFactor, 2, MidpointRounding.AwayFromZero);
    }
}

public class ShippingQuoteService
{
    private readonly List<IShippingCarrier> _carriers;

    public ShippingQuoteService(List<IShippingCarrier> carriers)
    {
        _carriers = carriers;
    }

    public async Task<ShippingQuote> GetFastestQuoteAsync(Order order, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1200));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        var pendingTasks = _carriers.Select(async carrier =>
        {
            try
            {
                decimal price = await carrier.GetQuoteAsync(order, linkedCts.Token);
                return (Carrier: carrier.Name, Price: price, Success: true, Ex: (Exception?)null);
            }
            catch (Exception ex)
            {
                return (Carrier: carrier.Name, Price: 0m, Success: false, Ex: ex);
            }
        }).ToList();

        while (pendingTasks.Count > 0)
        {
            var completedTask = await Task.WhenAny(pendingTasks);
            pendingTasks.Remove(completedTask);

            var result = await completedTask;
            if (result.Success)
            {
                linkedCts.Cancel(); 
                sw.Stop();
                return new ShippingQuote(result.Carrier, result.Price, sw.Elapsed.TotalMilliseconds);
            }
        }

        sw.Stop();
        if (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            return new ShippingQuote("Flat rate", 25.00m, sw.Elapsed.TotalMilliseconds);
        }

        throw new ShippingProviderUnavailableException("All carriers failed");
    }
}