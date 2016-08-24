using System;
using System.Data.SQLite;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace LyndaDecryptor
{
    [Flags]
    enum Mode
    {
        None,
        Single,
        Folder,
        DB_Usage
    };

    class Program
    {
        static char[] partTwo = new char[] { '\'', '*', '\x00b2', '"', 'C', '\x00b4', '|', '\x00a7', '\\' };
        static string ENCRYPTION_KEY = "~h#\x00b0" + new string(partTwo) + "3~.";
        static SQLiteConnection sqlite_db_connection;

        static RijndaelManaged rijndael;
        static byte[] enc_key_bytes;
        static ICryptoTransform decryptor;

        static ConsoleColor color_default = Console.ForegroundColor;
        static Mode usage_mode = Mode.None;

        static string directory, file_source, file_destination, db_path = string.Empty;
        static object console_lock = new object();
        

        static void Main(string[] args)
        {
            try
            {
                

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
                            }
                            else
                            {
                                Usage();
                                return;
                            }
                            break;

                        case "/F":
                            if (File.Exists(args[arg_index + 1]) && !string.IsNullOrWhiteSpace(args[arg_index + 2]) && !File.Exists(args[arg_index + 2]))
                            {
                                file_source = args[arg_index + 1];
                                file_destination = args[arg_index + 2];
                                usage_mode = Mode.Single;
                            }
                            else
                            {
                                Usage();
                                return;
                            }
                            break;

                        case "/DB":
                            usage_mode |= Mode.DB_Usage;

                            if (args.Length-1 > arg_index && File.Exists(args[arg_index + 1]))
                                db_path = args[arg_index + 1];
                            break;
                    }

                    arg_index++;
                }

                if(usage_mode.HasFlag(Mode.None))
                {
                    Usage();
                    goto End;
                }
                else
                    InitDecryptor();

                if (usage_mode.HasFlag(Mode.DB_Usage))
                    InitDB();

                if (usage_mode.HasFlag(Mode.Folder))
                    DecryptAll(directory, usage_mode.HasFlag(Mode.DB_Usage));
                else if (usage_mode.HasFlag(Mode.Single))
                    Decrypt(file_source, file_destination);


            }
            catch (Exception e)
            {
                WriteToConsole("[START] Error occured: " + e.Message + Environment.NewLine, ConsoleColor.Red);
                Usage();
            }
            finally
            {
                if (sqlite_db_connection != null && sqlite_db_connection.State == System.Data.ConnectionState.Open)
                    sqlite_db_connection.Close();
            }

End:
            WriteToConsole(Environment.NewLine + "Press any key to exit the program...");
            Console.ReadKey();
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
            decryptor = rijndael.CreateDecryptor(enc_key_bytes, enc_key_bytes);

            WriteToConsole("[START] Decryptor successful initalized!" + Environment.NewLine, ConsoleColor.Green);
        }

        static void Usage()
        {
            Console.WriteLine("Usage (Directory):   LyndaDecryptor /D PATH_TO_FOLDER");
            Console.WriteLine("Usage (Single File): LyndaDecryptor /F ENCRYPTED_FILE   DECRYPTED_FILE");
            Console.WriteLine("Usage (with Database): LyndaDecryptor /D PATH_TO_FOLDER /DB PATH_TO_DATABASE");

            Console.WriteLine(Environment.NewLine + Environment.NewLine + "Flags: ");
            Console.WriteLine("\t/D\tSource files are located in a folder.");
            Console.WriteLine("\t/F\tSource and Destination file are specified.");
            Console.WriteLine("\t/DB\tSearch for Database or specify the location on your system.");
        }

        static async void DecryptAll(string folderPath, bool useDB = false)
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException();

            List<Task> taskList = new List<Task>();

            foreach(string entry in Directory.EnumerateFiles(folderPath, "*.lynda", SearchOption.AllDirectories))
            {
                var item = entry;

                if(useDB)
                {
                    DirectoryInfo containingDir;
                    DirectoryInfo info = containingDir = new DirectoryInfo(Path.GetDirectoryName(item));
                    string videoID = Path.GetFileName(item).Split('_')[0];

                    var cmd = sqlite_db_connection.CreateCommand();

                    cmd.CommandText = "SELECT Video.ID, Video.ChapterId, Video.CourseId, Video.Title, Filename, Video.CourseTitle, Video.SortIndex, Chapter.Title as ChapterTitle, Chapter.SortIndex as ChapterIndex FROM Video " +
                                      "INNER JOIN Chapter ON Video.ChapterId = Chapter.ID " +
                                      $"WHERE Video.CourseId = {info.Name} AND Video.ID = {videoID}";
                                      

                    var reader = cmd.ExecuteReader(System.Data.CommandBehavior.Default);
                    
                    if(reader.Read())
                    {
                        // Get course name to create a new directory or use this as the working directory
                        var courseTitle = reader.GetString(reader.GetOrdinal("CourseTitle"));
                        foreach (char invalidChar in Path.GetInvalidPathChars())
                            courseTitle = courseTitle.Replace(invalidChar, '-');

                        // Use the existing directory or create a new folder with the courseTitle
                        if (!Directory.Exists(Path.Combine(info.FullName, courseTitle)))
                            containingDir = info.CreateSubdirectory(courseTitle);
                        else
                            containingDir = new DirectoryInfo(Path.Combine(info.FullName, courseTitle));

                        // Create or use another folder to group by the Chapter
                        var chapterTitle = reader.GetString(reader.GetOrdinal("ChapterTitle"));
                        foreach (char invalidChar in Path.GetInvalidPathChars())
                            chapterTitle = chapterTitle.Replace(invalidChar, '-');

                        var chapterIndex = reader.GetInt32(reader.GetOrdinal("ChapterIndex"));
                        chapterTitle = $"{chapterIndex} - {chapterTitle}";

                        if (!Directory.Exists(Path.Combine(containingDir.FullName, chapterTitle)))
                            containingDir = containingDir.CreateSubdirectory(chapterTitle);
                        else
                            containingDir = new DirectoryInfo(Path.Combine(containingDir.FullName, chapterTitle));

                        var videoIndex = reader.GetInt32(reader.GetOrdinal("SortIndex"));
                        var title = reader.GetString(reader.GetOrdinal("Title"));
                        foreach (char invalidChar in Path.GetInvalidFileNameChars())
                            title = title.Replace(invalidChar, '-');

                        var newPath = Path.Combine(containingDir.FullName, $"E{videoIndex.ToString()} - {title}.mp4");
                        taskList.Add(Task.Factory.StartNew(() => Decrypt(item, newPath)));
                    }
                    else
                    {
                        WriteToConsole("[STATUS] Couldn't find db entry for file: " + item + Environment.NewLine + "[STATUS] Using the default behaviour!", ConsoleColor.DarkYellow);
                        taskList.Add(Task.Factory.StartNew(() => Decrypt(item, Path.ChangeExtension(item, ".mp4"))));
                    }
                }
                else
                    taskList.Add(Task.Factory.StartNew(() => Decrypt(item, Path.ChangeExtension(item, ".mp4"))));
            }

            await Task.WhenAll(taskList);
            WriteToConsole("Decryption completed!", ConsoleColor.DarkGreen);
        }

        static void Decrypt(string encryptedFilePath, string decryptedFilePath)
        {
            if (!File.Exists(encryptedFilePath))
                throw new FileNotFoundException();

            FileInfo encryptedFileInfo = new FileInfo(encryptedFilePath);
            byte[] buffer = new byte[0x500000];

            if (encryptedFileInfo.Extension != ".lynda")
            {
                WriteToConsole("[ERR] Couldn't load file: " + encryptedFilePath, ConsoleColor.Red);
                return;
            }

            using (FileStream inStream = new FileStream(encryptedFilePath, FileMode.Open))
            {
                using (CryptoStream decryptionStream = new CryptoStream(inStream, decryptor, CryptoStreamMode.Read))
                using (FileStream outStream = new FileStream(decryptedFilePath, FileMode.Create))
                {
                    WriteToConsole("[DEC] Decrypting file" + encryptedFileInfo.Name + "...");

                    while ((inStream.Length - inStream.Position) >= buffer.Length)
                    {
                        decryptionStream.Read(buffer, 0, buffer.Length);
                        outStream.Write(buffer, 0, buffer.Length);
                    }

                    buffer = new byte[inStream.Length - inStream.Position];
                    decryptionStream.Read(buffer, 0, buffer.Length);
                    outStream.Write(buffer, 0, buffer.Length);
                    outStream.Flush();

                    WriteToConsole("[DEC] File decryption completed: " + decryptedFilePath, ConsoleColor.DarkGreen);
                }
            }
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
