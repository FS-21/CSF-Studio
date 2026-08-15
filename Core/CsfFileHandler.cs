using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CsfStudio.Core
{
    public static class CsfFileHandler
    {
        // Magic signatures
        private const uint HEADER_MAGIC = 0x43534620; // " FSC" -> 'F','S','C',' '
        private const uint LABEL_MAGIC  = 0x4C424C20; // " LBL" -> 'L','B','L',' '
        private const uint STRING_MAGIC = 0x53545220; // " STR" -> 'S','T','R',' '
        private const uint STRW_MAGIC   = 0x53545257; // "STRW" -> 'S','T','R','W'
        private const uint STRW_MAGIC_ALT = 0x57535452; // "WSTR" -> 'W','S','T','R'

        public static CsfDocument Load(string filePath)
        {
            // FileShare.ReadWrite: don't block other processes (including our own Save)
            // from accessing the file while we read it.
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536))
            using (var br = new BinaryReader(fs, Encoding.ASCII))
            {
                return Read(br);
            }
        }

        public static CsfDocument Read(BinaryReader br)
        {
            uint magic = br.ReadUInt32();
            if (magic != HEADER_MAGIC)
            {
                throw new InvalidDataException($"Invalid CSF file signature. Expected 0x{HEADER_MAGIC:X8}, got 0x{magic:X8}.");
            }

            int version = br.ReadInt32();
            int labelCount = br.ReadInt32();
            int totalStringCount = br.ReadInt32();
            int reserved = br.ReadInt32();
            int languageId = br.ReadInt32();

            var doc = new CsfDocument
            {
                Version = version,
                Language = Enum.IsDefined(typeof(CsfLanguage), languageId)
                    ? (CsfLanguage)languageId
                    : CsfLanguage.Unknown
            };

            for (int i = 0; i < labelCount; i++)
            {
                uint lblMagic = br.ReadUInt32();
                if (lblMagic != LABEL_MAGIC)
                {
                    throw new InvalidDataException($"Invalid label signature at index {i}. Expected 'LBL ', got 0x{lblMagic:X8}.");
                }

                int stringCount = br.ReadInt32();
                int nameLength = br.ReadInt32();

                byte[] nameBytes = br.ReadBytes(nameLength);
                string labelName = Encoding.ASCII.GetString(nameBytes);

                var label = new CsfLabel(labelName);

                for (int j = 0; j < stringCount; j++)
                {
                    uint strMagic = br.ReadUInt32();
                    bool isStrw = (strMagic == STRW_MAGIC || strMagic == STRW_MAGIC_ALT);
                    if (strMagic != STRING_MAGIC && !isStrw)
                    {
                        throw new InvalidDataException($"Invalid string signature in label '{labelName}'. Expected 'STR ' or 'STRW', got 0x{strMagic:X8}.");
                    }

                    int strLength = br.ReadInt32();
                    string decodedValue;
                    if (strLength > 0)
                    {
                        byte[] rawBytes = br.ReadBytes(strLength * 2);
                        char[] chars = new char[strLength];
                        for (int k = 0; k < strLength; k++)
                        {
                            ushort encoded = (ushort)(rawBytes[k * 2] | (rawBytes[k * 2 + 1] << 8));
                            chars[k] = (char)(~encoded);
                        }
                        decodedValue = new string(chars);
                    }
                    else
                    {
                        decodedValue = string.Empty;
                    }

                    string extraValue = null;
                    if (isStrw)
                    {
                        int extraLength = br.ReadInt32();
                        byte[] extraBytes = br.ReadBytes(extraLength);
                        extraValue = Encoding.ASCII.GetString(extraBytes);
                    }

                    label.Strings.Add(new CsfStringEntry(decodedValue, extraValue));
                }

                doc.Labels.Add(label);
            }

            return doc;
        }

        public static void Save(CsfDocument doc, string filePath)
        {
            // Atomic save strategy: write to a temp file in the same directory,
            // then replace the target. This avoids corruption if the process is
            // interrupted, and works around transient locks from antivirus/indexer
            // on the original file.
            string dir = Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(dir)) dir = ".";
            string tempPath = Path.Combine(dir, Path.GetRandomFileName() + ".csf.tmp");

            try
            {
                // Write the complete document to a temp file first.
                using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
                using (var bw = new BinaryWriter(fs, Encoding.ASCII))
                {
                    Write(doc, bw);
                }

                // Replace the original with the temp file. Retry on transient locks.
                const int MaxRetries = 5;
                const int RetryDelayMs = 200;
                for (int attempt = 0; ; attempt++)
                {
                    try
                    {
                        if (File.Exists(filePath))
                        {
                            var attr = File.GetAttributes(filePath);
                            if ((attr & FileAttributes.ReadOnly) != 0)
                            {
                                File.SetAttributes(filePath, attr & ~FileAttributes.ReadOnly);
                            }
                        }

                        // File.Copy with overwrite handles read-shared locks better
                        // than FileMode.Create on the target path directly.
                        File.Copy(tempPath, filePath, overwrite: true);
                        break;
                    }
                    catch (IOException) when (attempt < MaxRetries)
                    {
                        System.Threading.Thread.Sleep(RetryDelayMs);
                    }
                    catch (UnauthorizedAccessException) when (attempt < MaxRetries)
                    {
                        // Transient lock from antivirus / indexer / read-only attribute — retry after stripping read-only.
                        try
                        {
                            if (File.Exists(filePath))
                            {
                                var attr = File.GetAttributes(filePath);
                                if ((attr & FileAttributes.ReadOnly) != 0)
                                {
                                    File.SetAttributes(filePath, attr & ~FileAttributes.ReadOnly);
                                }
                            }
                        }
                        catch { }
                        System.Threading.Thread.Sleep(RetryDelayMs);
                    }
                }
            }
            finally
            {
                // Clean up the temp file regardless of outcome.
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            }
        }

        public static void Write(CsfDocument doc, BinaryWriter bw)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));

            bw.Write(HEADER_MAGIC);
            bw.Write(doc.Version);
            bw.Write(doc.Labels.Count);
            bw.Write(doc.TotalStringCount);
            bw.Write(0); // Reserved
            bw.Write((int)doc.Language);

            foreach (var label in doc.Labels)
            {
                bw.Write(LABEL_MAGIC);
                bw.Write(label.Strings.Count);

                byte[] nameBytes = Encoding.ASCII.GetBytes(label.Name ?? string.Empty);
                bw.Write(nameBytes.Length);
                bw.Write(nameBytes);

                foreach (var s in label.Strings)
                {
                    bool hasExtra = s.HasExtra;
                    bw.Write(hasExtra ? STRW_MAGIC : STRING_MAGIC);

                    string val = s.Value ?? string.Empty;
                    int len = val.Length;
                    bw.Write(len);
                    if (len > 0)
                    {
                        byte[] buffer = new byte[len * 2];
                        for (int i = 0; i < len; i++)
                        {
                            ushort encoded = (ushort)(~val[i]);
                            buffer[i * 2] = (byte)(encoded & 0xFF);
                            buffer[i * 2 + 1] = (byte)((encoded >> 8) & 0xFF);
                        }
                        bw.Write(buffer);
                    }

                    if (hasExtra)
                    {
                        byte[] extraBytes = Encoding.ASCII.GetBytes(s.ExtraValue ?? string.Empty);
                        bw.Write(extraBytes.Length);
                        bw.Write(extraBytes);
                    }
                }
            }
        }
    }
}
