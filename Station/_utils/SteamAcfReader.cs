using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Station
{
    /// <summary>
    /// This is a class specifically to read Steam ACF files
    /// The only thing we're currently looking to get out of
    /// them is the installdir, so the code is a bit geared
    /// around that idea. Definitely open to rework of this
    /// file
    /// 
    /// Code taken from:
    /// https://stackoverflow.com/questions/39065573/reading-values-from-an-acf-manifest-file
    /// </summary>
    public class AcfReader
    {
        public string FileLocation { get; private set; }
        public string? installdir { get; private set; }
        public string? gameName { get; private set; }

        public string? appId { get; private set; }

        /// <summary>
        /// Initialize the AcfReader
        /// </summary>
        /// <param name="appID">The steam app id for the acf file we want to look at</param>
        /// <exception cref="FileNotFoundException"></exception>
        public AcfReader(string appID)
        {
            string fileLocation = "C:\\Program Files (x86)\\Steam\\steamapps\\appmanifest_" + appID + ".acf";
            if (File.Exists(fileLocation))
                this.FileLocation = fileLocation;
            else
                throw new FileNotFoundException("Error", fileLocation);
        }
        
        /// <summary>
        /// Initialize the AcfReader
        /// </summary>
        /// <param name="filePath">File path for the ACF file</param>
        /// <param name="placeholder">Differentiator to the other constructor</param>
        /// <exception cref="FileNotFoundException"></exception>
        public AcfReader(string filePath, bool placeholder)
        {
            if (File.Exists(filePath))
                this.FileLocation = filePath;
            else
                throw new FileNotFoundException("Error", filePath);
        }

        public bool CheckIntegrity()
        {
            string Content = File.ReadAllText(FileLocation);
            int quote = Content.Count(x => x == '"');
            int braceleft = Content.Count(x => x == '{');
            int braceright = Content.Count(x => x == '}');

            return ((braceleft == braceright) && (quote % 2 == 0));
        }

        public ACF_Struct ACFFileToStruct()
        {
            return ACFFileToStruct(File.ReadAllText(FileLocation));
        }

        private ACF_Struct ACFFileToStruct(string RegionToReadIn)
        {
            ACF_Struct ACF = new ACF_Struct();
            int LengthOfRegion = RegionToReadIn.Length;
            int CurrentPos = 0;
            while (LengthOfRegion > CurrentPos)
            {
                int FirstItemStart = RegionToReadIn.IndexOf('"', CurrentPos);
                if (FirstItemStart == -1)
                    break;
                int FirstItemEnd = RegionToReadIn.IndexOf('"', FirstItemStart + 1);
                if (FirstItemEnd == -1)
                    break;
                CurrentPos = FirstItemEnd + 1;
                string FirstItem = RegionToReadIn.Substring(FirstItemStart + 1, FirstItemEnd - FirstItemStart - 1);

                int SecondItemStartQuote = RegionToReadIn.IndexOf('"', CurrentPos);
                int SecondItemStartBraceleft = RegionToReadIn.IndexOf('{', CurrentPos);
                if (SecondItemStartBraceleft == -1 || SecondItemStartQuote < SecondItemStartBraceleft)
                {
                    int SecondItemEndQuote = RegionToReadIn.IndexOf('"', SecondItemStartQuote + 1);
                    if (SecondItemEndQuote >= 0)
                    {
                        string SecondItem = RegionToReadIn.Substring(SecondItemStartQuote + 1, SecondItemEndQuote - SecondItemStartQuote - 1);
                        CurrentPos = SecondItemEndQuote + 1;
                        if (FirstItem.Equals("installdir"))
                        {
                            this.installdir = SecondItem;
                        }
                        if (FirstItem.Equals("name"))
                        {
                            this.gameName = SecondItem;
                        }
                        
                        if (FirstItem.Equals("appid"))
                        {
                            this.appId = SecondItem;
                        }

                        if (!ACF.SubItems.ContainsKey(FirstItem))
                        {
                            ACF.SubItems.Add(FirstItem, SecondItem);
                        }
                    }
                }
                else
                {
                    int SecondItemEndBraceright = RegionToReadIn.NextEndOf('{', '}', SecondItemStartBraceleft + 1);
                    ACF_Struct ACFS = ACFFileToStruct(RegionToReadIn.Substring(SecondItemStartBraceleft + 1, SecondItemEndBraceright - SecondItemStartBraceleft - 1));
                    CurrentPos = SecondItemEndBraceright + 1;
                    ACF.SubACF.Add(FirstItem, ACFS);
                }
            }

            return ACF;
        }

    }

    public class ACF_Struct
    {
        public Dictionary<string, ACF_Struct> SubACF { get; private set; }
        public Dictionary<string, string> SubItems { get; private set; }

        public ACF_Struct()
        {
            SubACF = new Dictionary<string, ACF_Struct>();
            SubItems = new Dictionary<string, string>();
        }

        public void WriteToFile(string File)
        {

        }

        public override string ToString()
        {
            return ToString(0);
        }

        private string ToString(int Depth)
        {
            StringBuilder SB = new StringBuilder();
            foreach (KeyValuePair<string, string> item in SubItems)
            {
                SB.Append('\t', Depth);
                SB.AppendFormat("\"{0}\"\t\t\"{1}\"\r\n", item.Key, item.Value);
            }
            foreach (KeyValuePair<string, ACF_Struct> item in SubACF)
            {
                SB.Append('\t', Depth);
                SB.AppendFormat("\"{0}\"\n", item.Key);
                SB.Append('\t', Depth);
                SB.AppendLine("{");
                SB.Append(item.Value.ToString(Depth + 1));
                SB.Append('\t', Depth);
                SB.AppendLine("}");
            }
            return SB.ToString();
        }
    }

    static class Extension
    {
        public static int NextEndOf(this string str, char Open, char Close, int startIndex)
        {
            if (Open == Close)
                throw new Exception("\"Open\" and \"Close\" char are equivalent!");

            int OpenItem = 0;
            int CloseItem = 0;
            for (int i = startIndex; i < str.Length; i++)
            {
                if (str[i] == Open)
                {
                    OpenItem++;
                }
                if (str[i] == Close)
                {
                    CloseItem++;
                    if (CloseItem > OpenItem)
                        return i;
                }
            }
            throw new Exception("Not enough closing characters!");
        }
    }
}
