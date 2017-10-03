using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace Upgrader
{
    class Program
    {
        private static string _manifestName = "manifest.xml";
        static void Main(string[] args)
        {
            string serverPath=null;
            string installDirectory = null;

            for (int i=0;i<args.Length; i++)
            {
                if (args[i].ToLowerInvariant() == "-server")
                {
                    serverPath = args[i + 1];
                }
                else if (args[i].ToLowerInvariant() == "-install")
                {
                    installDirectory = args[i + 1];
                }
            }

            if (serverPath == null)
            {
                Console.WriteLine("No server provided.  Use the argument -server to provide the server.");
            }

            if (installDirectory == null)
            {
                //If no path provided, assume we are running from the bin, so determine the location automatically.
                string location = System.Reflection.Assembly.GetExecutingAssembly().Location;
                installDirectory = Directory.GetParent(location).Parent.FullName + @"\";
                //installDirectory = Environment.ExpandEnvironmentVariables(@"%appdata%\ANCILE\uPerform\App\");
            }

            Run(serverPath, installDirectory);
        }

        private static void Run(string serverPath ,string installDirectory)
        { 
            if (!Directory.Exists(installDirectory))
            {
                Directory.CreateDirectory(installDirectory);
            }
            
            Manifest localManifest = LoadLocalManifest(installDirectory + _manifestName);
            //TODO: Move to URL.  Maybe also support network share?
            Manifest serverManifest = LoadLocalManifest(serverPath + _manifestName);

            List<Package> updates = GetChangedPackages(localManifest, serverManifest);

            PerformUpdate(installDirectory, serverPath, updates);
            
            SaveLocalManifest(installDirectory + _manifestName, serverManifest);
        }

        private static Manifest LoadLocalManifest(string manifestPath)
        {
            Console.Write("Loading " + manifestPath + "...");
            Manifest manifest = null;
            try
            {
                if (File.Exists(manifestPath))
                {
                    XmlSerializer x = new XmlSerializer(typeof(Manifest));
                    using (StreamReader manifestStream = new StreamReader(manifestPath))
                    {
                        manifest = (Manifest) x.Deserialize(manifestStream);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Unable to load local manifest: " + e.Message);
                File.Delete(manifestPath);
            }
            if (manifest == null)
            {
                Console.WriteLine("Not Found");
            }
            else
            {
                Console.WriteLine("Found");
            }
            return manifest;
        }

        private static void SaveLocalManifest(string manifestPath, Manifest manifest)
        {
            try
            {
                if (File.Exists(manifestPath))
                {
                    File.Delete(manifestPath);
                }

                XmlSerializer x = new XmlSerializer(typeof(Manifest));
                using (XmlWriter manifestWriter = XmlWriter.Create(manifestPath))
                {
                    x.Serialize(manifestWriter, manifest);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Unable to save local manifest: " + e.Message);
                File.Delete(manifestPath);
            }
        }


        private static List<Package> GetChangedPackages(Manifest local, Manifest server)
        {
            Console.WriteLine("Check for updates to " + server.Count.ToString() + " packages.");
            List<Package> packages = new List<Package>();

            foreach (Package serverPackage in server)
            {
                Console.Write("Check for local instance of " + serverPackage.Name + "...");
                Package localPackage = GetPackage(local, serverPackage.Name);
                if (localPackage == null)
                {
                    Console.WriteLine("Not Found");
                }
                else
                {
                    Console.WriteLine("Found");
                }
                if (localPackage == null || localPackage.Id != serverPackage.Id)
                {
                    Console.WriteLine("Add package.");
                    packages.Add(serverPackage);
                }
            }
            return packages;
        }

        private static Package GetPackage(Manifest manifest, string name)
        {
            if (manifest != null)
            {
                foreach (Package package in manifest)
                {
                    if (package.Name == name)
                    {
                        return package;
                    }
                }
            }
            return null;
        }

        private static void PerformUpdate(string installDirectory, string server, List<Package> updates)
        {
            string tempDirectory = installDirectory + @"temp\";
            if (!Directory.Exists(tempDirectory))
            {
                Directory.CreateDirectory(tempDirectory);
            }
            foreach (Package package in updates)
            {
                string packageFile = DownloadPackage(tempDirectory, server, package);
                ExtractPackage(installDirectory, package, packageFile, tempDirectory);
                File.Delete(packageFile);
            }
            Directory.Delete(tempDirectory);
        }

        private static string DownloadPackage(string tempDirectory, string serverPath, Package package)
        {
            string filePath = tempDirectory + package.Name;
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            File.Copy(serverPath + package.Name, filePath);
            return filePath;
        }

        private static void ExtractPackage(string installDirectory, Package package, string packageFile,
            string tempDirectory)
        {
            string tempFile = tempDirectory + package.Id + "tmp";
            //Decrypt
            using (FileStream packageStream = new FileStream(packageFile, FileMode.Open, FileAccess.Read))
            {
                using (FileStream zipFileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
                {
                    using (DESCryptoServiceProvider des = new DESCryptoServiceProvider())
                    {
                        des.Key = Encoding.ASCII.GetBytes(package.Id.Substring(0, 8));
                        des.IV = Encoding.ASCII.GetBytes(package.Id.Substring(package.Id.Length - 8));
                        ICryptoTransform decryptor = des.CreateDecryptor();

                        using (
                            CryptoStream cryptostream = new CryptoStream(zipFileStream, decryptor,CryptoStreamMode.Write))
                        {
                            byte[] bytearrayinput = new byte[packageStream.Length];
                            packageStream.Read(bytearrayinput, 0, bytearrayinput.Length);
                            cryptostream.Write(bytearrayinput, 0, bytearrayinput.Length);
                            cryptostream.Close();
                        }
                    }
                    zipFileStream.Close();
                }
                packageStream.Close();
            }

            if (Directory.Exists(installDirectory + package.Location))
            {
                Directory.Delete(installDirectory + package.Location,true);
            }
            ZipFile.ExtractToDirectory(tempFile, installDirectory + package.Location);
            File.Delete(tempFile);
        }
    }
}
