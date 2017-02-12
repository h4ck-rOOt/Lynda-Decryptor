using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LyndaDecryptor
{
    class CaptionToSrt
    {
        private string filePath;
        private string outFile;

        public CaptionToSrt(string afilePath)
        {
            this.filePath = afilePath;
        }

        public string FilePath
        {
            get
            {
                return filePath;
            }

            set
            {
                filePath = value;
            }
        }

        public string OutFile
        {
            get
            {
                return outFile;
            }

            set
            {
                outFile = value;
            }
        }

        public string preparesrt()
        {
            //read all file in memory
            string content = File.ReadAllText(filePath);

            //crude replacement of characters that are not plain text. File has NUL, SOX, ACK and other non printing ASCII / binary chars
            //Observed a pattern in file, rows in caption are ordered/marked by 
            // a structure of characters of the form [ACK]<0-9A-Z>[NUL] remove these first
            // a structure of characters of the form [ACK]<0-9A-Z>[SOH] remove these first
            string output = Regex.Replace(content, @"\u0006[\u0020-\u007F][\u0000\u0001\u0002]", "");

            //there are some chars right after the timestamp ] and before [NUL] or [SOH] or [ETB] chars, drop them 
            output = Regex.Replace(output, @"\][^\]\u0000-\u001F]+[\u0000-\u001F]", "]");

            //delete all non-UTF8 ASCII printable chars by this regexp
            output = Regex.Replace(output, @"[^\u0020-\u007F \u000D\n\r]+", "");

            //now we might be left with a newline or useless white space after the timestamp and before the text, delete that too
            output = Regex.Replace(output, @"\][ \n\r\t]", "");

            //remove all info at start of file used by Lynda desktop app to link subtitle to video
            //presume first timestamp starts with '[00:00...' so this is where the actual subtitle text starts
            if (output.IndexOf("[0") > 0)
            {
                output = output.Substring(output.IndexOf("[0"));
            }
            return output;
        }

        public bool convertToSrt()
        {
            string output = preparesrt();

            //split full formatted text in subtitle sections at start of timestamp
            string[] phrases = Regex.Split(output, @"(?=\[[0-9])");

            string start;
            string text;
            string[] subline;
            ArrayList timestamps = new ArrayList();
            ArrayList captions = new ArrayList();
            for (int i = 0; i < phrases.Length; i++)
            {
                try
                {
                    //get timestamp and text separately
                    subline = Regex.Split(phrases[i], @"(?<=\[[0-9:,.]+\])");
                    if (subline.Length == 2)
                    {
                        //separator for miliseconds is ',' in srt, '.' in .caption switch it
                        start = Regex.Replace(subline[0], "\\.", ",");
                        start = start.Substring(1, start.Length - 2);
                        text = subline[1];
                        //there may be a number or sign before the actual text, drop it.
                        //ATTENTION, if there is a subtitle phrase starting eth numbers or symbols this line will delete information from it
                        text = Regex.Replace(text, @"^[\u0020-\u0040\-\u005B-\u0060]{1,2}[\r\n\t]?", "");
                        //and delete any useless newlines at the end.
                        text = Regex.Replace(text, @"[\r\n\t]+[\u0020-\u0040\-]?$", "");
                        text = Regex.Replace(text, @"^[a-zA-Z0-9.]{0,2}[\r\n\t]+", "");
                        timestamps.Add(start);
                        captions.Add(text);
                    }
                    else if (subline.Length == 1 && i == phrases.Length - 1)
                    {
                        //add last timestamp to subtitle
                        start = Regex.Replace(subline[0], "\\.", ",");
                        start = start.Substring(1, start.Length - 2);
                        timestamps.Add(start);

                    }
                        this.buildSrt(timestamps, captions, this.outFile);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: Cannot convert caption content to srt " + ex.ToString());
                    return false;
                }
            }
            return true;
        }

        private bool buildSrt(ArrayList timestamps, ArrayList captions, string path)
        {
            try
            {
                StreamWriter writer = new StreamWriter(path);
                //SRT is perhaps the most basic of all subtitle formats.
                //It consists of four parts, all in text..

                //1.A number indicating which subtitle it is in the sequence.
                //2.The time that the subtitle should appear on the screen, and then disappear.
                //3.The subtitle itself.
                //4.A blank line indicating the start of a new subtitle.

                //1
                //00:02:17,440-- > 00:02:20,375
                //and here goes the text, after which there's a blank line

                //last input in array is a single timestamp with no text, used only to see where the end of the last caption is
                for (int i = 0; i < timestamps.Count - 1; i++)
                {
                    writer.WriteLine(i + 1);
                    writer.WriteLine(timestamps[i] + " --> " + timestamps[i + 1]);
                    writer.WriteLine(captions[i]);
                    writer.WriteLine();
                }
                writer.Close();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: Cannot write file " + path + ex.ToString());
                return false;
            }
        }



    }
}