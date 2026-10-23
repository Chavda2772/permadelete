# Permadelete

[![Platform](https://img.shields.io/badge/platform-Windows%207%2B-blue.svg)](https://microsoft.com/windows)
[![Framework](https://img.shields.io/badge/.NET%20Framework-4.8-purple.svg)](https://dotnet.microsoft.com/download/dotnet-framework/net48)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Release](https://img.shields.io/github/v/release/DevelopersTree/permadelete?include_prereleases)](https://github.com/DevelopersTree/permadelete/releases)

**Permadelete is a lightweight, secure, and beautiful Windows application designed to shred files and folders permanently, ensuring complete user privacy and zero data recoverability.**

- **[Download the Latest Release](https://github.com/DevelopersTree/permadelete/releases)**
- Read the [Tutorial: 3 Ways to Shred Files](https://github.com/DevelopersTree/permadelete/wiki/How-to-use-Permadelete)
- Learn [How Permadelete Works](https://github.com/DevelopersTree/permadelete/wiki/How-Permadelete-works)

---

![Permadelete Main Window](screenshots/mainwindow.png)

---

## Key Features

- **Permanent, Zero-Recovery Shredding**: Employs multi-pass pseudorandom overwrite patterns and DoD 5220.22-M algorithms to render file and folder contents completely unrecoverable, even by advanced forensic and commercial data recovery software.
- **Windows Explorer Context Menu**: Right-click any file or directory in Windows Explorer and select **"Shred with Permadelete"** for instant deletion.
- **Cluster Slack Space Cleaning**: Overwrites cluster slack space to eradicate lingering remnant data residing in unused trailing sectors of file clusters.
- **Alternate Data Streams (ADS) Stripping**: Automatically detects and purges hidden NTFS alternate data streams and zone identifiers before deleting files.
- **Metadata, Name & Timestamp Obfuscation**: Renames files and directories with randomized strings and randomizes timestamps (Created, Modified, Last Accessed) prior to unlinking, preventing file name or timeline recovery.
- **SSD & Flash Drive Awareness**: Intelligent drive detection with specialized handling tailored for solid-state storage.
- **Modern & Responsive UI**: Clean WPF interface featuring smooth animations, drag-and-drop shredding, theme accent colors (Magenta, Slate, etc.), and cancellation control.

---

## System Requirements

- **Operating System**: Windows 7 SP1, 8, 8.1, 10, or 11 (32-bit and 64-bit).
- **Runtime**: [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48) or later.

---

## Installation

Permadelete offers multiple installation methods:

1. **Windows Installer (`.msi`)**:
   - Built using Visual Studio Setup Projects (`Permadelete.Setup`).
   - Includes options for Desktop and Start Menu shortcuts.
   - Automatically cleans up context menu registry keys upon uninstallation.
2. **Inno Setup Installer (`.exe`)**:
   - Packaged installer generated via `Publish\Installer.iss`.
   - Supports silent installation (`/SILENT` or `/VERYSILENT`) and uninstallation.
3. **Portable**:
   - Download the release archive, extract to any directory, and run `Permadelete.exe`.

---

## Command Line Usage

Permadelete supports command-line arguments for automation and integration:

```cmd
:: Shred specific files or directories directly
Permadelete.exe "C:\Sensitive\Confidential.docx"
Permadelete.exe "D:\PrivateFolder"

:: Unregister Windows Explorer context menu entries cleanly (used during uninstall)
Permadelete.exe --uninstall
```

---

## How to Build from Source

### Prerequisites
- [Visual Studio 2019 / 2022](https://visualstudio.microsoft.com/) (Community, Professional, or Enterprise).
- Workload: **.NET desktop development** (.NET Framework 4.8 targeting pack).

### Steps
1. Clone the repository:
   ```cmd
   git clone https://github.com/DevelopersTree/permadelete.git
   cd permadelete
   ```
2. Open [`Permadelete.sln`](file:///e:/Development/Win_Apps/permadelete/Permadelete.sln) in Visual Studio.
3. If not already present, ensure [`Keys.cs`](file:///e:/Development/Win_Apps/permadelete/Permadelete.FrontEnd/Helpers/Keys.cs) is present in `Permadelete.FrontEnd\Helpers` or `Publish`:
   ```csharp
   namespace Permadelete
   {
       public class Keys
       {
           public const string SENTRY_API_DSN = "";
           public const string DROPBOX_API_KEY = "";
       }
   }
   ```
4. Select the **Release** or **Debug** configuration.
5. Press **Ctrl + Shift + B** to build the solution.
6. The compiled binary will be in `Permadelete.FrontEnd\bin\Release\Permadelete.exe`.

---

## Resources & References

- [Secure Delete (.NET)](https://github.com/bitbeans/securedelete-net) by @bitbeans
- [Data Evaporation on SSDs](https://www.youtube.com/watch?v=zG0orMGf_Go) by Sam Bowne
- [The Urban Legend of Multipass Hard Disk Overwrite and DoD 5220-22-M](http://web.archive.org/web/20121110053501/http://grot.com/wordpress/?p=154) by Brian Smithson

---

## Special Thanks

- [Icons8](https://icons8.com) for icons.
- [Sentry.io](https://sentry.io) for crash analytics support.
- All contributors and testers who help keep Permadelete reliable and secure.

---

## License

This project is licensed under the [MIT License](LICENSE.md).

---

## Contact

- Website: [DevelopersTree.com](https://developerstree.com/)
- Email: [encryptor@outlook.com](mailto:encryptor@outlook.com)
