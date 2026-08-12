# CSF Studio

![License GPLv3](https://img.shields.io/badge/License-GPLv3-blue.svg)
![Framework .NET 4.8](https://img.shields.io/badge/.NET%20Framework-4.8-purple.svg)
![Language C# 7.3](https://img.shields.io/badge/C%23-7.3-green.svg)
![Platform Windows](https://img.shields.io/badge/Platform-Windows-0078D6.svg)

**CSF Studio** is an open-source desktop editor for Command & Conquer `.csf` string-table files (used in *Red Alert 2* and *Yuri's Revenge* modding and localization).

Designed specifically for mod developers, translators, and game localization teams, CSF Studio allows managing multiple language `.csf` files simultaneously within a unified session, featuring side-by-side editors, Drag & Drop file loading, plain text import/export, automated key synchronization, INI/MAP scanning, and integrated machine translation.

---

## 🌟 Key Features

- 📑 **Simultaneous Multi-CSF Sessions**:
  Open and edit multiple CSF files in a single session. Set a main CSF file as reference and compare target files side-by-side using tabbed or split views.

- 📥 **Drag & Drop Integration**:
  Drag `.csf` binary files or `.txt` plain-text string tables directly into the window or onto the desktop icon to open sessions or trigger instant text imports.

- 📄 **Plain Text Import & Export**:
  Export `.csf` string tables to UTF-8 plain text files (or key structure templates without values) and import modified text files back into your session.

- 🔎 **Key & Value RegEx Filtering**:
  Filter grid entries by Key, Value, or both simultaneously using plain text matching or Regular Expressions (RegEx).

- 🤖 **Integrated Machine Translation**:
  Translate single entries or entire batches using customizable providers:
  - **Google Translate (Web Free)**
  - **OpenAI-Compatible AI Models** (DeepSeek, OpenAI, Groq, OpenRouter, local Ollama / LM Studio)

- 🔍 **Automated INI / MAP Key Scanner**:
  Scan game mod `.ini` and `.map` files to detect string keys (`Name=...`, `UIName=...`, `CSF:...`) that are missing or untranslated across your `.csf` string tables.

- ⚖️ **Visual CSF Diff Engine**:
  Compare any two `.csf` files side-by-side with color-coded status badges (`Modified`, `Added`, `Removed`) and one-click value copying between documents.

- 🔤 **ANSI / Codepage to Unicode UTF-16 Conversion**:
  Fix character encoding issues in legacy community translations by re-encoding text from localized ANSI codepages (e.g., CP1251 Cyrillic, CP1250 Central European, CP936 Simplified Chinese) into UTF-16LE.

- 📦 **100% Portable Executable**:
  All application icons and resources are embedded directly into the compiled assembly binary. No external `.ico` or `.png` files required.

---

## ⚠️ Disclaimer & Usage Notice

> [!IMPORTANT]
> **Use at your own risk.** This software is provided **"AS IS"** without warranty of any kind.
> While care has been taken during development, it has not been 100% stress-tested across all community mod setups.
> **Always make backups of your original `.csf` string-table files before editing.**
> Neither the author nor contributors shall be held liable for any data loss, file corruption, or game crashes resulting from the use of this tool.

---

## 💾 Binary Format Support

CSF Studio fully implements the **Westwood Studios 32-bit CSF Binary Standard (v3)**:
- Obfuscated UTF-16LE string payloads (bitwise-NOT bitmask encryption).
- Plain ASCII label headers and `STRW` extra audio string tags.
- Full support for `LanguageNeutral` and custom header language IDs (Ares Yuri's Revenge Expansion feature).

---

## 🚀 Building & Requirements

### Requirements
- **OS**: Windows 10 / 11
- **Runtime**: .NET Framework 4.8
- **IDE / Build Tools**: Visual Studio 2019 / 2022 or MSBuild 17/18

### Building from Source

```cmd
# Clean build with MSBuild
"C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" CSFStudio.sln /t:Rebuild /p:Configuration=Release
```

---

## 📜 License

This project is licensed under the **GNU General Public License v3.0 (GPLv3)** - see the [LICENSE](LICENSE) file for details.

---

## 🙌 Credits & Author

- **Author**: **FS-21** ([https://github.com/FS-21](https://github.com/FS-21))
- **Development**: Created via Live-Coding & AI Pair-Programming with Antigravity.
