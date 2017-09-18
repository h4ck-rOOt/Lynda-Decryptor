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


        public byte[] stringToBytes(string text)
        {
            return System.Text.Encoding.UTF8.GetBytes(text);
        }


        public int[] findTimestamps(byte[] inputByteArray)
        {
            byte openingBracket = stringToBytes("[")[0];
            byte closingBracket = stringToBytes("]")[0];
            byte colon = stringToBytes(":")[0];
            byte dot = stringToBytes(".")[0];

            // Offsets for the following characters are relative to the opening bracket.
            int firstColonOffset = 3;
            int secondColonOffset = 6;
            int dotOffset = 9;
            int closingBracketOffset = 12;

            int rangeStartOffset = -16; // Start of binary data before timestamp occurs this far from the opening bracket.
            int rangeEndOffset = 26;    // End of binary data after timestamp occurs this far from the opening bracket.
            List<int> timestampRangePositionList = new List<int>();
            int inputIndex = 16; // Valid opening bracket can't occur before this position so start here.
            bool foundTimestamp = false;
            byte inputCharacter;
            int inputLength = inputByteArray.Length;

            while (inputIndex < inputLength)
            {
                inputCharacter = inputByteArray[inputIndex];
                if (inputCharacter == openingBracket)
                {
                    if (inputCharacter + rangeEndOffset <= inputLength)
                    {
                        if (inputByteArray[inputIndex + closingBracketOffset] == closingBracket &&
                            inputByteArray[inputIndex + firstColonOffset] == colon &&
                            inputByteArray[inputIndex + secondColonOffset] == colon &&
                            inputByteArray[inputIndex + dotOffset] == dot)
                        {
                            // All timestamp identifiers match, timestamp located.
                            foundTimestamp = true;
                        }
                        else
                        {
                            // Not enough space after opening bracket for this to be a real subtitle.
                            foundTimestamp = false;
                        }
                    }
                    else
                    {
                        // Opening bracket was not part of timestamp.
                        foundTimestamp = false;
                    }
                }
                if (foundTimestamp)
                {
                    timestampRangePositionList.Add(inputIndex + rangeStartOffset);
                    // Set checkpoint for next timestamp beyond the range of this one and the leading binary data of the next.
                    inputIndex += rangeEndOffset + 1 - rangeStartOffset;
                    // Reset found flag.
                    foundTimestamp = false;
                }
                else
                {
                    inputIndex++;
                }
            }
            return timestampRangePositionList.ToArray();
        }


        public class Subtitle
        {
            private int textStartPosition = 43;

            public bool isValidSubtitle
            {
                get
                {
                    if (start_timestamp != null &&
                        end_timestamp != null &&
                        subtitleText != null)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            private string start_timestamp;
            public string Start_timestamp
            {
                get { return start_timestamp; }
            }

            private string end_timestamp;
            public string End_timestamp
            {
                get { return end_timestamp; }
            }

            private string subtitleText;
            public string SubtitleText
            {
                get { return subtitleText; }
            }

            // Initialize subtitle object.
            public Subtitle(byte[] subtitleData)
            {
                extractStartTimestamp(subtitleData);
                extractText(subtitleData);
            }

            public void setEndTimestamp(string endtime)
            {
                end_timestamp = endtime;
            }

            private void extractStartTimestamp(byte[] subtitleData)
            {
                // Timestamp (without brackets) is 11 bytes long and starts at position 17.
                byte[] timestampByteArray = new byte[11];
                Array.Copy(subtitleData, 17, timestampByteArray, 0, 11);
                string timestampString = bytesToString(timestampByteArray, timestampByteArray.Length);
                // Change "." in timestamp to "," which is used in the SRT format.
                start_timestamp = Regex.Replace(timestampString, "\\.", ",");
            }

            private void extractText(byte[] subtitleData)
            {
                bool hasValidText;
                byte[] trimmedText = new byte[0];
                // Text data starts at byte 43.
                if (subtitleData.Length > textStartPosition)
                {
                    // Subtitle is long enough to contain text following the timestamp.
                    int textLength = subtitleData.Length - textStartPosition;
                    byte[] textPortion = new byte[textLength];
                    Array.Copy(subtitleData, 43, textPortion, 0, textLength);
                    trimmedText = trimNonprintable(textPortion);
                    if (trimmedText.Length > 0)
                    {
                        hasValidText = true;
                    }
                    else
                    {
                        hasValidText = false;
                    }
                }
                else
                {
                    // Subtitle is too short to have text.
                    hasValidText = false;
                }
                if (hasValidText)
                {
                    trimmedText = enforce_CRLF_linebreaks(trimmedText);
                    subtitleText = bytesToString(trimmedText, trimmedText.Length);
                }
            }

            private byte[] trimNonprintable(byte[] textData)
            {
                List<byte> textList = textData.ToList();
                int currentCharacter;
                int removeIndex;
                bool searchForward = true;

                // Trim beginning of text.
                while (textList.Count > 0)
                {
                    if (searchForward)
                    {
                        // Scan forwards from beginning of text.
                        currentCharacter = Convert.ToInt32(textList.First());
                        removeIndex = 0;
                    }
                    else
                    {
                        // Scan backwards from end of text.
                        currentCharacter = Convert.ToInt32(textList.Last());
                        removeIndex = textList.Count - 1;
                    }
                    if (currentCharacter < 33 || currentCharacter > 126)
                    {
                        // Character values lower than 33 or higher than 126 are all nonprintable, remove them.
                        textList.RemoveAt(removeIndex);
                    }
                    else
                    {
                        // Hit a printable character.
                        if (searchForward)
                        {
                            // Switch to trimming end of text.
                            searchForward = false;
                        }
                        else
                        {
                            // Trimming complete.
                            break;
                        }
                    }
                }
                return textList.ToArray();
            }

            private byte[] enforce_CRLF_linebreaks(byte[] text)
            {
                // Make all linebreaks CRLF (if they aren't already), just in case mixed linebreaks would trip up some SRT interpreter.
                byte currentCharacter;
                byte previousCharacter = 0x0; // Set to something not \r
                int textLength = text.Length;
                bool found_LF_linebreak = false;
                List<byte> returnText = new List<byte>();
                byte LF_Character = stringToBytes("\n")[0];
                byte CR_Character = stringToBytes("\r")[0];
                for (int characterIndex = 0; characterIndex < textLength; characterIndex++)
                {
                    currentCharacter = text[characterIndex];
                    if (currentCharacter == LF_Character)
                    {
                        if (previousCharacter != CR_Character)
                        {
                            found_LF_linebreak = true;
                        }
                    }
                    if (found_LF_linebreak)
                    {
                        returnText.Add(CR_Character);
                        returnText.Add(LF_Character);
                        // Reset found flag.
                        found_LF_linebreak = false;
                    }
                    else
                    {
                        returnText.Add(currentCharacter);
                    }
                    previousCharacter = currentCharacter;
                }
                return returnText.ToArray();
            }

            private string bytesToString(byte[] bytes, int length)
            {
                return System.Text.Encoding.UTF8.GetString(bytes, 0, length);
            }

            private byte[] stringToBytes(string text)
            {
                return System.Text.Encoding.UTF8.GetBytes(text);
            }
        }

        public Subtitle[] parseCaptionData(byte[] captionData, int[] subtitlePositions)
        {
            // Extract subtitles from caption file data.
            int positionCount = subtitlePositions.Length;
            int startPosition;
            int subtitleLength;
            byte[] newSubtitleData;
            Subtitle newSubtitle;
            List<Subtitle> subtitleList = new List<Subtitle>();
            for (int positionIndex = 0; positionIndex < positionCount; positionIndex++)
            {
                startPosition = subtitlePositions[positionIndex];
                if (positionIndex == positionCount - 1)
                {
                    // The last subtitle extends to the end of the caption data.
                    subtitleLength = captionData.Length - startPosition;
                }
                else
                {
                    // The subtitle extends up to the beginning of the next one.
                    subtitleLength = subtitlePositions[positionIndex + 1] - startPosition;
                }
                newSubtitleData = new byte[subtitleLength];
                Array.Copy(captionData, startPosition, newSubtitleData, 0, subtitleLength);

                // Create subtitle object, which also handles parsing of the data.
                newSubtitle = new Subtitle(newSubtitleData);
                subtitleList.Add(newSubtitle);
            }

            // Set end time of each subtitle to the beginning of the next one.
            int subtitleCount = subtitleList.Count;
            for (int subtitleIndex = 0; subtitleIndex < subtitleCount; subtitleIndex++)
            {
                if (subtitleIndex < subtitleCount - 1)
                {
                    // End timestamp is equal to the start timestamp of the next one. Skip this for the last subtitle because there is no next one. It wouldn't contain text anyway.
                    Subtitle currentSub = subtitleList[subtitleIndex];
                    Subtitle nextSub = subtitleList[subtitleIndex + 1];
                    currentSub.setEndTimestamp(nextSub.Start_timestamp);
                }
            }

            // Discard subtitles that have no text
            List<Subtitle> filteredSubtitles = new List<Subtitle>();
            for (int subtitleIndex = 0; subtitleIndex < subtitleCount; subtitleIndex++)
            {
                Subtitle currentSub = subtitleList[subtitleIndex];
                if (currentSub.isValidSubtitle)
                {
                    filteredSubtitles.Add(currentSub);
                }
            }

            // Return array of valid subtitles.
            return filteredSubtitles.ToArray();
        }


        public bool convertToSrt()
        {
            ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // NOTES ON THE STRUCTURE OF .CAPTION FILES                                                                                                                   //
            //                                                                                                                                                            //
            // The .caption files start with a header. It is ignored by the conversion function.                                                                          //
            // Following the header are blocks of subtitle data. These blocks consist of 16 bytes of binary data, followed by a timestamp in the format [##:##:##.##].    //
            // This is followed by another 14 bytes of binary data, after which the text of the subtitle starts. The text continues until the next block.                 //
            // Some blocks contain no valid text, and serve as markers for the end time of the previous block. They usually occur once (or several times) at the end of   //
            // the .caption file, but can sometimes be found following each valid subtitle block.                                                                         //
            // Linebreaks used by .caption files are usually CRLF, but can also be LF. This may be different for each video.                                              //
            // Blocks are most often separated by double, but sometimes single linebreaks (in combination with end-time blocks).                                          //
            ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            // NOTES ON THE CONVERSION CODE                                                                                                                               //
            //                                                                                                                                                            //
            // During testing, splitting subtitle blocks by linebreaks was the first attempted method of extracting data. It came with some hard-to-squish bugs, as the   //
            // binary data mentioned above would sometimes randomly produce linebreak characters. The method of extraction was then switched to locating the timestamps   //
            // and going from there. Their fixed location in the subtitle block makes it easy to locate (and ignore) other elements in the block.                         //
            //                                                                                                                                                            //
            // The data is mostly handled in byte-form. While performing  operations on strings would be more simple and concise, it can interfere with the evenly-spaced //
            // layout of the data, sometimes producing extra or missing characters. Handling the data in byte-form prevents that.                                         //
            ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


            // Read content of source subtitle file as bytes.
            byte[] subtitleFile = File.ReadAllBytes(filePath);

            // Locate start positions of subtitles.
            int[] subtitlePositions = findTimestamps(subtitleFile);

            // Extract subtitles from file data.
            Subtitle[] subtitles = parseCaptionData(subtitleFile, subtitlePositions);

            // Output data in .srt file.
            this.buildSrt(subtitles, this.outFile);

            return true;
        }

        private bool buildSrt(Subtitle[] subtitleArray, string path)
        {
            try
            {
                //SRT is perhaps the most basic of all subtitle formats.
                //It consists of four parts, all in text..

                //1.A number indicating which subtitle it is in the sequence.
                //2.The time that the subtitle should appear on the screen, and then disappear.
                //3.The subtitle itself.
                //4.A blank line indicating the start of a new subtitle.

                //1
                //00:02:17,440-- > 00:02:20,375
                //and here goes the text
                //blank line

                StreamWriter writer = new StreamWriter(path);
                Subtitle currentSubtitle;
                for (int subtitleIndex = 0; subtitleIndex < subtitleArray.Length; subtitleIndex++)
                {
                    currentSubtitle = subtitleArray[subtitleIndex];
                    writer.WriteLine(subtitleIndex + 1);
                    writer.WriteLine(currentSubtitle.Start_timestamp + " --> " + currentSubtitle.End_timestamp);
                    writer.WriteLine(currentSubtitle.SubtitleText);
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
