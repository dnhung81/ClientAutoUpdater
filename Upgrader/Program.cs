using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private static string _serverManifestName = "ServerManifest.xml";
        private static string _localManifestName = "LocalManifest.xml";

        static void Main(string[] args)
        {
            try
            {
                Process[] procs = Process.GetProcessesByName("uPerform");
                procs[0].Kill();
            }
            catch (Exception ex)
            {
            }

            string serverPath=null;
            string installDirectory = null;
            string clientPackages = null;
            string cePackages = null;

            for (int i=0;i<args.Length; i++)
            {
                if (args[i].ToLowerInvariant() == "-server")
                {
                    serverPath = args[i + 1];
                }
                else if (args[i].ToLowerInvariant() == "-install")
                {
                    installDirectory = args[i + 1];
                    installDirectory = installDirectory.Trim('\"') + @"\";
                }
                else if (args[i].ToLowerInvariant() == "-upcpackages")
                {
                    clientPackages = args[i + 1];
                }
                else if (args[i].ToLowerInvariant() == "-cepackages")
                {
                    cePackages = args[i + 1];
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

            if (clientPackages != null && cePackages != null)
            {
                string location = System.Reflection.Assembly.GetExecutingAssembly().Location;
                installDirectory = Directory.GetParent(location).FullName + @"\";

                serverPath = installDirectory + @"Client\";
                Console.WriteLine("Download uPerform Client packages");
                DownloadServerManifest(clientPackages, serverPath);
                DownloadServerPackages(clientPackages, serverPath);
                Run(serverPath, installDirectory);

                serverPath = serverPath + @"bin\CaptureEngine\";
                Console.WriteLine("Download Capture Engine packages");
                DownloadServerManifest(cePackages, serverPath);
                DownloadServerPackages(cePackages, serverPath);
                Run(serverPath, installDirectory + @"Client\bin\");
                
                if (File.Exists(installDirectory + _localManifestName))
                {
                    File.Copy(installDirectory + _localManifestName, installDirectory + @"Client/" + _localManifestName);
                    File.Delete(installDirectory + _localManifestName);
                }

                Process.Start(installDirectory + @"Client\bin\uPerform.exe");
            }
            else
            {
                Run(serverPath, installDirectory);

                if (Directory.Exists(installDirectory + "Client"))
                {
                    string des = Directory.GetParent(System.Reflection.Assembly.GetExecutingAssembly().Location).Parent.FullName + @"\";
                    DirectoryCopy(installDirectory + "Client", des, true);
                    Directory.Delete(installDirectory + "Client", true);
                }

                Process.Start("uPerform.exe");
            }
        }

        private static void DownloadServerManifest(string serverUrl, string des)
        {
            System.Net.WebClient webClient = new System.Net.WebClient();
            webClient.DownloadFile(serverUrl + "manifest.xml", _serverManifestName);

            if (!Directory.Exists(des))
            {
                Directory.CreateDirectory(des);
            }

            if (File.Exists(_serverManifestName))
            {
                File.Copy(_serverManifestName, des + _serverManifestName, true);
                File.Delete(_serverManifestName);
            }
        }

        private static void DownloadServerPackages(string serverUrl, string des)
        {
            System.Net.WebClient webClient = new System.Net.WebClient();
            Manifest serverManifest = LoadLocalManifest(des + _serverManifestName);
            foreach (Package serverPackage in serverManifest)
            {
                webClient.DownloadFile(serverUrl + serverPackage.Name, serverPackage.Name);
                if (File.Exists(serverPackage.Name))
                {
                    File.Copy(serverPackage.Name, des + serverPackage.Name, true);
                    File.Delete(serverPackage.Name);
                }
            }
        }

        private static void Run(string serverPath ,string installDirectory)
        { 
            if (!Directory.Exists(installDirectory))
            {
                Directory.CreateDirectory(installDirectory);
            }
            
            Manifest localManifest = LoadLocalManifest(installDirectory + _localManifestName);
            //TODO: Move to URL.  Maybe also support network share?
            Manifest serverManifest = LoadLocalManifest(serverPath + _serverManifestName);

            List<Package> updates = GetChangedPackages(localManifest, serverManifest);

            PerformUpdate(installDirectory, serverPath, updates);
            
            SaveLocalManifest(installDirectory + _localManifestName, serverManifest);
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

        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, true);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, temppath, copySubDirs);
                }
            }
        }
    }
}
