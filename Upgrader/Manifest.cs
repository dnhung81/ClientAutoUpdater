using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Upgrader
{
    /// <summary>
    /// Opted to duplicate from PackageBuilder to avoid need for dependencies
    /// </summary>
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
