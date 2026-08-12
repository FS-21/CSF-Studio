using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace CsfStudio.UI
{
    public class AboutDialog : Form
    {
        public AboutDialog()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "ℹ️ About CSF Studio";
            this.Size = new Size(480, 360);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;

            var picIcon = new PictureBox
            {
                Location = new Point(20, 20),
                Size = new Size(64, 64),
                SizeMode = PictureBoxSizeMode.Zoom
            };
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                using (var pngStrm = asm.GetManifestResourceStream("CsfStudio.app_icon.png"))
                {
                    if (pngStrm != null)
                    {
                        picIcon.Image = Image.FromStream(pngStrm);
                    }
                }

                if (picIcon.Image == null)
                {
                    using (var icoStrm = asm.GetManifestResourceStream("CsfStudio.app_icon.ico"))
                    {
                        if (icoStrm != null)
                        {
                            using (var embeddedIcon = new Icon(icoStrm, 64, 64))
                            {
                                picIcon.Image = embeddedIcon.ToBitmap();
                            }
                        }
                    }
                }

                if (picIcon.Image == null)
                {
                    string pngPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.png");
                    string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.ico");

                    if (File.Exists(pngPath))
                    {
                        picIcon.Image = Image.FromFile(pngPath);
                    }
                    else if (File.Exists(icoPath))
                    {
                        using (var customIcon = new Icon(icoPath, 64, 64))
                        {
                            picIcon.Image = customIcon.ToBitmap();
                        }
                    }
                    else if (this.Icon != null)
                    {
                        var bmp = new Bitmap(64, 64);
                        using (var g = Graphics.FromImage(bmp))
                        {
                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                            g.DrawIcon(this.Icon, new Rectangle(0, 0, 64, 64));
                        }
                        picIcon.Image = bmp;
                    }
                }
            }
            catch { }

            var lblTitle = new Label
            {
                Text = "CSF Studio",
                Location = new Point(95, 20),
                Size = new Size(350, 28),
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.DarkSlateBlue
            };

            var lblVersion = new Label
            {
                Text = $"Version {AppInfo.Version}",
                Location = new Point(95, 48),
                Size = new Size(350, 20),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.DimGray
            };

            var lblSubtitle = new Label
            {
                Text = "Created by FS-21",
                Location = new Point(95, 68),
                Size = new Size(350, 20),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.DarkGray
            };

            var sepHeader = new Label
            {
                BorderStyle = BorderStyle.Fixed3D,
                Location = new Point(20, 100),
                Size = new Size(425, 2)
            };

            var txtDesc = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                TabStop = false,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(20, 115),
                Size = new Size(425, 150),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                BackColor = SystemColors.ControlLightLight,
                Text = "CSF Studio is a specialized multi-file string table (.CSF) editor created via Live-Coding & AI Pair-Programming, designed for Command & Conquer: Red Alert 2 and Yuri's Revenge modding and localization.\r\n\r\n" +
                       "Key Features:\r\n" +
                       " • Simultaneous Multi-CSF Sessions & Drag & Drop (.csf & .txt import)\r\n" +
                       " • Key and/or Value search & filtering with Regular Expressions (RegEx)\r\n" +
                       " • Automated INI/MAP key scanner and visual diff comparison\r\n" +
                       " • Plain text UTF-8 import and export for string tables\r\n" +
                       " • ANSI / Codepage to Unicode UTF-16 conversion\r\n" +
                       " • Batch key pattern renaming & automatic session backups\r\n\r\n" +
                       "Format Specification:\r\n" +
                       " • Westwood Studios 32-bit CSF Binary Format Standard (v3)\r\n\r\n" +
                       "License:\r\n" +
                       " • GNU General Public License v3.0 (GPLv3)\r\n\r\n" +
                       "Disclaimer:\r\n" +
                       " • Provided 'AS IS' without warranty of any kind. Always backup your original .CSF files before editing. Use at your own risk.\r\n\r\n" +
                       "Author:\r\n" +
                       " • FS-21"
            };

            var lnkGitHub = new LinkLabel
            {
                Text = "🌐 GitHub Repository: FS-21/CSF-Studio",
                Location = new Point(20, 282),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                LinkColor = Color.FromArgb(0, 102, 204)
            };
            lnkGitHub.LinkClicked += (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start("https://github.com/FS-21/CSF-Studio");
                }
                catch { }
            };

            var btnOK = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(360, 278),
                Size = new Size(85, 28),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                UseVisualStyleBackColor = true
            };

            this.Controls.Add(picIcon);
            this.Controls.Add(lblTitle);
            this.Controls.Add(lblVersion);
            this.Controls.Add(lblSubtitle);
            this.Controls.Add(sepHeader);
            this.Controls.Add(txtDesc);
            this.Controls.Add(lnkGitHub);
            this.Controls.Add(btnOK);

            this.AcceptButton = btnOK;

            this.Shown += (s, e) =>
            {
                txtDesc.SelectionStart = 0;
                txtDesc.SelectionLength = 0;
                btnOK.Focus();
            };
        }
    }
}
