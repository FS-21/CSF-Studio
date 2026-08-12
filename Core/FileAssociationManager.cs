using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CsfStudio.Core
{
    public static class FileAssociationManager
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private const uint SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNF_IDLIST = 0x0000;

        public static bool IsCsfAssociated()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\CSFStudio.Document\shell\open\command"))
                {
                    if (key != null)
                    {
                        string cmd = key.GetValue("") as string;
                        if (!string.IsNullOrEmpty(cmd) && cmd.Contains(Application.ExecutablePath))
                        {
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        public static bool AssociateCsfExtension()
        {
            try
            {
                string exePath = Application.ExecutablePath;

                using (var extKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.csf"))
                {
                    extKey.SetValue("", "CSFStudio.Document");
                }

                using (var docKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\CSFStudio.Document"))
                {
                    docKey.SetValue("", "Command & Conquer String Table File");

                    using (var iconKey = docKey.CreateSubKey("DefaultIcon"))
                    {
                        iconKey.SetValue("", $"\"{exePath}\",0");
                    }

                    using (var cmdKey = docKey.CreateSubKey(@"shell\open\command"))
                    {
                        cmdKey.SetValue("", $"\"{exePath}\" \"%1\"");
                    }
                }

                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to associate .CSF extension in registry:\n{ex.Message}", "Association Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }
    }
}
