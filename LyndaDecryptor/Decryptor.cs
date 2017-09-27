using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using static LyndaDecryptor.Utils;

namespace LyndaDecryptor
{
    public class Decryptor
    {
        #region Fields & Properties

        // Cryptographics
        RijndaelManaged RijndaelInstace;
        byte[] KeyBytes;

        // Database Connection
        SQLiteConnection DatabaseConnection;

        // Threading
        List<Task> TaskList = new List<Task>();
        SemaphoreSlim Semaphore = new SemaphoreSlim(5);
        object SemaphoreLock = new object();

        // IO
        List<char> InvalidPathCharacters = new List<char>(), InvalidFileCharacters = new List<char>();
        DirectoryInfo OutputDirectory = null;

        // Decryptor Options
        public DecryptorOptions Options = new DecryptorOptions();

        #endregion

        public Decryptor()
        {
            InvalidPathCharacters.AddRange(Path.GetInvalidPathChars());
            InvalidPathCharacters.AddRange(new char[] { ':', '?', '"', '\\', '/' });

            InvalidFileCharacters.AddRange(Path.GetInvalidFileNameChars());
            InvalidFileCharacters.AddRange(new char[] { ':', '?', '"', '\\', '/' });
        }

        /// <summary>
        /// Constructs an object with decryptor options</br>
        /// If specified this constructor inits the database
        /// </summary>
        /// <param name="options"></param>
        public Decryptor(DecryptorOptions options) : this()
        {
            Options = options;

            if (options.UseDatabase)
                Options.UseDatabase = InitDB(options.DatabasePath);  
        }

        #region Methods

        /// <summary>
        /// Create the RSA Instance and EncryptedKeyBytes
        /// </summary>
        /// <param name="EncryptionKey">secret cryptographic key</param>
        public void InitDecryptor(string EncryptionKey)
        {
            WriteToConsole("[START] Init Decryptor...");
            RijndaelInstace = new RijndaelManaged
            {
                KeySize = 0x80,
                Padding = PaddingMode.Zeros
            };

            KeyBytes = new ASCIIEncoding().GetBytes(EncryptionKey);
            WriteToConsole("[START] Decryptor successful initalized!" + Environment.NewLine, ConsoleColor.Green);
        }

        /// <summary>
        /// Create a SqliteConnection to the specified or default application database.
        /// </summary>
        /// <param name="databasePath">Path to database file</param>
        /// <returns>true if init was successful</returns>
        public bool InitDB(string databasePath)
        {
            WriteToConsole("[DB] Creating db connection...");

            // Check for databasePath
            if (string.IsNullOrEmpty(databasePath))
            {
                // Try to figure out default app db path
                var AppPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "lynda.com", "video2brain Desktop App");

                if (!Directory.Exists(AppPath))
                    AppPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "lynda.com", "lynda.com Desktop App");

                // Find db file or databasePath = default(string)
                databasePath = Directory.EnumerateFiles(AppPath, "*.sqlite", SearchOption.AllDirectories).FirstOrDefault();
            }

            // Check if databasePath is present (specific or default)
            if (!string.IsNullOrEmpty(databasePath))
            {
                DatabaseConnection = new SQLiteConnection($"Data Source={databasePath}; Version=3;FailIfMissing=True");
                DatabaseConnection.Open();

                WriteToConsole("[DB] DB successfully connected and opened!" + Environment.NewLine, ConsoleColor.Green);
                return true;
            }
            else
            {
                WriteToConsole("[DB] Couldn't find db file!" + Environment.NewLine, ConsoleColor.Red);
                return false;
            }
        }

        /// <summary>
        /// Decrypt all files in a given folder
        /// </summary>
        /// <param name="folderPath">path to folder with encrypted .lynda files</param>
        /// <param name="outputFolder">specify an output folder</param>
        public void DecryptAll(string folderPath, string outputFolder = "")
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException();

            if(string.IsNullOrWhiteSpace(outputFolder))
                outputFolder = Path.Combine(Path.GetDirectoryName(folderPath), "decrypted");

            OutputDirectory = Directory.Exists(outputFolder) ? new DirectoryInfo(outputFolder) : Directory.CreateDirectory(outputFolder);

            foreach (string entry in Directory.EnumerateFiles(folderPath, "*.lynda", SearchOption.AllDirectories))
            {
                string newPath = string.Empty;
                var item = entry;

                if (Options.UseDatabase)
                {
                    try
                    {
                        // get metadata with courseID and videoID
                        var videoInfo = GetVideoInfoFromDB(new DirectoryInfo(Path.GetDirectoryName(item)).Name, Path.GetFileName(item).Split('_')[0]);

                        if(videoInfo != null)
                        {
                            // create new path and folder
                            var complexTitle = $"E{videoInfo.VideoIndex} - {videoInfo.VideoTitle}.mp4";
                            var simpleTitle = $"E{videoInfo.VideoIndex}.mp4";

                            newPath = Path.Combine(OutputDirectory.FullName, CleanPath(videoInfo.CourseTitle),
                                                   CleanPath(videoInfo.ChapterTitle), CleanPath(complexTitle));

                            if (newPath.Length > 240)
                            {
                                newPath = Path.Combine(OutputDirectory.FullName, CleanPath(videoInfo.CourseTitle),
                                                       CleanPath(videoInfo.ChapterTitle), CleanPath(simpleTitle));
                            }

                            if (!Directory.Exists(Path.GetDirectoryName(newPath)))
                                Directory.CreateDirectory(Path.GetDirectoryName(newPath));
                        }
                    }
                    catch (Exception e)
                    {
                        WriteToConsole($"[ERR] Could not retrive media information from database! Exception: {e.Message} Falling back to default behaviour!", ConsoleColor.Yellow);
                    }
                }

                if(String.IsNullOrWhiteSpace(newPath))
                {
                    newPath = Path.ChangeExtension(item, ".mp4");
                }

                Semaphore.Wait();
                TaskList.Add(Task.Run(() =>
                {
                    Decrypt(item, newPath);
                    convertSub(item, newPath);
                    lock (SemaphoreLock)
                    {
                        Semaphore.Release();
                    }
                }));
            }

            Task.WhenAll(TaskList).Wait();
            WriteToConsole("Decryption completed!", ConsoleColor.DarkGreen);
        }

        /// <summary>
        /// Decrypt a single encrypted file into decrypted file path
        /// </summary>
        /// <param name="encryptedFilePath">Path to encrypted file</param>
        /// <param name="decryptedFilePath">Path to decrypted file</param>
        /// <param name="removeOldFile">Remove encrypted file after decryption?</param>
        public void Decrypt(string encryptedFilePath, string decryptedFilePath)
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
                using (CryptoStream decryptionStream = new CryptoStream(inStream, RijndaelInstace.CreateDecryptor(KeyBytes, KeyBytes), CryptoStreamMode.Read))
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

            convertSub(encryptedFilePath, decryptedFilePath);

            if (Options.RemoveFilesAfterDecryption)
                encryptedFileInfo.Delete();

            encryptedFileInfo = null;
        }

        /// <summary>
        /// Retrive video infos from the database
        /// </summary>
        /// <param name="courseID">course id</param>
        /// <param name="videoID">video id</param>
        /// <returns>VideoInfo instance or null</returns>
        private VideoInfo GetVideoInfoFromDB(string courseID, string videoID)
        {
            VideoInfo videoInfo = null;

            try
            {
                var cmd = DatabaseConnection.CreateCommand();

                // Query all required tables and fields from the database
                cmd.CommandText = @"SELECT Video.ID, Video.ChapterId, Video.CourseId, 
                                           Video.Title, Filename, Course.Title as CourseTitle, 
                                           Video.SortIndex, Chapter.Title as ChapterTitle, 
                                           Chapter.SortIndex as ChapterIndex 
                                    FROM Video, Course, Chapter 
                                    WHERE Video.ChapterId = Chapter.ID
                                    AND Course.ID = Video.CourseId 
                                    AND Video.CourseId = @courseId 
                                    AND Video.ID = @videoId";

                cmd.Parameters.Add(new SQLiteParameter("@courseId", courseID));
                cmd.Parameters.Add(new SQLiteParameter("@videoId", videoID));

                var reader = cmd.ExecuteReader();

                if (reader.Read())
                {
                    videoInfo = new VideoInfo();

                    videoInfo.CourseTitle = reader.GetString(reader.GetOrdinal("CourseTitle"));
                    videoInfo.ChapterTitle = reader.GetString(reader.GetOrdinal("ChapterTitle"));
                    videoInfo.ChapterIndex = reader.GetInt32(reader.GetOrdinal("ChapterIndex"));
                    videoInfo.VideoIndex = reader.GetInt32(reader.GetOrdinal("SortIndex"));
                    videoInfo.VideoTitle = reader.GetString(reader.GetOrdinal("Title"));

                    videoInfo.ChapterTitle = $"{videoInfo.ChapterIndex} - {videoInfo.ChapterTitle}";

                    videoInfo.VideoID = videoID;
                    videoInfo.CourseID = courseID;
                }
            }
            catch (Exception e)
            {
                WriteToConsole($"[ERR] Exception occured during db query ({courseID}/{videoID}): {e.Message}", ConsoleColor.Yellow);
                videoInfo = null;
            }

            return videoInfo;
        }

        /// <summary>
        /// Clean the input string and remove all invalid chars
        /// </summary>
        /// <param name="path">input path</param>
        /// <returns></returns>
        private string CleanPath(string path)
        {
            foreach (var invalidChar in InvalidPathCharacters)
                path = path.Replace(invalidChar, '-');

            return path;
        }


        /// <summary>
        /// get caption path and create subtitle in the same plae as the decrypted video
        /// </summary>
        /// <param name="videoPath">Initial video path (.lynda file)</param>
        /// <param name="decryptedFilePath">Full decrypted video path</param>
        /// <returns>boolean value, true for succesful conversion</returns>
        private Boolean convertSub(string videoPath,string decryptedFilePath) {
            using (MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                var videoId = Path.GetFileName(videoPath).Split('_')[0];

                byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(videoId);
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // Convert the byte array to hexadecimal string
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i<hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("X2"));
                }
                var subFName = sb.ToString() + ".caption";

                string captionFilePath = Path.Combine(Path.GetDirectoryName(videoPath), subFName);

                if (File.Exists(captionFilePath))
                {
                    var csConv = new CaptionToSrt(captionFilePath);

                    var srtFile = Path.Combine(Path.GetDirectoryName(decryptedFilePath), Path.GetFileNameWithoutExtension(decryptedFilePath) + ".srt");
                    csConv.OutFile = srtFile;

                    return csConv.convertToSrt();
                }
                else
                {
                    return false;
                }
            }
        }

        #endregion
    }
}
