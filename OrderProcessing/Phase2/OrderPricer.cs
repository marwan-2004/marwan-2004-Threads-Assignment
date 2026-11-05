using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase2;

public class OrderPricer
{
    private readonly PricingService _pricingService;

    public OrderPricer(PricingService pricingService)
    {
        _pricingService = pricingService;
    }

    public async Task PriceOrderAsync(Order order, CancellationToken ct = default)
    {
        order.SubTotal = await _pricingService.CalculateSubTotalAsync(order, ct);

        // Concurrent execution for Discount & Shipping APIs
        var discountTask = _pricingService.GetDiscountAsync(order, ct);
        var shippingTask = _pricingService.CalculateShippingCostAsync(order, ct);

        await Task.WhenAll(discountTask, shippingTask);

        order.Discount = await discountTask;
        order.ShippingCost = await shippingTask;
        order.TotalAmount = Math.Round(order.SubTotal - order.Discount + order.ShippingCost, 2, MidpointRounding.AwayFromZero);
    }

    public async Task PriceOrderSequentialAsync(Order order, CancellationToken ct = default)
    {
        order.SubTotal = await _pricingService.CalculateSubTotalAsync(order, ct);
        order.Discount = await _pricingService.GetDiscountAsync(order, ct);
        order.ShippingCost = await _pricingService.CalculateShippingCostAsync(order, ct);
        order.TotalAmount = Math.Round(order.SubTotal - order.Discount + order.ShippingCost, 2, MidpointRounding.AwayFromZero);
    }
}