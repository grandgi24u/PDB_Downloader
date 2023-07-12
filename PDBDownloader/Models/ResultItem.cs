using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PDBDownloader.Models
{
    class ResultItem
    {
        public string filename { get; set; }
        public double clashscore { get; set; }
        public string struct_pdbx_descriptors { get; set; }
        public string method { get; set; }
    }
}
