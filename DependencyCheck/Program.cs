using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace DependencyCheck
{
    class Program
    {
        static void Main(string[] args)
        {
            Dependencies dependencies = LoadDependencies();
            //dependencies[0].CPU= Dependency.ProcessorType.x86;
            //XmlSerializer x = new XmlSerializer(typeof(Dependencies));
            //XmlTextWriter w = new XmlTextWriter(@"C:\Users\dhumphreys\desktop\d.xml", Encoding.ASCII);
            //x.Serialize(w,dependencies);

            foreach (Dependency dependency in dependencies)
            {
                if (dependency.CPU == Dependency.ProcessorType.All || 
                    (dependency.CPU == Dependency.ProcessorType.x64 && System.Environment.Is64BitOperatingSystem) ||
                    (dependency.CPU == Dependency.ProcessorType.x86 && !System.Environment.Is64BitOperatingSystem))
                {
                    bool? isDependencyValid = null;
                    foreach (BaseValidationCheck check in dependency.Validation.Checks)
                    {
                        bool isCheckValid = check.Validate();

                        if (isDependencyValid.HasValue == false)
                        {
                            isDependencyValid = isCheckValid;
                        }
                        else if (dependency.Validation.RequiresAll && !isCheckValid)
                        {
                            isDependencyValid = false;
                            break;
                        }
                        else if (!dependency.Validation.RequiresAll && isCheckValid)
                        {
                            isDependencyValid = true;
                            break;
                        }
                    }

                    if (isDependencyValid.HasValue && isDependencyValid.Value)
                    {
                        Console.WriteLine(dependency.Name + " Is Valid!");
                    }
                    else
                    {
                        Console.WriteLine("DOH!" + dependency.Name + " Is NOT Valid!");
                    }
                }
            }

            Console.ReadLine();
        }

        static Dependencies LoadDependencies()
        {
            Assembly self = Assembly.GetExecutingAssembly();
            XmlSerializer x = new XmlSerializer(typeof(Dependencies));
            Stream stream = self.GetManifestResourceStream("DependencyCheck.Dependencies.xml");
            if (stream != null)
            {
                return (Dependencies) x.Deserialize(stream);
            }
            else
            {
                throw new Exception("Unable to load dependency list.");
            }


        }
    }
}
