using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LyndaDecryptor;

namespace LyndaDecryptorTests
{
    [TestClass]
    public class DecryptionTest
    {
        [TestMethod]
        public void TestSingleDecryption()
        {
            DecryptorOptions options = new DecryptorOptions
            {
                UsageMode = Mode.File,
                InputPath = "TestFiles\\88067_2195c10678b4f73e34795af641ad1ecc.lynda",
                OutputPath = "TestFiles\\88067_2195c10678b4f73e34795af641ad1ecc.mp4",
                RemoveFilesAfterDecryption = false
            };

            Decryptor decryptor = new Decryptor(options);

            decryptor.InitDecryptor(Utils.ENCRYPTION_KEY);
            decryptor.Decrypt(options.InputPath, options.OutputPath);

            FileInfo encryptedFile = new FileInfo(options.InputPath);
            FileInfo decryptedFile = new FileInfo(options.OutputPath);

            Assert.AreEqual(encryptedFile.Length, decryptedFile.Length);
        }

        [TestMethod]
        public void TestSingleDecryptionWithDB()
        {

        }

        [TestMethod]
        public void TestFolderDecryption()
        {

        }

        [TestMethod]
        public void TestFolderDecryptionWithDB()
        {

        }
    }
}
