using OrderProcessing.Phase1;
using OrderProcessing.Phase2;
using OrderProcessing.Phase3;
using OrderProcessing.Shared.Logging;

namespace OrderProcessing;

public class Program
{
    public static async Task Main(string[] args)
    {
        CancellationTokenSource cts = new();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Log.Error("Run cancelled via Ctrl+C");
        };

        int cancelAfterMs = -1;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--cancel-after" && i + 1 < args.Length && int.TryParse(args[i + 1], out int ms))
            {
                cancelAfterMs = ms;
                cts.CancelAfter(ms);
            }
        }

        string mode = args.FirstOrDefault(a => !a.StartsWith("--")) ?? "menu";

        if (mode == "menu")
        {
            Console.WriteLine("Select Phase to run:");
            Console.WriteLine("1) Phase 1 - Async Data Access Layer");
            Console.WriteLine("2) Phase 2 - Concurrent Processing & Race Conditions");
            Console.WriteLine("3) Phase 3 - Fire-and-Forget Investigation");
            Console.WriteLine("4) Exit");
            Console.Write("Enter choice (1-4): ");

            string? choice = Console.ReadLine();
            mode = choice switch
            {
                "1" => "phase1",
                "2" => "phase2",
                "3" => "phase3",
                _ => "exit"
            };
        }

        try
        {
            switch (mode.ToLower())
            {
                case "phase1":
                    await Phase1Runner.RunAsync(cts.Token);
                    break;
                case "phase2":
                    await Phase2Runner.RunAsync(cts.Token);
                    break;
                case "phase3":
                    await Phase3Runner.RunAsync(cts.Token);
                    break;

                case "exit":
                    return;
                default:
                    Log.Error($"Unknown mode: {mode}");
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            Log.Error("Operation was canceled.");
        }
    }
}