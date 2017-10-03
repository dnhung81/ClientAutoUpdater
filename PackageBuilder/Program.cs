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

namespace PackageBuilder
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args[0] == "extract")
            {

            }
            else
            {
                string path = @"C:\Users\dhumphreys\Desktop\bin";
                string output = @"C:\Users\dhumphreys\Desktop\packages";


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
                string outputFile = outputDirectory.FullName + "/" + folder.Name + ".pck";

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
                package.Name = folder.Name + ".pck";
                package.Location = folder.Name + @"\";
            }
            return package;
        }

        private static void ExtractPackage(string packageFile, string outputDir, string packageId)
        {
            string tempFile = Path.GetTempPath() + "\\" + DateTime.Now.ToLongTimeString() + ".tmp";
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
