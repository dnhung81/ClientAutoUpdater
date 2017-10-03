using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace DependencyCheck
{
    public class FileBaseValidationCheck : BaseValidationCheck
    {
        public string Path { get; set; }
        public string Version { get; set; }

        public override bool Validate()
        {
            string expandedPath = Environment.ExpandEnvironmentVariables(Path);
            if (!File.Exists(expandedPath))
            {
                return false;
            }
            else if (string.IsNullOrEmpty(Version))
            {
                //Any version is good
                return true;
            }
            else if (System.IO.Path.GetExtension(expandedPath) == ".dll")
            {
                FileVersionInfo myFileVersionInfo = FileVersionInfo.GetVersionInfo(expandedPath);
                string fileVersion = myFileVersionInfo.ProductVersion;
                if ((Version.EndsWith("*") && fileVersion.StartsWith(Version.Substring(0, Version.Length - 1))) || Version == fileVersion)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            return false;
        }
    }
}
