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

        public string bytesToString(byte[] bytes, int from, int length)
        {
            return System.Text.Encoding.UTF8.GetString(bytes, from, length);
        }

        public byte[] stringToBytes(string text)
        {
            return System.Text.Encoding.UTF8.GetBytes(text);
        }

        public int searchByteArray(byte[] inputByteArray, byte[] searchBytes, int startPosition)
        {
            // This function is similar to the String.IndexOf() function, but for byte arrays.
            // The input array is searched for the occurrence of the search term, also represented as a byte array, using a Boyer-Moore search algorithm.

            // Build jumptable
            Dictionary<Byte, int> jumptable = new Dictionary<Byte, int>();
            int searchBytesMaxIndex = searchBytes.Length - 1;
            Byte currentSearchByte;
            for (int searchBytesIndex = searchBytesMaxIndex; searchBytesIndex >= 0; searchBytesIndex--)
            {
                currentSearchByte = searchBytes[searchBytesIndex];
                if (! jumptable.ContainsKey(currentSearchByte))
                {
                    jumptable.Add(currentSearchByte, searchBytesMaxIndex - searchBytesIndex);
                }
            }
            // Remove last character in the search term from the jumptable.
            // It was useful for preventing other occurrences of this character from ending up in the jumptable, but it's not used for searching.
            // Technically it wouldn't break anything by being there but let's remove it for the sake of clarity.
            jumptable.Remove(searchBytes[searchBytesMaxIndex]);

            // Perform the search
            int inputArrayIndex = startPosition + searchBytesMaxIndex;
            Byte currentInputByte;
            bool matchFound;
            while (inputArrayIndex < inputByteArray.Length)
            {
                matchFound = true; // Match not actually found yet, but if this is still true after the full comparison completes there's an actual match.
                for (int searchBytesIndex = searchBytesMaxIndex; searchBytesIndex >= 0; searchBytesIndex--)
                {
                    currentInputByte = inputByteArray[inputArrayIndex - (searchBytesMaxIndex - searchBytesIndex)];
                    currentSearchByte = searchBytes[searchBytesIndex];
                    if (currentInputByte == currentSearchByte)
                    {
                        // This character matches, check next character.
                        continue;
                    }
                    else
                    {
                        if (jumptable.ContainsKey(currentInputByte))
                        {
                            // Another character in the search term matches this input character. Jump forward by the corresponding amount.
                            inputArrayIndex = inputArrayIndex + jumptable[currentInputByte];
                        }
                        else
                        {
                            // This character doesn't have any match, jump past it.
                            inputArrayIndex = inputArrayIndex + searchBytesIndex + 1;
                        }
                        // No match for the search term at this position, stop checking further characters of the search term.
                        matchFound = false;
                        break;
                    }
                }
                if (matchFound)
                {
                    // Match was found; return its position and exit function.
                    return inputArrayIndex - searchBytesMaxIndex;
                }
            }
            // No match was found in the entire input (from the start position). Return -1.
            return -1;
        }

        public byte[][] splitByteArrayByDelimiter(byte[]  inputByteArray, byte[] delimiter)
        {
            // This function is similar to the String.Split() function, but for byte arrays.
            // The array is split into multiple parts at the places where the delimiter occurs.

            List<byte[]> choppedArray = new List<byte[]>();
            int chopPoint = 0;
            int chopLength;
            byte[] newChop;
            int delimiterFoundPosition;

            // Look for the delimiter in the input array until all occurrences have been found.
            do
            {
                // Search for delimiter in input array.
                delimiterFoundPosition = searchByteArray(inputByteArray, delimiter, chopPoint);
                if (delimiterFoundPosition >= 0)
                {
                    // Cut everything between this occurrence of the delimiter and the last.
                    chopLength = delimiterFoundPosition - chopPoint;
                }
                else
                {
                    // Delimiter not found, append remaining part of the input to the final result.
                    chopLength = inputByteArray.Length - chopPoint;
                }
                // Cut the text adjacent to the delimiters and add it to the output array.
                newChop = new byte[chopLength];
                Array.Copy(inputByteArray, chopPoint, newChop, 0, chopLength);
                choppedArray.Add(newChop);
                chopPoint = delimiterFoundPosition + delimiter.Length + 1;
            } while (delimiterFoundPosition >= 0);
            return choppedArray.ToArray();
        }

        public bool convertToSrt()
        {
            ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // The .caption files consist of subtitle data with extraneous, non-printable data interposed.                                                                //
            // The subtitle data is positioned at predictable locations in the file. This function operates based on that principle.                                      //
            // The code below mostly deals with the subtitles in byte-form. While performing these operations on strings would be more simple and concise,                //
            // it can interfere with the evenly-spaced layout of the data, sometimes producing extra or missing characters. Handling the data in byte-form prevents that. //
            ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            // Read content of source subtitle file as bytes
            byte[] subtitleFile = File.ReadAllBytes(filePath);

            // Split by double newlines (CRLFCRLF).
            byte[][] rawSubtitles = splitByteArrayByDelimiter(subtitleFile, stringToBytes("\r\n\r\n"));

            byte[] rawSubtitle;
            string timestamp;
            string text;
            ArrayList timestamps = new ArrayList();
            ArrayList captions = new ArrayList();
            string timeStartCharacter;
            string timeEndCharacter;
            int returnPosition;

            // Following are the fixed beginning and ending positions for the timestamp and subtitle text.
            // If subtitles are produced that look like they have erroneous extra or missing characters, try tweaking these numbers.
            // Looking at the source subtitle file with a hex editor may help determine the correct positions.
            int timeStartPosition = 15;  // Timestamp opening bracket always occurs at this position.
            int timeEndPosition = 27;    // Timestamp closing bracket always occurs at this position.
            int textStartPosition = 42;  // Subtitle text always starts at this position and continues until the end of the raw subtitle.

            // Extract timestamp and text from subtitle.
            for (int subtitleIndex = 0; subtitleIndex < rawSubtitles.Length; subtitleIndex++)
            {
                try
                {
                    rawSubtitle = rawSubtitles[subtitleIndex];
                    if (rawSubtitle.Length < textStartPosition)
                    {
                        // Skip this line if it's not even long enough to have subtitle text.
                        continue;
                    }
                    timeStartCharacter = bytesToString(rawSubtitle, timeStartPosition, 1);
                    timeEndCharacter = bytesToString(rawSubtitle, timeEndPosition, 1);
                    if (timeStartCharacter == "[" && timeEndCharacter == "]")
                    {
                        // Extract the timestamp excluding the brackets.
                        timestamp = bytesToString(rawSubtitle, timeStartPosition + 1, timeEndPosition - (timeStartPosition + 1));
                        //separator for miliseconds is ',' in srt, '.' in .caption switch it
                        timestamp = Regex.Replace(timestamp, "\\.", ",");
                        timestamps.Add(timestamp);

                        // Only add subtitle text if there is no return before before its start position (meaning there is no text following the timestamp).
                        returnPosition = searchByteArray(rawSubtitle, stringToBytes("\r\n"), timeEndPosition + 1);
                        if (returnPosition > textStartPosition || returnPosition == -1)
                        {
                            // Add subtitle text from its start position until the end of the raw subtitle.
                            text = bytesToString(rawSubtitle, textStartPosition, rawSubtitle.Length - textStartPosition);
                            captions.Add(text);
                        }
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
