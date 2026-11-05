using System;
using System.Collections.Generic;
using System.Text;

namespace OrderProcessing.Shared.Logging
{
    public static class Log
    {
        private static readonly object _consoleLock = new();

        public static void Info(string message)
        {
            WriteLine(message, ConsoleColor.White);
        }
        public static void Success(string message)
        {
            WriteLine(message, ConsoleColor.Green);
        }
        public static void Error(string message)
        {
            WriteLine(message, ConsoleColor.Red);
        }

        private static void WriteLine(string message , ConsoleColor color)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            int threadId = Environment.CurrentManagedThreadId;
            string formatted = $"[{timestamp}] [Thread {threadId}] {message}";

            lock (_consoleLock)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(formatted);
                Console.ResetColor();
            }
        }
    }
}
