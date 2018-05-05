using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LyndaDecryptor
{
    public static class Utils
    {
        private static ConsoleColor color_default;
        private static object console_lock = new object();

        public static string ENCRYPTION_KEY = "~h#\x00b0" + new string(new char[] { '\'', '*', '\x00b2', '"', 'C', '\x00b4', '|', '\x00a7', '\\' }) + "3~.";

        static Utils()
        {
            color_default = Console.ForegroundColor;
        }

        public static void WriteToConsole(string Text, ConsoleColor color = ConsoleColor.Gray)
        {
            lock (console_lock)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(Text);
                Console.ForegroundColor = color_default;
            }
        }

        public static DecryptorOptions ParseCommandLineArgs(string[] args)
        {
            var options = new DecryptorOptions();
            int index = 0;
            int length = args.Length;

            foreach (string arg in args)
            {
                if (string.IsNullOrWhiteSpace(arg))
                {
                    index++;
                    continue;
                }

                switch (arg.ToUpper())
                {
                    case "/D": // Directory Mode
                        if (length - 1 > index && Directory.Exists(args[index + 1]))
                        {
                            options.InputPath = args[index + 1];
                            options.UsageMode = Mode.Folder;
                            WriteToConsole("[ARGS] Changing mode to Folder decryption!", ConsoleColor.Yellow);
                        }
                        else
                        {
                            WriteToConsole("[ARGS] The directory path is missing..." + Environment.NewLine, ConsoleColor.Red);
                            throw new FileNotFoundException("Directory path is missing or specified directory was not found!");
                        }
                        break;

                    case "/F": // File Mode
                        if (length - 1 > index && File.Exists(args[index + 1]))
                        {
                            options.InputPath = args[index + 1];

                            if (length - 1 > index + 1 && !string.IsNullOrWhiteSpace(args[index + 2]))
                            {
                                if (File.Exists(args[index + 2]))
                                    throw new IOException("File already exists: " + args[index + 2]);

                                options.OutputPath = args[index + 2];
                            }
                            else
                                throw new FormatException("Output file path is missing...");

                            options.UsageMode = Mode.File;
                            WriteToConsole("[ARGS] Changing mode to Single decryption!", ConsoleColor.Yellow);
                        }
                        else
                        {
                            throw new FileNotFoundException("Input file is missing or not specified!");
                        }
                        break;

                    case "/DB": // Use Database
                        options.UseDatabase = true;

                        if (length - 1 > index && File.Exists(args[index + 1]))
                            options.DatabasePath = args[index + 1];
                        break;

                    case "/RM": // Remove encrypted files after decryption
                        options.RemoveFilesAfterDecryption = true;
                        break;

                    case "/OUT":
                        options.UseOutputFolder = true;

                        if (args.Length - 1 > index)
                            options.OutputFolder = args[index + 1];
                        break;
                }

                index++;
            }

            return options;
        }
    }
}
