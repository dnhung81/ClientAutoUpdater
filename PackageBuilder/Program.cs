using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace PackageBuilder
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args != null && args.Length == 4 && args[0] == "extract")
            {
                ExtractPackage(args[1], args[3], args[2]);
            }
            else if(args != null && args.Length == 3 && args[0] == "generate")
            {
                string path = args[1], output = args[2];

                DirectoryInfo outputDirectory = GetOutputLocation(output);

                Manifest manifest = new Manifest();

                DirectoryInfo sourceDirectory = new DirectoryInfo(path);
                DirectoryInfo[] subDirectories = sourceDirectory.GetDirectories();
                foreach (DirectoryInfo subDirectory in subDirectories)
                {
                    manifest.Add(GeneratePackage(subDirectory, outputDirectory));
                }

                //manifest.Id = GenerateManifestId(manifest);

                XmlSerializer x = new XmlSerializer(typeof(Manifest));
                using (XmlWriter manifestWriter = XmlWriter.Create(output + "/manifest.xml"))
                {
                    x.Serialize(manifestWriter, manifest);
                }
            }
            else if(args != null && args.Length == 7 && args[0] == "teamcity")
            {
                string szTeamCityServer = args[1],
                    szBuildId = args[2],
                    szBranchName = args[3],
                    szUserName = args[4],
                    szPassword = args[5],
                    fileName = args[6];
                WebRequest getBuildInfo = WebRequest.Create(szTeamCityServer + "/app/rest/builds/buildType:(id:" + szBuildId + "),branch:" + szBranchName + ",status:SUCCESS/artifacts");
                getBuildInfo.Credentials = new System.Net.NetworkCredential(szUserName, szPassword);
                WebResponse webResponse = getBuildInfo.GetResponse();
                string szResponse = string.Empty;
                if (webResponse != null)
                {
                    Stream objStream = webResponse.GetResponseStream();
                    StreamReader objReader = new StreamReader(objStream);
                    while (!objReader.EndOfStream)
                    {
                        szResponse += objReader.ReadLine();
                    }
                }
                XmlDocument buildInfoDoc = new XmlDocument();
                buildInfoDoc.LoadXml(szResponse);
                XmlNodeList files = buildInfoDoc.SelectNodes("/files/file");
                if(files != null)
                {
                    foreach(XmlNode file in files)
                    {
                        if(file.Attributes != null)
                        {
                            foreach(XmlAttribute attribute in file.Attributes)
                            {
                                if(attribute.Name == "href")
                                {
                                    string szHref = attribute.Value;
                                    if (szHref.EndsWith(".zip"))
                                    {
                                        int startId = szHref.IndexOf("id:");
                                        if (startId >= 0)
                                        {
                                            int endId = szHref.IndexOf("/", startId);
                                            string szId = szHref.Substring(startId + 3, endId - startId - 3);
                                            int startFile = szHref.LastIndexOf("/");
                                            if (startFile >= 0)
                                            {
                                                string szArtifactName = szHref.Substring(startFile + 1);
                                                string szArtifact = szTeamCityServer + "/repository/download/" + szBuildId + "/" + szId + ":id/" + szArtifactName;
                                                WebClient downloadClient = new WebClient();
                                                downloadClient.Credentials = new System.Net.NetworkCredential(szUserName, szPassword);
                                                downloadClient.DownloadFile(szArtifact, szArtifactName);
                                                if(File.Exists(szArtifactName))
                                                {
                                                    Directory.CreateDirectory("Temp");
                                                    ZipFile.ExtractToDirectory(szArtifactName, @"Temp\Client");
                                                    DirectoryInfo outputDirectory = GetOutputLocation(Directory.GetCurrentDirectory());

                                                    Manifest manifest = new Manifest();

                                                    DirectoryInfo sourceDirectory = new DirectoryInfo("Temp");
                                                    DirectoryInfo[] subDirectories = sourceDirectory.GetDirectories();
                                                    foreach (DirectoryInfo subDirectory in subDirectories)
                                                    {
                                                        manifest.Add(GeneratePackage(subDirectory, outputDirectory));
                                                    }

                                                    //manifest.Id = GenerateManifestId(manifest);

                                                    XmlSerializer x = new XmlSerializer(typeof(Manifest));
                                                    using (XmlWriter manifestWriter = XmlWriter.Create(outputDirectory + "/manifest.xml"))
                                                    {
                                                        x.Serialize(manifestWriter, manifest);
                                                    }
                                                    Directory.Delete("Temp", true);
                                                    File.Delete(szArtifactName);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else if (args != null && args.Length == 2)
            {
                if (args[0] == "Client")
                {
                    string szCurrentDirectory = Directory.GetCurrentDirectory();

                    if (Directory.Exists(Path.Combine(szCurrentDirectory, "bin"))
                        && Directory.Exists(Path.Combine(szCurrentDirectory, "appdata"))
                        && Directory.Exists(Path.Combine(szCurrentDirectory, "data"))
                        && Directory.Exists(Path.Combine(szCurrentDirectory, "upgrade")))
                    {
                        Directory.CreateDirectory(Path.Combine(szCurrentDirectory, "Temp"));
                        string szDestination = Path.Combine(szCurrentDirectory, @"Temp\Client");
                        Directory.CreateDirectory(szDestination);
                        DirectoryCopy(Path.Combine(szCurrentDirectory, "bin"), Path.Combine(szDestination, "bin"), true);
                        DirectoryCopy(Path.Combine(szCurrentDirectory, "appdata"), Path.Combine(szDestination, "appdata"), true);
                        DirectoryCopy(Path.Combine(szCurrentDirectory, "data"), Path.Combine(szDestination, "data"), true);
                        DirectoryCopy(Path.Combine(szCurrentDirectory, "upgrade"), Path.Combine(szDestination, "upgrade"), true);
                        File.Copy(Path.Combine(szCurrentDirectory, "install.config"), Path.Combine(szDestination, "install.config"));
                        DirectoryInfo outputDirectory = GetOutputLocation(szCurrentDirectory);

                        Manifest manifest = new Manifest();

                        DirectoryInfo sourceDirectory = new DirectoryInfo(Path.Combine(szCurrentDirectory, "Temp"));
                        DirectoryInfo[] subDirectories = sourceDirectory.GetDirectories();
                        foreach (DirectoryInfo subDirectory in subDirectories)
                        {
                            manifest.Add(GeneratePackage(subDirectory, outputDirectory));
                        }

                        //manifest.Id = GenerateManifestId(manifest);

                        XmlSerializer x = new XmlSerializer(typeof(Manifest));
                        using (XmlWriter manifestWriter = XmlWriter.Create(outputDirectory + "/manifest.xml"))
                        {
                            x.Serialize(manifestWriter, manifest);
                        }

                        WebClient client = new WebClient();
                        client.UploadFile(args[1], outputDirectory + "/manifest.xml");
                        client.UploadFile(args[1], outputDirectory + "/Client.zip");
                        Directory.Delete(Path.Combine(szCurrentDirectory, "Temp"), true);
                        File.Delete(outputDirectory + "/manifest.xml");
                        File.Delete(outputDirectory + "/Client.zip");
                    }
                }
                else if (args[0] == "CaptureEngine")
                {
                }
            }
        }

        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);
            DirectoryInfo[] dirs = dir.GetDirectories();

            // If the source directory does not exist, throw an exception.
            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            // If the destination directory does not exist, create it.
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }


            // Get the file contents of the directory to copy.
            FileInfo[] files = dir.GetFiles();

            foreach (FileInfo file in files)
            {
                // Create the path to the new copy of the file.
                string temppath = Path.Combine(destDirName, file.Name);

                // Copy the file.
                file.CopyTo(temppath, false);
            }

            // If copySubDirs is true, copy the subdirectories.
            if (copySubDirs)
            {

                foreach (DirectoryInfo subdir in dirs)
                {
                    // Create the subdirectory.
                    string temppath = Path.Combine(destDirName, subdir.Name);

                    // Copy the subdirectories.
                    DirectoryCopy(subdir.FullName, temppath, copySubDirs);
                }
            }
        }


        private static string GenerateManifestId(Manifest manifest)
        {
            string id = "";
            foreach (Package package in manifest)
            {
                id = id + package.Id;
            }

            string hash="";
            using (var md5 = MD5.Create())
            {
                byte[] hashBytes = md5.ComputeHash(Encoding.ASCII.GetBytes(id));
                foreach (byte bt in hashBytes)
                {
                    hash = hash + bt.ToString("x2");
                }
            }
            return hash;
        }

        static DirectoryInfo GetOutputLocation(string output)
        {
            DirectoryInfo outputDirectory = null;
            if (!Directory.Exists(output))
            {
                Directory.CreateDirectory(output);
            }
            outputDirectory = new DirectoryInfo(output);
            return outputDirectory;
        }

        static Package GeneratePackage(DirectoryInfo folder, DirectoryInfo outputDirectory)
        {
            Package package = null;
            if (folder.Exists)
            {
                string zipFile = outputDirectory.FullName + "/" + folder.Name + ".tmp";
                string outputFile = outputDirectory.FullName + "/" + folder.Name + ".zip";

                if (File.Exists(zipFile))
                {
                    File.Delete(zipFile);
                }
                if (File.Exists(outputFile))
                {
                    File.Delete(outputFile);
                }

                package = new Package();

                //Create Zip
                ZipFile.CreateFromDirectory(folder.FullName, zipFile, CompressionLevel.Optimal, false);
                
                //Create Hash
                using (var md5 = MD5.Create())
                {
                    using (var stream = File.OpenRead(zipFile))
                    {
                        byte[] hashBytes = md5.ComputeHash(stream);
                        foreach (byte bt in hashBytes)
                        {
                            package.Id = package.Id + bt.ToString("x2");
                        }
                    }
                }

                //Encrypt
                using (FileStream zipFileStream = new FileStream(zipFile, FileMode.Open, FileAccess.Read))
                {
                    using (FileStream packageStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write))
                    {
                        using (DESCryptoServiceProvider DES = new DESCryptoServiceProvider())
                        {
                            DES.Key = ASCIIEncoding.ASCII.GetBytes(package.Id.Substring(0,8));
                            DES.IV = ASCIIEncoding.ASCII.GetBytes(package.Id.Substring(package.Id.Length-8));
                            ICryptoTransform desencrypt = DES.CreateEncryptor();

                            using (CryptoStream cryptostream = new CryptoStream(packageStream, desencrypt,CryptoStreamMode.Write))
                            {
                                byte[] bytearrayinput = new byte[zipFileStream.Length];
                                zipFileStream.Read(bytearrayinput, 0, bytearrayinput.Length);
                                cryptostream.Write(bytearrayinput, 0, bytearrayinput.Length);
                                cryptostream.Close();
                            }
                        }
                        packageStream.Close();
                    }
                    zipFileStream.Close();
                }

                //Delete temp
                if (File.Exists(zipFile))
                {
                    File.Delete(zipFile);
                }
                package.Name = folder.Name + ".zip";
                package.Location = folder.Name + @"\";
            }
            return package;
        }

        private static void ExtractPackage(string packageFile, string outputDir, string packageId)
        {
            string tempFile = Path.GetTempPath() + "\\" + packageId + ".tmp";
            //Decrypt
            using (FileStream packageStream = new FileStream(packageFile, FileMode.Open, FileAccess.Read))
            {
                using (FileStream zipFileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
                {
                    using (DESCryptoServiceProvider des = new DESCryptoServiceProvider())
                    {
                        des.Key = Encoding.ASCII.GetBytes(packageId.Substring(0, 8));
                        des.IV = Encoding.ASCII.GetBytes(packageId.Substring(packageId.Length - 8));
                        ICryptoTransform decryptor = des.CreateDecryptor();

                        using (
                            CryptoStream cryptostream = new CryptoStream(zipFileStream, decryptor, CryptoStreamMode.Write))
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

            ZipFile.ExtractToDirectory(tempFile, outputDir);
            File.Delete(tempFile);
        }
    }
}
