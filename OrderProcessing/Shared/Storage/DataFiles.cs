using System;
using System.Collections.Generic;
using System.Text;

namespace OrderProcessing.Shared.Storage
{
    public static class DataFiles
    {
        public static string DataFolder => Path.Combine(AppContext.BaseDirectory, "Data");

        public static string Customers => Path.Combine(DataFolder, "customers.json");
        public static string Products => Path.Combine(DataFolder, "products.json");
        public static string Coupons => Path.Combine(DataFolder, "coupons.json");
        public static string IncomingOrders => Path.Combine(DataFolder, "incoming_orders.json");
        public static string Orders => Path.Combine(DataFolder, "orders.json");
        public static string Payments => Path.Combine(DataFolder, "payments.json");
        public static string Emails => Path.Combine(DataFolder, "emails.json");
        public static string EmailRetry => Path.Combine(DataFolder, "email_retry.json");

        public static string InvoicesFolder => Path.Combine(DataFolder, "invoices");
        public static string ReportsFolder => Path.Combine(DataFolder, "reports");
    }
}
