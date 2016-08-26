using System;
using System.Data.SQLite;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Threading;

namespace LyndaDecryptor
{
    [Flags]
    enum Mode
    {
        None = 1,
        Single = 2,
        Folder = 4,
        DB_Usage = 8,
        RemoveFiles = 16,
        SpecialOutput = 32
    };

    class Program
    {
        static List<char> invalidPathChars = new List<char>(), invalidFileChars = new List<char>();
        static char[] partTwo = new char[] { '\'', '*', '\x00b2', '"', 'C', '\x00b4', '|', '\x00a7', '\\' };
        static string ENCRYPTION_KEY = "~h#\x00b0" + new string(partTwo) + "3~.";
        static SQLiteConnection sqlite_db_connection;

        static RijndaelManaged rijndael;
        static byte[] enc_key_bytes;

        static ConsoleColor color_default = Console.ForegroundColor;
        static Mode usage_mode = Mode.None;

        static string directory, file_source, file_destination, db_path = string.Empty, out_path = string.Empty;
        static object console_lock = new object();
        static SemaphoreSlim semaphore = new SemaphoreSlim(5);
        static object sem_lock = new object();
        

        static void Main(string[] args)
        {
            try
            {
                invalidPathChars.AddRange(Path.GetInvalidPathChars());
                invalidPathChars.AddRange(new char[] { ':', '?', '"', '\\', '/' });
                invalidFileChars.AddRange(Path.GetInvalidFileNameChars());
                invalidFileChars.AddRange(new char[] { ':', '?', '"', '\\', '/' });

                int arg_index = 0;

                foreach (string arg in args)
                {
                    if (string.IsNullOrWhiteSpace(arg))
                    {
                        arg_index++;
                        continue;
                    }

                    switch (arg.ToUpper())
                    {
                        case "/D":
                            if (Directory.Exists(args[arg_index + 1]))
                            {
                                directory = args[arg_index + 1];
                                usage_mode = Mode.Folder;
                                WriteToConsole("[ARGS] Changing mode to Folder decryption!", ConsoleColor.Yellow);
                            }
                            else
                            {
                                WriteToConsole("[ARGS] The directory path is missing..." + Environment.NewLine, ConsoleColor.Red);
                                Usage();
                                goto End;
                            }
                            break;

                        case "/F":
                            if (File.Exists(args[arg_index + 1]) && !string.IsNullOrWhiteSpace(args[arg_index + 2]) && !File.Exists(args[arg_index + 2]))
                            {
                                file_source = args[arg_index + 1];
                                file_destination = args[arg_index + 2];
                                usage_mode = Mode.Single;
                                WriteToConsole("[ARGS] Changing mode to Single decryption!", ConsoleColor.Yellow);

                            }
                            else
                            {
                                WriteToConsole("[ARGS] Some relevant args are missing..." + Environment.NewLine, ConsoleColor.Red);
                                Usage();
                                goto End;
                            }
                            break;

                        case "/DB":
                            usage_mode |= Mode.DB_Usage;

                            if (args.Length-1 > arg_index && File.Exists(args[arg_index + 1]))
                                db_path = args[arg_index + 1];
                            break;

                        case "/RM":
                            usage_mode |= Mode.RemoveFiles;
                            WriteToConsole("[ARGS] Removing files after decryption..." + Environment.NewLine, ConsoleColor.Yellow);
                            WriteToConsole("[ARGS] Press any key to continue..." + Environment.NewLine, ConsoleColor.Yellow);
                            Console.ReadKey();
                            break;

                        case "/OUT":
                            usage_mode |= Mode.SpecialOutput;

                            if (args.Length - 1 > arg_index)
                                out_path = args[arg_index + 1];
                            break;
                    }

                    arg_index++;
                }

                if((usage_mode & Mode.None) == Mode.None)
                {
                    Usage();
                    goto End;
                }
                else
                    InitDecryptor();

                if ((usage_mode & Mode.DB_Usage) == Mode.DB_Usage)
                    InitDB();

                if ((usage_mode & Mode.Folder) == Mode.Folder)
                    DecryptAll(directory, out_path, (usage_mode & Mode.DB_Usage) == Mode.DB_Usage);
                else if ((usage_mode & Mode.Single) == Mode.Single)
                    Decrypt(file_source, file_destination);


            }
            catch (Exception e)
            {
                WriteToConsole("[START] Error occured: " + e.Message + Environment.NewLine, ConsoleColor.Red);
                Usage();
            }
End:
            WriteToConsole(Environment.NewLine + "Press any key to exit the program...");
            Console.ReadKey();

            if (sqlite_db_connection != null && sqlite_db_connection.State == System.Data.ConnectionState.Open)
                sqlite_db_connection.Close();
        }

        private static void InitDB()
        {
            WriteToConsole("[DB] Creating db connection...");

            if (string.IsNullOrEmpty(db_path))
            {
                var files = Directory.EnumerateFiles(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "lynda.com", "video2brain Desktop App"), "*.sqlite", SearchOption.AllDirectories);

                foreach (string path in files)
                {
                    db_path = path;
                }
            }

            if (!string.IsNullOrEmpty(db_path))
            {
                sqlite_db_connection = new SQLiteConnection($"Data Source={db_path}; Version=3;FailIfMissing=True");
                sqlite_db_connection.Open();

                WriteToConsole("[DB] DB successfully connected and opened!" + Environment.NewLine, ConsoleColor.Green);
            }
            else
                WriteToConsole("[DB] Couldn't find db file!" + Environment.NewLine, ConsoleColor.Red);
        }

        private static void InitDecryptor()
        {
            WriteToConsole("[START] Init Decryptor...");
            rijndael = new RijndaelManaged
            {
                KeySize = 0x80,
                Padding = PaddingMode.Zeros
            };

            enc_key_bytes = new ASCIIEncoding().GetBytes(ENCRYPTION_KEY);

            WriteToConsole("[START] Decryptor successful initalized!" + Environment.NewLine, ConsoleColor.Green);
        }

        static void Usage()
        {
            Console.WriteLine("Usage (Directory):   LyndaDecryptor /D PATH_TO_FOLDER [OPTIONS]");
            Console.WriteLine("Usage (File): LyndaDecryptor /F ENCRYPTED_FILE   DECRYPTED_FILE [OPTIONS]");

            Console.WriteLine(Environment.NewLine + Environment.NewLine + "Flags: ");
            Console.WriteLine("\t/D\tSource files are located in a folder.");
            Console.WriteLine("\t/F\tSource and Destination file are specified.");
            Console.WriteLine("\t/DB\tSearch for Database or specify the location on your system.");
            Console.WriteLine("\t/RM\tRemoves all files after decryption is complete.");
            Console.WriteLine("\t/OUT\tSpecifies an output directory instead of using default directory.");
        }

        static async void DecryptAll(string folderPath, string outputFolder = "", bool useDB = false)
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException();

            List<Task> taskList = new List<Task>();

            foreach (string entry in Directory.EnumerateFiles(folderPath, "*.lynda", SearchOption.AllDirectories))
            {
                string newPath = outputFolder;
                var item = entry;

                if (useDB)
                {
                    DirectoryInfo containingDir;
                    DirectoryInfo info = new DirectoryInfo(Path.GetDirectoryName(item));
                    string videoID = Path.GetFileName(item).Split('_')[0];

                    if (!string.IsNullOrWhiteSpace(outputFolder))
                    {
                        if (Directory.Exists(outputFolder))
                            containingDir = new DirectoryInfo(outputFolder);
                        else
                            containingDir = Directory.CreateDirectory(outputFolder);
                    }
                    else
                        containingDir = info;

                    var cmd = sqlite_db_connection.CreateCommand();

                    cmd.CommandText = "SELECT Video.ID, Video.ChapterId, Video.CourseId, Video.Title, Filename, Video.CourseTitle, Video.SortIndex, Chapter.Title as ChapterTitle, Chapter.SortIndex as ChapterIndex FROM Video " +
                                      "INNER JOIN Chapter ON Video.ChapterId = Chapter.ID " +
                                      $"WHERE Video.CourseId = {info.Name} AND Video.ID = {videoID}";


                    var reader = cmd.ExecuteReader(System.Data.CommandBehavior.Default);

                    if (reader.Read())
                    {
                        // Get course name to create a new directory or use this as the working directory
                        var courseTitle = reader.GetString(reader.GetOrdinal("CourseTitle"));
                        foreach (char invalidChar in invalidPathChars)
                            courseTitle = courseTitle.Replace(invalidChar, '-');

                        // Use the existing directory or create a new folder with the courseTitle
                        if (!Directory.Exists(Path.Combine(containingDir.FullName, courseTitle)))
                            containingDir = containingDir.CreateSubdirectory(courseTitle);
                        else
                            containingDir = new DirectoryInfo(Path.Combine(containingDir.FullName, courseTitle));

                        // Create or use another folder to group by the Chapter
                        var chapterTitle = reader.GetString(reader.GetOrdinal("ChapterTitle"));
                        foreach (char invalidChar in invalidPathChars)
                            chapterTitle = chapterTitle.Replace(invalidChar, '-');

                        var chapterIndex = reader.GetInt32(reader.GetOrdinal("ChapterIndex"));
                        chapterTitle = $"{chapterIndex} - {chapterTitle}";

                        if (!Directory.Exists(Path.Combine(containingDir.FullName, chapterTitle)))
                            containingDir = containingDir.CreateSubdirectory(chapterTitle);
                        else
                            containingDir = new DirectoryInfo(Path.Combine(containingDir.FullName, chapterTitle));

                        var videoIndex = reader.GetInt32(reader.GetOrdinal("SortIndex"));
                        var title = reader.GetString(reader.GetOrdinal("Title"));
                        foreach (char invalidChar in invalidFileChars)
                            title = title.Replace(invalidChar, '-');

                        newPath = Path.Combine(containingDir.FullName, $"E{videoIndex.ToString()} - {title}.mp4");

                        if (newPath.Length > 240)
                            newPath = Path.Combine(containingDir.FullName, $"E{videoIndex.ToString()}.mp4");
                    }
                    else
                    {
                        WriteToConsole("[STATUS] Couldn't find db entry for file: " + item + Environment.NewLine + "[STATUS] Using the default behaviour!", ConsoleColor.DarkYellow);
                        newPath = Path.ChangeExtension(item, ".mp4");
                    }

                    if (!reader.IsClosed)
                        reader.Close();
                }
                else
                {
                    await semaphore.WaitAsync();
                    newPath = Path.ChangeExtension(item, ".mp4");
                }


                semaphore.Wait();
                taskList.Add(Task.Run(() =>
                {
                    Decrypt(item, newPath);
                    lock(sem_lock)
                    {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(taskList);
            WriteToConsole("Decryption completed!", ConsoleColor.DarkGreen);
        }

        static void Decrypt(string encryptedFilePath, string decryptedFilePath)
        {
            if (!File.Exists(encryptedFilePath))
            {
                WriteToConsole("[ERR] Couldn't find encrypted file...", ConsoleColor.Red);
                return;
            }

            FileInfo encryptedFileInfo = new FileInfo(encryptedFilePath);

            if (File.Exists(decryptedFilePath))
            {
                FileInfo decryptedFileInfo = new FileInfo(decryptedFilePath);

                if (decryptedFileInfo.Length == encryptedFileInfo.Length)
                {
                    WriteToConsole("[DEC] File " + decryptedFilePath + " exists already and will be skipped!", ConsoleColor.Yellow);
                    return;
                }
                else
                    WriteToConsole("[DEC] File " + decryptedFilePath + " exists already but seems to differ in size...", ConsoleColor.Blue);

                decryptedFileInfo = null;
            }

            
            byte[] buffer = new byte[0x50000];

            if (encryptedFileInfo.Extension != ".lynda")
            {
                WriteToConsole("[ERR] Couldn't load file: " + encryptedFilePath, ConsoleColor.Red);
                return;
            }

            using (FileStream inStream = new FileStream(encryptedFilePath, FileMode.Open))
            {
                using (CryptoStream decryptionStream = new CryptoStream(inStream, rijndael.CreateDecryptor(enc_key_bytes, enc_key_bytes), CryptoStreamMode.Read))
                using (FileStream outStream = new FileStream(decryptedFilePath, FileMode.Create))
                {
                    WriteToConsole("[DEC] Decrypting file " + encryptedFileInfo.Name + "...");

                    while ((inStream.Length - inStream.Position) >= buffer.Length)
                    {
                        decryptionStream.Read(buffer, 0, buffer.Length);
                        outStream.Write(buffer, 0, buffer.Length);
                    }

                    buffer = new byte[inStream.Length - inStream.Position];
                    decryptionStream.Read(buffer, 0, buffer.Length);
                    outStream.Write(buffer, 0, buffer.Length);
                    outStream.Flush();
                    outStream.Close();

                    WriteToConsole("[DEC] File decryption completed: " + decryptedFilePath, ConsoleColor.DarkGreen);
                }

                inStream.Close();
                buffer = null;
            }

            if((usage_mode & Mode.RemoveFiles) == Mode.RemoveFiles)
                encryptedFileInfo.Delete();

            encryptedFileInfo = null;
        }

        static void WriteToConsole(string Text, ConsoleColor color = ConsoleColor.Gray)
        {
            lock(console_lock)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(Text);
                Console.ForegroundColor = color_default;
            }
        }
    }
}
