using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LyndaDecryptor
{
    public class VideoInfo
    {
        public string CourseTitle { get; set; }
        public string ChapterTitle { get; set; }
        public string VideoTitle { get; set; }
        public string VideoID { get; set; }
        public string CourseID { get; set; }

        public int ChapterIndex { get; set; }
        public int VideoIndex { get; set; }
    }
}
