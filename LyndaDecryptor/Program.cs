using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace LyndaDecryptor
{
    class Program
    {
        static char[] partTwo = new char[] { '\'', '*', '\x00b2', '"', 'C', '\x00b4', '|', '\x00a7', '\\' };
        static string ENCRYPTION_KEY = "~h#\x00b0" + new string(partTwo) + "3~.";

        static void Main(string[] args)
        {
            //Decrypt(@"M:\v2b_dl_courses_de\2773\-2773_487b8078002e86365e35ec0a4c61bb67.lynda", @"M:\v2b_dl_courses_de\2773\-2773_487b8078002e86365e35ec0a4c61bb67.decrypted");
            //DecryptAll(@"M:\v2b_dl_courses_de\2773");

            try
            {
                if (args.Length > 1)
                {
                    if (File.Exists(args[0]))
                        Decrypt(args[0], args[1]);
                }
                else if (args.Length == 1)
                {
                    if (Directory.Exists(args[0]))
                        DecryptAll(args[0]);
                }
                else
                {
                    Console.WriteLine("Usage (Directory):   LyndaDecryptor PATH_TO_FOLDER");
                    Console.WriteLine("Usage (Single File): LyndaDecryptor ENCRYPTED_FILE   DECRYPTED_FILE");
                }
            }
            catch(Exception e)
            {
                Console.WriteLine("Error occured: " + e.Message);
            }

            Console.ReadKey();
            Console.ForegroundColor = ConsoleColor.White;
        }

        static async void DecryptAll(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException();

            List<Task> taskList = new List<Task>();

            foreach(string entry in Directory.EnumerateFiles(folderPath, "*.lynda", SearchOption.AllDirectories))
            {
                var item = entry;
                taskList.Add(Task.Factory.StartNew(() => Decrypt(item, Path.ChangeExtension(item, ".mp4"))));
            }

            await Task.WhenAll(taskList);
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine("Decryption completed!");
        }

        static void Decrypt(string encryptedFilePath, string decryptedFilePath)
        {
            if (!File.Exists(encryptedFilePath))
                throw new FileNotFoundException();

            FileInfo encryptedFileInfo = new FileInfo(encryptedFilePath);
            byte[] buffer = new byte[0x500000];
            

            if (encryptedFileInfo.Extension != ".lynda")
                throw new FileLoadException();

            using (FileStream inStream = new FileStream(encryptedFilePath, FileMode.Open))
            {
                RijndaelManaged managed = new RijndaelManaged
                {
                    KeySize = 0x80,
                    Padding = PaddingMode.Zeros
                };

                byte[] bytes = new ASCIIEncoding().GetBytes(ENCRYPTION_KEY);

                using (CryptoStream decryptionStream = new CryptoStream(inStream, managed.CreateDecryptor(bytes, bytes), CryptoStreamMode.Read))
                {
                    using (FileStream outStream = new FileStream(decryptedFilePath, FileMode.Create))
                    {
                        Console.WriteLine("Decrypting file" + encryptedFileInfo.Name + "...");
                        while ((inStream.Length - inStream.Position) >= buffer.Length)
                        {
                            decryptionStream.Read(buffer, 0, buffer.Length);
                            outStream.Write(buffer, 0, buffer.Length);
                        }
                        buffer = new byte[inStream.Length - inStream.Position];
                        decryptionStream.Read(buffer, 0, buffer.Length);
                        outStream.Write(buffer, 0, buffer.Length);
                        outStream.Flush();
                        Console.WriteLine("Decryption completed: " + decryptedFilePath);
                    }
                }
            }

        }
    }
}
