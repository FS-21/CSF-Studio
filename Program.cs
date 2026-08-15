using System;
using System.Windows.Forms;
using CsfStudio.UI;

namespace CsfStudio
{
    public static class AppInfo
    {
        public const string Version = "1.5.6";
        public const string Title = "CSF Studio";
        public static string FullVersion => $"v{Version}";
        public static string WindowTitle => $"{Title} v{Version} (Another C&C CSF String Table Editor)";
    }

    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var initialFiles = new System.Collections.Generic.List<string>();
            if (args != null)
            {
                foreach (string arg in args)
                {
                    if (!string.IsNullOrWhiteSpace(arg) && System.IO.File.Exists(arg))
                    {
                        initialFiles.Add(arg);
                    }
                }
            }

            var config = CsfStudio.Core.ConfigManager.LoadConfig();
            CsfStudio.Core.LanguageManager.Initialize(config.UiLanguage);

            Application.Run(new MainForm(initialFiles));
        }
    }
}
