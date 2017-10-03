using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml.Serialization;

namespace DependencyCheck
{
    [XmlRoot("Dependencies")]
    public class Dependencies: List<Dependency>
    {
        //[XmlElement("Dependencies")]
        //public List<Dependency> Dependency{ get; set; }
    }

    [XmlRoot("Dependency")]
    public class Dependency
    {
        public enum ProcessorType
        {
            [XmlEnum("All")]
            All,
            [XmlEnum("x86")]
            x86,
            [XmlEnum("x64")]
            x64
        }
        [XmlElement("ProcessorType")]
        public ProcessorType CPU { get; set; }
        public string Name { get; set; }
        public string InstallUrl { get; set; }
        public string MoreInfoUrl { get; set; }
        public InstallationValidation Validation { get; set; }
    }

    public class InstallationValidation
    {
        public bool RequiresAll { get; set; }
        [XmlArray("Checks")]
        [XmlArrayItem(typeof(RegistryBaseValidationCheck), ElementName = "RegistryCheck")]
        [XmlArrayItem(typeof(FileBaseValidationCheck), ElementName = "FileCheck")]
        [XmlArrayItem(typeof(ProductCodeValidationCheck), ElementName = "ProductCodeCheck")]
        public List<BaseValidationCheck> Checks { get; set; }
    }
}
