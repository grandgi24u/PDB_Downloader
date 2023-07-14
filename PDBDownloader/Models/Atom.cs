using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PDBDownloader.Models
{
    class Atom
    {
        public string atomId { get; set; }
        public string type_symbol { get; set; }
        public string label_atom_id { get; set; }
        public string label_comp_id { get; set; }
        public string Cartn_x { get; set; }
        public string Cartn_y { get; set; }
        public string Cartn_z { get; set; }
        public string occupancy { get; set; }
        public string B_iso_or_equiv { get; set; }
        public string Id_File { get; set; }
    }
}
