using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3.Services;

public interface IEmailService
{
    Task SendConfirmationAsync(Order order, Customer customer, CancellationToken ct = default);
}

public class EmailService : IEmailService
{
    private readonly Dictionary<int, int> _failAttempts;
    private readonly Dictionary<int, int> _currentAttempts = new();

    public EmailService(Dictionary<int, int>? failAttempts = null)
    {
        _failAttempts = failAttempts ?? new Dictionary<int, int>();
    }

    public async Task SendConfirmationAsync(Order order, Customer customer, CancellationToken ct = default)
    {
        _currentAttempts.TryGetValue(order.Id, out int attempts);
        attempts++;
        _currentAttempts[order.Id] = attempts;

        Log.Info($"Email Service -> Rendering template for Order #{order.Id} (Attempt {attempts})...");
        await Task.Delay(200, ct);

        Log.Info($"Email Service -> Connecting to SMTP for Order #{order.Id}...");
        await Task.Delay(300, ct);

        if (_failAttempts.TryGetValue(order.Id, out int maxFails) && attempts <= maxFails)
        {
            throw new SmtpUnavailableException($"SMTP server unreachable on attempt {attempts}");
        }

        await Task.Delay(500, ct);

        var emailMsg = new EmailMessage
        {
            OrderId = order.Id,
            To = customer.Email,
            Subject = $"Order #{order.Id} Confirmation",
            Body = $"Thank you {customer.Name}, your order total is {order.TotalAmount:C}",
            SentAt = DateTime.Now,
            Attempts = attempts,
            LastError = null
        };

        var emails = await JsonFileStore.ReadAsync<EmailMessage>(DataFiles.Emails, ct);
        emails.Add(emailMsg);
        await JsonFileStore.WriteAsync(DataFiles.Emails, emails, ct);

        Log.Success($"Email sent successfully to {customer.Email} for Order #{order.Id}");
    }
}