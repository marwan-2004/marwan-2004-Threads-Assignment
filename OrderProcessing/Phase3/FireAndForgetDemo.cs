using OrderProcessing.Phase1;
using OrderProcessing.Phase3.Services;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase3;

public class FireAndForgetDemo
{
    private readonly OrderRepository _orderRepository = new();
    private readonly EmailService _emailService = new(new Dictionary<int, int> { [1] = 1 });

#pragma warning disable CS4014
    public async Task ProcessOrderBrokenAsync(Order order, Customer customer)
    {
        await _orderRepository.SaveAsync(order);
        _emailService.SendConfirmationAsync(order, customer);
        Console.WriteLine("Order processing finished");
    }
#pragma warning restore CS4014

    public async Task ProcessOrderFixedAsync(Order order, Customer customer)
    {
        await _orderRepository.SaveAsync(order);
        await _emailService.SendConfirmationAsync(order, customer); 
        Console.WriteLine("Order processing finished");
    }
}