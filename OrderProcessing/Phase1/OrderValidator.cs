using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase1;

public class OrderValidator
{
    private readonly CustomerRepository _customerRepo;
    private readonly ProductRepository _productRepo;
    private readonly CouponRepository _couponRepo;
    private readonly OrderRepository _orderRepo;

    public OrderValidator(
        CustomerRepository customerRepo,
        ProductRepository productRepo,
        CouponRepository couponRepo,
        OrderRepository orderRepo)
    {
        _customerRepo = customerRepo;
        _productRepo = productRepo;
        _couponRepo = couponRepo;
        _orderRepo = orderRepo;
    }

    public async Task ValidateOrderAsync(Order order, CancellationToken ct = default)
    {
        // 1. Customer exists
        var customer = await _customerRepo.GetByIdAsync(order.CustomerId, ct);
        if (customer == null)
        {
            order.Status = OrderStatus.Rejected;
            order.FailureReason = $"Customer #{order.CustomerId} not found";
            await _orderRepo.SaveAsync(order, ct);
            return;
        }

        // 2. Customer is active
        if (!customer.IsActive)
        {
            order.Status = OrderStatus.Rejected;
            order.FailureReason = "Customer is inactive";
            await _orderRepo.SaveAsync(order, ct);
            return;
        }

        // 3 & 4. Products exist and Stock check
        foreach (var item in order.Items)
        {
            var product = await _productRepo.GetByIdAsync(item.ProductId, ct);
            if (product == null)
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason = $"Product #{item.ProductId} not found";
                await _orderRepo.SaveAsync(order, ct);
                return;
            }

            if (product.StockQuantity < item.Quantity)
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason = $"Insufficient stock for '{product.Name}'";
                await _orderRepo.SaveAsync(order, ct);
                return;
            }
        }

        // 5 & 6. Coupon validation
        if (!string.IsNullOrWhiteSpace(order.CouponCode))
        {
            var coupon = await _couponRepo.GetByCodeAsync(order.CouponCode, ct);
            if (coupon == null)
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason = $"Coupon '{order.CouponCode}' does not exist";
                await _orderRepo.SaveAsync(order, ct);
                return;
            }

            if (coupon.ExpiryDate < DateTime.Today)
            {
                order.Status = OrderStatus.Rejected;
                order.FailureReason = $"Coupon '{order.CouponCode}' has expired";
                await _orderRepo.SaveAsync(order, ct);
                return;
            }
        }

        order.Status = OrderStatus.Validated;
        await _orderRepo.SaveAsync(order, ct);
    }
}