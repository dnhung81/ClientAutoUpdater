using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace PackageBuilder
{
    [XmlRoot("Manifest")]
    public class Manifest:List<Package>
    {
        //public string Id { get; set; }
    }

    public class Package
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string Location { get; set; }
    }
}
