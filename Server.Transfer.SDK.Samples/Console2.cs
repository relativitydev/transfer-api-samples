// ----------------------------------------------------------------------------
// <copyright file="Console2.cs" company="Relativity ODA LLC">
//   © Relativity All Rights Reserved.
// </copyright>
// ----------------------------------------------------------------------------

namespace Relativity.Server.Transfer.SDK.Samples
{
    using System;

    /// <summary>
    /// Represents extensions to the <see cref="Console"/> class to write headers and write colored text.
    /// </summary>
    public static class Console2
    {
        private static ConsoleColor DefaultConsoleColor = ConsoleColor.Gray;

        public static void Initialize()
        {
            DefaultConsoleColor = Console.ForegroundColor;
        }

        public static void WriteStartHeader(string message)
        {
            int bannerLength = Console.WindowWidth - 6;
            string asterisks = new string('*', (bannerLength - message.Length - 1) / 2);
            message = $"{asterisks} {message} {asterisks}".Substring(0, bannerLength);
            WriteLine(string.Empty);
            WriteLine(DefaultConsoleColor, message);
        }

        public static void WriteEndHeader()
        {
            int bannerLength = Console.WindowWidth - 6;
            string asterisks = new string('*', bannerLength);
            string message = $"{asterisks}{asterisks}".Substring(0, bannerLength);
            WriteLine(DefaultConsoleColor, message);
        }

        public static void WriteLine()
        {
            WriteLine(string.Empty);
        }

        public static void WriteLine(string format, params object[] args)
        {
            WriteLine(ConsoleColor.White, format, args);
        }

        public static void WriteLine(ConsoleColor color, string format, params object[] args)
        {
            WriteLine(color, string.Format(format, args));
        }

        public static void WriteLine(string message)
        {
            WriteLine(ConsoleColor.White, message);
        }

        public static void WriteLine(ConsoleColor color, string message)
        {
            ConsoleColor existingColor = Console.ForegroundColor;

            try
            {
                Console.ForegroundColor = color;
                Console.WriteLine(message);
            }
            finally
            {
                Console.ForegroundColor = existingColor;
            }
        }

        public static void WriteTerminateLine(int exitCode)
        {
            Console2.WriteLine();
            Console2.WriteLine(
                exitCode == 0
                    ? "The sample successfully completed. Exit code: {0}"
                    : "The sample failed to complete. Exit code: {0}",
                exitCode);
            Console2.WriteLine("Press any key to terminate.");
            Console.ReadLine();
        }
    }
}