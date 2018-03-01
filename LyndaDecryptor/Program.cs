using System;

using static LyndaDecryptor.Utils;

namespace LyndaDecryptor
{
    public enum Mode
    {
        None = 0,
        File,
        Folder
    };

    public class Program
    {
        static void Main(string[] args)
        {
            Decryptor decryptor;
            var decryptorOptions = new DecryptorOptions();

            try
            {
                decryptorOptions = ParseCommandLineArgs(args);
                decryptor = new Decryptor(decryptorOptions);

                if (decryptorOptions.UsageMode == Mode.None)
                {
                    Usage();
                    goto End;
                }
                else if (decryptorOptions.RemoveFilesAfterDecryption)
                {
                    WriteToConsole("[ARGS] Removing files after decryption." + Environment.NewLine, ConsoleColor.Yellow);
                    WriteToConsole("[ARGS] Press any key to continue or CTRL + C to break..." + Environment.NewLine, ConsoleColor.Yellow);
                    Console.ReadKey();
                }

                decryptor.InitDecryptor(ENCRYPTION_KEY);


                if (decryptorOptions.UsageMode == Mode.Folder)
                    decryptor.DecryptAll(decryptorOptions.InputPath, decryptorOptions.OutputFolder);
                else if (decryptorOptions.UsageMode == Mode.File)
                    decryptor.Decrypt(decryptorOptions.InputPath, decryptorOptions.OutputPath);
            }
            catch (Exception e)
            {
                WriteToConsole("[START] Error occured: " + e.Message + Environment.NewLine, ConsoleColor.Red);
                Usage();
            }
            End:
            WriteToConsole(Environment.NewLine + "Press any key to exit the program...");
            Console.ReadKey();
        }

        /// <summary>
        /// Print usage instructions
        /// </summary>
        static void Usage()
        {
            Console.WriteLine("Usage (Directory):   LyndaDecryptor /D PATH_TO_FOLDER [OPTIONS]");
            Console.WriteLine("Usage (File): LyndaDecryptor /F ENCRYPTED_FILE   DECRYPTED_FILE [OPTIONS]");

            Console.WriteLine(Environment.NewLine + Environment.NewLine + "Flags: ");
            Console.WriteLine("\t/D\tSource files are located in a folder.");
            Console.WriteLine("\t/F\tSource and Destination file are specified.");
            Console.WriteLine("\t/DB [PATH]\tSearch for Database or specify the location on your system.");
            Console.WriteLine("\t/RM\tRemoves all files after decryption is complete.");
            Console.WriteLine("\t/OUT [PATH]\tSpecifies an output directory instead of using default directory.");
        }
    }
}
