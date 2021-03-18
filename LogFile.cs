using System;
using System.Collections.Generic;

namespace STAT
{
    public class LogFile
    {
        public string FolderName { get; set; }
        
        public string FolderPath { get; set; }

        public string Name { get; set; }

        public string FilePath { get; set; }

        public DateTime FileDate { get; set; }

        public List<string> Content { get; set; }

        public string Result { get; set; }

        public string TextColor { get; set; }
    }
}