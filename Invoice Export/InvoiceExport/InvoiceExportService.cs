using System.Diagnostics;

namespace InvoiceExport;

public sealed class InvoiceExportService
{
    readonly OrderDatabase _db;
    readonly CustomerFileStore _customers;
    readonly InvoiceFileWriter _invoices;

    public InvoiceExportService(OrderDatabase db, CustomerFileStore customers, InvoiceFileWriter invoices)
    {
        _db = db;
        _customers = customers;
        _invoices = invoices;
    }

    public async Task ExportAllPendingAsync(CancellationToken ct=default)
    {
        var watch = Stopwatch.StartNew();
        List<Order> orders =await  _db.GetPendingOrdersAsync(ct);

        var tasks = orders.Select(async order =>
        {
            CustomerProfile customer = await _customers.ReadProfileAsync(order.CustomerId, ct);
            string path = await _invoices.WriteInvoiceAsync(order, customer, ct);
            Console.WriteLine($"Wrote {path}");
        });

        await Task.WhenAll(tasks);
        Console.WriteLine($"Export finished in {watch.Elapsed.TotalSeconds:F1} s");
    }
}
