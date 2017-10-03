using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace DependencyCheck
{
    /// <summary>
    /// In theory is this works, in practice I am not sure.
    /// </summary>
    public class ProductCodeValidationCheck : BaseValidationCheck
    {
        private int INSTALLSTATE_DEFAULT = 5;

        public string ProductCode { get; set; }

        public override bool Validate()
        {
            return MsiQueryProductState(ProductCode) == INSTALLSTATE_DEFAULT;
        }

        [DllImport("msi.dll")]
        public static extern Int32 MsiQueryProductState(string szProduct);
    }
}
