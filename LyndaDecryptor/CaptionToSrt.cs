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


        public enum timestampFormat
        {
            shortTimestamp, //Format [##:##:##.##], trailing binary data 14 bytes.
            longTimestamp   //Format [##:##:##.###], trailing binary data 15 bytes.
        }


        public class Timestamp
        {
            public readonly int position;
            public readonly timestampFormat type;

            public Timestamp(int _position, timestampFormat _type)
            {
                position = _position;
                type = _type;
            }
        }


        public Timestamp[] findTimestamps(byte[] inputByteArray)
        {
            List<Timestamp> timestampList = new List<Timestamp>();

            byte openingBracket = stringToBytes("[")[0];
            byte closingBracket = stringToBytes("]")[0];
            byte colon = stringToBytes(":")[0];
            byte dot = stringToBytes(".")[0];

            // Offsets for the following characters are relative to the opening bracket.
            int firstColonOffset = 3;
            int secondColonOffset = 6;
            int dotOffset = 9;
            int rangeStartOffset = -16; // Start of binary data before timestamp occurs this far from the opening bracket.

            // Character positions for short timestamp.
            int closingBracketOffset_shortTimestamp = 12;
            int rangeEndOffset_shortTimestamp = 26; // End of binary data after timestamp occurs this far from the opening bracket.

            // Character positions for long timestamp.
            int closingBracketOffset_longTimestamp = 13;
            // int rangeEndOffset_longTimestamp = 27; // End of binary data after timestamp occurs this far from the opening bracket. // Not used in this part of the code, but still good to be aware of.

            // The distance you can skip forward when searching for the next timestamp. This can vary according to the timestamp format. More logic could be put into this, but taking the length belonging to the shortest format is simplest and safest.
            int minimumRangeEndOffset = rangeEndOffset_shortTimestamp;
            
            timestampFormat timestampFormat = timestampFormat.shortTimestamp; //Default value, real format will be determined later.

            int inputIndex = rangeStartOffset * -1; // Valid opening bracket can't occur before this position so start here.
            bool foundTimestamp = false; //Default value, will be set to true later if a timestamp is found.
            byte inputCharacter;
            int inputLength = inputByteArray.Length;

            // Look for timestamps in the caption file data.
            while (inputIndex < inputLength)
            {
                inputCharacter = inputByteArray[inputIndex];
                // Search for opening bracket.
                if (inputCharacter == openingBracket)
                {
                    // Check if opening bracket belongs to timestamp.
                    if (inputCharacter + minimumRangeEndOffset <= inputLength)
                    {
                        // Timestamps can take 2 forms: [##:##:##.##] and [##:##:##.###].
                        // Check the elements they have in common (::.).
                        if (inputByteArray[inputIndex + firstColonOffset] == colon &&
                            inputByteArray[inputIndex + secondColonOffset] == colon &&
                            inputByteArray[inputIndex + dotOffset] == dot)
                        {
                            // Fixed timestamp identifiers match, now check location of the closing bracket.
                            if (inputByteArray[inputIndex + closingBracketOffset_shortTimestamp] == closingBracket)
                            {
                                // Timestamp was format [##:##:##.##].
                                timestampFormat = timestampFormat.shortTimestamp;
                                foundTimestamp = true;
                            }
                            else if (inputByteArray[inputIndex + closingBracketOffset_longTimestamp] == closingBracket)
                            {
                                // Timestamp was format [##:##:##.###].
                                timestampFormat = timestampFormat.longTimestamp;
                                foundTimestamp = true;
                            }
                            else
                            {
                                // No match for closing bracket. Not a timestamp after all, or in an unknown format.
                                foundTimestamp = false;
                            }
                        }
                        else
                        {
                            // Other timestamp identifiers don't match, opening bracket was not part of timestamp.
                            foundTimestamp = false;
                        }
                    }
                    else
                    {
                        // Not enough space after opening bracket for this to be the beginning of a subtitle.
                        foundTimestamp = false;
                    }
                }
                if (foundTimestamp)
                {
                    // The timestamp is preceded by some binary data (rangeStartOffset), so skip over that and add the actual beginning of the timestamp.
                    // Also add the timestamp format ([##:##:##.##] or [##:##:##.###]), this is important later for parsing.
                    timestampList.Add(new Timestamp(inputIndex + rangeStartOffset, timestampFormat));
                    // Set checkpoint for next timestamp beyond the (minimum) range of this one and the leading binary data of the next.
                    inputIndex += minimumRangeEndOffset + 1 - rangeStartOffset;
                    // Reset found flag.
                    foundTimestamp = false;
                }
                else
                {
                    // Advance to the next character in the caption data.
                    inputIndex++;
                }
            }
            // Return timestamp locations in the caption file data.
            return timestampList.ToArray();
        }


        public class Subtitle
        {
            // Text portion of the subtitle starts this far from the beginning of the timestamp. Can vary according to timestamp format.
            private int textStartPosition;

            // Check if all necessary parameters for a valid subtitle have been set.
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

            // Time when the subtitle should appear in the video.
            private string start_timestamp;
            public string Start_timestamp
            {
                get { return start_timestamp; }
            }

            // Time when the subtitle should disappear.
            private string end_timestamp;
            public string End_timestamp
            {
                get { return end_timestamp; }
            }

            // The actual text of the subtitle.
            private string subtitleText;
            public string SubtitleText
            {
                get { return subtitleText; }
            }

            // Initialize subtitle object.
            public Subtitle(byte[] subtitleData, timestampFormat ts_format)
            {
                extractStartTimestamp(subtitleData, ts_format);
                extractText(subtitleData);
            }

            // Unknown at initialization. Will be set later with the start time of the next subtitle object.
            public void setEndTimestamp(string endtime)
            {
                end_timestamp = endtime;
            }

            // Extract timestamp from subtitle data.
            private void extractStartTimestamp(byte[] subtitleData, timestampFormat ts_format)
            {
                int timestampLength;
                if (ts_format == timestampFormat.shortTimestamp)
                {
                    // Short timestamp (without brackets) is 11 bytes long.
                    timestampLength = 11;
                    textStartPosition = 43;
                }
                else
                {
                    // Long timestamp (without brackets) is 12 bytes long.
                    timestampLength = 12;
                    textStartPosition = 44;
                }
                byte[] timestampByteArray = new byte[timestampLength];
                // Timestamp (without brackets) starts at position 17.
                Array.Copy(subtitleData, 17, timestampByteArray, 0, timestampLength);
                string timestampString = bytesToString(timestampByteArray, timestampByteArray.Length);
                string properFormatTimestamp = convertToShortFormat(timestampString);
                // Change "." in timestamp to "," which is used in the SRT format.
                start_timestamp = Regex.Replace(properFormatTimestamp, "\\.", ",");
            }

            // Extract text from subtitle data.
            private void extractText(byte[] subtitleData)
            {
                bool hasValidText;
                byte[] trimmedText = new byte[0];

                if (subtitleData.Length > textStartPosition)
                {
                    // Subtitle is long enough to contain text following the timestamp.
                    int textLength = subtitleData.Length - textStartPosition;
                    byte[] textPortion = new byte[textLength];
                    Array.Copy(subtitleData, textStartPosition, textPortion, 0, textLength);
                    // Strip nonprintable characters from beginning and end of text.
                    trimmedText = trimNonprintable(textPortion);
                    if (trimmedText.Length > 0)
                    {
                        hasValidText = true;
                    }
                    else
                    {
                        // Text consisted of only nonprintable characters.
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
                        // Character was not part of linebreak, or character was part of CRLF linebreak.
                        returnText.Add(currentCharacter);
                    }
                    previousCharacter = currentCharacter;
                }
                return returnText.ToArray();
            }

            // Change long timestamp formats to short format for .SRT file. SRT can handle long format too, but maybe not mixed formats. Making everything the same just in case.
            private string convertToShortFormat(string timestamp)
            {
                if (timestamp.Length == 12)
                {
                    // Timestamp is long format.
                    // Return everything but last character.
                    return timestamp.Substring(0,11);
                }
                else
                {
                    // Timestamp is already short format.
                    return timestamp;
                }
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
        

        public Subtitle[] parseCaptionData(byte[] captionData, Timestamp[] timestampDataArray)
        {
            // Extract subtitles from caption file data.
            int timestampCount = timestampDataArray.Length;
            int startPosition;
            int subtitleLength;
            byte[] newSubtitleData;
            Subtitle newSubtitle;
            List<Subtitle> subtitleList = new List<Subtitle>();
            for (int timestampIndex = 0; timestampIndex < timestampCount; timestampIndex++)
            {
                startPosition = timestampDataArray[timestampIndex].position;
                if (timestampIndex == timestampCount - 1)
                {
                    // Special case for the last subtitle. It extends to the end of the caption data.
                    subtitleLength = captionData.Length - startPosition;
                }
                else
                {
                    // The subtitle extends up to the beginning of the next one.
                    subtitleLength = timestampDataArray[timestampIndex + 1].position - startPosition;
                }

                newSubtitleData = new byte[subtitleLength];
                Array.Copy(captionData, startPosition, newSubtitleData, 0, subtitleLength);

                // Create subtitle object, which also handles parsing of the data.
                timestampFormat ts_format = timestampDataArray[timestampIndex].type;
                newSubtitle = new Subtitle(newSubtitleData, ts_format);
                subtitleList.Add(newSubtitle);
            }

            // Set end time of each subtitle to the beginning of the next one.
            int subtitleCount = subtitleList.Count;
            for (int subtitleIndex = 0; subtitleIndex < subtitleCount - 1; subtitleIndex++)
            {
                // End timestamp is equal to the start timestamp of the next one. Last subtitle is skipped because there is no next one. It wouldn't contain text anyway.
                Subtitle currentSub = subtitleList[subtitleIndex];
                Subtitle nextSub = subtitleList[subtitleIndex + 1];
                currentSub.setEndTimestamp(nextSub.Start_timestamp);
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
            // Following the header are blocks of subtitle data. These blocks consist of 16 bytes of binary data, followed by a timestamp in the format [##:##:##.##], or //
            // in some cases [##:##:##.###]. This is followed by another 14 or 15 bytes of binary data (depending on timestamp format), after which the text of the       //
            // subtitle starts. The text continues until the next block. Some blocks contain no valid text, and serve as markers for the end time of the previous block.  //
            // They usually occur once (or several times) at the end of the .caption file, but can sometimes be found following each valid subtitle block.                //
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
            Timestamp[] timestampData = findTimestamps(subtitleFile);

            // Extract subtitles from file data.
            Subtitle[] subtitles = parseCaptionData(subtitleFile, timestampData);

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
