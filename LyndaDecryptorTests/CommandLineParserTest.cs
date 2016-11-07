#define TEST

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LyndaDecryptor;
using System.Collections.Generic;
using System.IO;

namespace LyndaDecryptorTests
{
    [TestClass]
    public class CommandLineParserTest
    {
        [TestMethod]
        public void TestFileMode()
        {
            List<string> args = new List<string>();
            args.Add("/F");
            args.Add("TestFiles\\88067_2195c10678b4f73e34795af641ad1ecc.lynda");
            args.Add("output.mp4");

            DecryptorOptions options = new DecryptorOptions();
            options = Utils.ParseCommandLineArgs(args.ToArray());

            Assert.IsTrue(options.UsageMode == Mode.File);
            Assert.AreEqual("TestFiles\\88067_2195c10678b4f73e34795af641ad1ecc.lynda", options.InputPath);
            Assert.AreEqual("output.mp4", options.OutputPath);

            args.Add("/DB");
            options = Utils.ParseCommandLineArgs(args.ToArray());

            Assert.IsTrue(options.UseDatabase);
            Assert.AreEqual(null, options.DatabasePath);

            args.Add("TestDB\\db_de.sqlite");
            options = Utils.ParseCommandLineArgs(args.ToArray());

            Assert.AreEqual(args.Last(), options.DatabasePath);

            args.Add("/RM");
            options = Utils.ParseCommandLineArgs(args.ToArray());

            Assert.IsTrue(options.RemoveFilesAfterDecryption);

            args.Add("/OUT");
            args.Add("testfolder");
            options = Utils.ParseCommandLineArgs(args.ToArray());

            Assert.IsTrue(options.UseOutputFolder);
            Assert.AreEqual(args.Last(), options.OutputFolder);
        }

        [TestMethod]
        public void TestFolderMode()
        {
            List<string> args = new List<string>();
            args.Add("/D");
            args.Add("TestFiles");

            DecryptorOptions options = new DecryptorOptions();
            options = Utils.ParseCommandLineArgs(args.ToArray());

            Assert.IsTrue(options.UsageMode == Mode.Folder);
            Assert.AreEqual(options.InputPath, "TestFiles");

            args.Add("/RM");
            options = Utils.ParseCommandLineArgs(args.ToArray());

            Assert.IsTrue(options.RemoveFilesAfterDecryption);

            args.Add("/DB");
            options = Utils.ParseCommandLineArgs(args.ToArray());

            Assert.IsTrue(options.UseDatabase);
            Assert.AreEqual(null, options.DatabasePath);

            args.Add("TestDB\\db_de.sqlite");
            options = Utils.ParseCommandLineArgs(args.ToArray());

            Assert.AreEqual(args.Last(), options.DatabasePath);

            args.Add("/OUT");
            args.Add("testfolder");
            options = Utils.ParseCommandLineArgs(args.ToArray());

            Assert.IsTrue(options.UseOutputFolder);
            Assert.AreEqual(options.OutputFolder, args.Last());
        }

        [TestMethod, ExpectedException(typeof(FormatException))]
        public void MissingOutputArgShouldFailWithException()
        {
            List<string> args = new List<string>();
            args.Add("/F");
            args.Add("TestFiles\\88067_2195c10678b4f73e34795af641ad1ecc.lynda");

            Utils.ParseCommandLineArgs(args.ToArray());
        }

        [TestMethod, ExpectedException(typeof(IOException))]
        public void OutputFileAlreadyExistShouldFailWithException()
        {
            List<string> args = new List<string>();
            args.Add("/F");
            args.Add("TestFiles\\88067_2195c10678b4f73e34795af641ad1ecc.lynda");
            args.Add("TestFiles\\88071_4650ab745df849fd96f1fdbdb016a5e6.lynda");

            Utils.ParseCommandLineArgs(args.ToArray());
        }

        [TestMethod, ExpectedException(typeof(FileNotFoundException))]
        public void TestMissingFolder()
        {
            List<string> args = new List<string>();
            args.Add("/D");
            args.Add("TestFiles2");

            Utils.ParseCommandLineArgs(args.ToArray());
        }
    }
}
