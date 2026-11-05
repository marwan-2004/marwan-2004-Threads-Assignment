using OrderProcessing.Phase1;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase2;

public class PricingService
{
    private readonly CustomerRepository _customerRepo;
    private readonly ProductRepository _productRepo;
    private readonly CouponRepository _couponRepo;
    private readonly HashSet<int> _failShippingForOrderIds;

    public PricingService(
        CustomerRepository customerRepo,
        ProductRepository productRepo,
        CouponRepository couponRepo,
        IEnumerable<int> failShippingForOrderIds)
    {
        _customerRepo = customerRepo;
        _productRepo = productRepo;
        _couponRepo = couponRepo;
        _failShippingForOrderIds = new HashSet<int>(failShippingForOrderIds);
    }

    public async Task<decimal> CalculateSubTotalAsync(Order order, CancellationToken ct = default)
    {
        Log.Info($"[Thread {Environment.CurrentManagedThreadId}] PricingService -> DB query for SubTotal Order #{order.Id}...");
        await Task.Delay(200, ct);
        await Task.Delay(300, ct);

        decimal subTotal = 0;
        foreach (var item in order.Items)
        {
            var product = await _productRepo.GetByIdAsync(item.ProductId, ct);
            if (product != null)
            {
                item.UnitPrice = product.Price;
                subTotal += item.UnitPrice * item.Quantity;
            }
        }
        return Math.Round(subTotal, 2, MidpointRounding.AwayFromZero);
    }

    public async Task<decimal> GetDiscountAsync(Order order, CancellationToken ct = default)
    {
        Log.Info($"[Thread {Environment.CurrentManagedThreadId}] PricingService -> Discount API for Order #{order.Id}...");
        await Task.Delay(150, ct);
        await Task.Delay(300, ct);
        await Task.Delay(500, ct);

        var customer = await _customerRepo.GetByIdAsync(order.CustomerId, ct);
        int loyaltyPercent = customer?.LoyaltyTier switch
        {
            LoyaltyTier.Silver => 5,
            LoyaltyTier.Gold => 10,
            _ => 0
        };

        int couponPercent = 0;
        if (!string.IsNullOrWhiteSpace(order.CouponCode))
        {
            var coupon = await _couponRepo.GetByCodeAsync(order.CouponCode, ct);
            if (coupon != null && coupon.ExpiryDate >= DateTime.Today)
            {
                couponPercent = coupon.DiscountPercent;
            }
        }

        int totalDiscountPercent = Math.Min(30, loyaltyPercent + couponPercent);
        decimal discountAmount = order.SubTotal * totalDiscountPercent / 100m;
        return Math.Round(discountAmount, 2, MidpointRounding.AwayFromZero);
    }

    public async Task<decimal> CalculateShippingCostAsync(Order order, CancellationToken ct = default)
    {
        Log.Info($"[Thread {Environment.CurrentManagedThreadId}] PricingService -> Shipping API for Order #{order.Id}...");
        await Task.Delay(150, ct);

        if (_failShippingForOrderIds.Contains(order.Id))
        {
            throw new ShippingProviderUnavailableException("Shipping provider unavailable");
        }

        await Task.Delay(300, ct);
        await Task.Delay(500, ct);

        if (order.SubTotal >= 1000m)
            return 0m;

        int totalQty = order.Items.Sum(i => i.Quantity);
        decimal totalWeight = 0m;

        foreach (var item in order.Items)
        {
            var product = await _productRepo.GetByIdAsync(item.ProductId, ct);
            if (product != null)
            {
                totalWeight += product.WeightKg * item.Quantity;
            }
        }

        decimal shipping = 5.00m + (1.50m * totalQty) + (2.00m * totalWeight);
        return Math.Round(shipping, 2, MidpointRounding.AwayFromZero);
    }
}