// ===================================================
//  Star Wars: EaW - Localization Tool
//  Created by EMP_UA
// ===================================================

using System;

namespace EaWLocalizationTool.UI
{
    public static class ConsoleHelper
    {
        private static string _lastFile = string.Empty;

        public static void DrawProgressBar(int current, int total, string currentFile, int width = 50)
        {
            if (total == 0) return;

            // UA: Оновлюємо текст лише якщо файл змінився
            // EN: Update text only if the file has changed
            if (_lastFile != currentFile)
            {
                Console.CursorLeft = 0;
                Console.Write(new string(' ', Console.WindowWidth - 1));
                Console.CursorLeft = 0;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($" Processing: {currentFile}");
                Console.ResetColor();
                _lastFile = currentFile;
            }
            else
            {
                Console.CursorTop--; // UA: Повертаємось на рядок вище / EN: Go back one line up
            }

            double percentage = (double)current / total;
            int progressWidth = (int)(percentage * width);

            Console.Write("[");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write(new string('█', progressWidth));
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(new string('░', width - progressWidth));
            Console.ResetColor();
            Console.Write($"] {percentage:P0} ({current}/{total})   ");
        }
    }
}