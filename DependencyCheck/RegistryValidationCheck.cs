using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DependencyCheck
{
    public class RegistryBaseValidationCheck : BaseValidationCheck
    {
        public string Key { get; set; }
        public string Subkey { get; set; }
        public string Value { get; set; }
        public string Expected { get; set; }

        public override bool Validate()
        {
            bool isValid = false;
            try
            {
                using (RegistryKey key = GetKey().OpenSubKey(Subkey))
                {
                    if (key != null)
                    {
                        object o = key.GetValue(Value);
                        if (o != null)
                        {
                            if (o.ToString() == Expected)
                            {
                                isValid = true;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            return isValid;
        }

        private RegistryKey GetKey()
        {
            switch (Key)
            {
                case "HKLM":
                    return Registry.LocalMachine;
                case "HKCU":
                    return Registry.CurrentUser;
                case "HKCR":
                    return Registry.ClassesRoot;
                default:
                    throw new Exception("Invalid registry key: " + Key);
            }
        }
    }
}
