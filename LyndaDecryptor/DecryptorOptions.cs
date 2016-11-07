namespace LyndaDecryptor
{
    public class DecryptorOptions
    {
        public Mode UsageMode { get; set; }
        public bool UseDatabase { get; set; }
        public bool UseOutputFolder { get; set; }
        public bool RemoveFilesAfterDecryption { get; set; }

        public string InputPath { get; set; }
        public string OutputPath { get; set; }
        public string OutputFolder { get; set; }
        public string DatabasePath { get; set; }
    }
}
