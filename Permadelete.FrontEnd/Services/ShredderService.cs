using Permadelete.Helpers;
using Permadelete.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DiskDetector;

namespace Permadelete.Services
{
    public sealed class ShredderService
    {
        #region Constructor
        private ShredderService()
        {

        }
        #endregion

        #region Fields
        private const int MAX_BUFFER_SIZE = Constants.MEGABYTE;
        private static readonly RNGCryptoServiceProvider _rng = new RNGCryptoServiceProvider();
        #endregion

        #region Properties
        // using Lazy<T> here makes the field both lazily loaded and thread safe
        private static Lazy<ShredderService> _instance = new Lazy<ShredderService>(() => new ShredderService());
        public static ShredderService Instance
        {
            get { return _instance.Value; }
        }
        #endregion

        #region Public Methods
        public async Task<bool> ShredItemAsync(string path, int passes, CancellationToken cancellationToken, IProgress<long> progress)
        {
            FileInfo file = null;
            DirectoryInfo folder = null;

            if (File.Exists(path))
                file = new FileInfo(path);
            else if (Directory.Exists(path))
                folder = new DirectoryInfo(path);
            else
                throw new ArgumentException($"This path does not exist: {path}");

            if (file != null)
                return await ShredFileAsync(file, passes, cancellationToken, progress).ConfigureAwait(false);

            if (cancellationToken != null)
                cancellationToken.ThrowIfCancellationRequested();

            if (folder != null)
                return await ShredFolderAsync(folder, passes, cancellationToken, progress).ConfigureAwait(false);

            return false;
        }

        public async Task<long> GetFolderSize(DirectoryInfo folder)
        {
            long length = 0;
            await Task.Run(async () =>
            {
                try
                {
                    foreach (var item in folder.EnumerateFiles())
                    {
                        length += item.Length;
                    }

                    foreach (var item in folder.EnumerateDirectories())
                    {
                        length += await GetFolderSize(item).ConfigureAwait(false);
                    }
                }
                catch
                {
                    // Continue gracefully if access denied
                }
            });

            return length;
        }

        public async Task<bool> ShredFileAsync(FileInfo file, int passes, CancellationToken cancellationToken, IProgress<long> progress)
        {
            if (file == null)
                return false;

            file.Refresh();
            if (!file.Exists)
                return true;

            // If the file is a symbolic link or junction, only delete the link, not the target content
            if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                file.Delete();
                return true;
            }

            var settings = SettingsHelper.GetSettings();
            var driveName = Path.GetPathRoot(file.FullName);
            bool isSsd = DiskIsSSD(driveName);
            bool shouldOverwrite = !isSsd || settings.OverwriteSolidStateDrives;

            // Strip read-only, hidden, and system attributes so file can be written and deleted
            StripAttributes(file.FullName);

            // Wipe Alternate Data Streams (ADS) such as Zone.Identifier and metadata before data overwrite
            if (settings.WipeAlternateDataStreams)
            {
                WipeAlternateDataStreams(file.FullName);
            }

            if (shouldOverwrite)
            {
                var writeProgress = new Progress<long>();
                writeProgress.ProgressChanged += (sender, newBytes) =>
                {
                    progress?.Report(newBytes);
                };

                for (int i = 0; i < passes; i++)
                {
                    if (cancellationToken != null)
                        cancellationToken.ThrowIfCancellationRequested();

                    var result = await OverwriteFileAsync(file, i, passes, cancellationToken, writeProgress).ConfigureAwait(false);
                    if (!result) return false;
                }
            }
            else
            {
                progress?.Report(file.Length * passes);
            }

            // Scramble file name and reset timestamps to eliminate MFT / directory records
            if (settings.ObfuscateNamesAndTimestamps)
            {
                DestroyFileMetadata(file);
            }
            else
            {
                file.Delete();
            }

            return true;
        }

        public async Task<bool> ShredFolderAsync(DirectoryInfo folder, int passes, CancellationToken cancellationToken, IProgress<long> progress)
        {
            if (folder == null)
                return false;

            folder.Refresh();
            if (!folder.Exists)
                return true;

            var everythingWasShredded = true;

            // If the folder is a reparse point (symlink/junction), only remove the link itself
            if ((folder.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                folder.Delete();
                return true;
            }

            StripAttributes(folder.FullName);

            try
            {
                foreach (var info in folder.EnumerateFileSystemInfos())
                {
                    if (cancellationToken != null)
                        cancellationToken.ThrowIfCancellationRequested();

                    var file = info as FileInfo;
                    var dir = info as DirectoryInfo;
                    long length = 0;

                    try
                    {
                        if (file != null)
                        {
                            length = file.Length;
                            var itemProgress = new Progress<long>();
                            itemProgress.ProgressChanged += (sender, newBytes) => progress?.Report(newBytes);

                            var result = await ShredFileAsync(file, passes, cancellationToken, itemProgress).ConfigureAwait(false);
                            if (!result)
                            {
                                everythingWasShredded = false;
                            }
                        }
                        else if (dir != null)
                        {
                            length = await GetFolderSize(dir).ConfigureAwait(false);
                            var itemProgress = new Progress<long>();
                            itemProgress.ProgressChanged += (sender, newBytes) => progress?.Report(newBytes);

                            var result = await ShredFolderAsync(dir, passes, cancellationToken, itemProgress).ConfigureAwait(false);
                            if (!result)
                            {
                                everythingWasShredded = false;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        everythingWasShredded = false;
                        progress?.Report(length * passes);
                        continue;
                    }
                }
            }
            catch (Exception)
            {
                // Directory enumeration failed (e.g., access denied on subfolder)
                everythingWasShredded = false;
            }

            // If folder is a root logical drive (e.g. D:\), don't attempt to delete the drive itself!
            if (everythingWasShredded && folder.Parent != null)
            {
                var settings = SettingsHelper.GetSettings();
                if (settings.ObfuscateNamesAndTimestamps)
                {
                    DestroyDirectoryMetadata(folder);
                }
                else
                {
                    folder.Delete(false);
                }
            }

            return everythingWasShredded;
        }

        public bool IsFileLocked(FileInfo file)
        {
            FileStream stream = null;

            try
            {
                stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (IOException)
            {
                return true;
            }
            finally
            {
                if (stream != null) stream.Close();
            }

            return false;
        }
        #endregion

        #region Private Methods
        private async Task<bool> OverwriteFileAsync(FileInfo file, int passIndex, int totalPasses, CancellationToken cancellationToken, IProgress<long> progress)
        {
            StripAttributes(file.FullName);

            var settings = SettingsHelper.GetSettings();
            long originalLength = file.Length;

            int clusterSize = NativeMethods.GetClusterSize(file.FullName);
            long clusterSlack = 0;
            if (settings.WipeClusterSlack && originalLength > 0 && clusterSize > 0)
            {
                clusterSlack = (clusterSize - (originalLength % clusterSize)) % clusterSize;
            }

            long totalWipeLength = originalLength + clusterSlack;
            byte[] buffer = new byte[MAX_BUFFER_SIZE];

            using (var stream = new FileStream(
                file.FullName,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize: 64 * 1024,
                options: FileOptions.WriteThrough | FileOptions.Asynchronous))
            {
                // Temporarily expand length to include cluster slack space so residual slack is wiped
                if (totalWipeLength > originalLength)
                {
                    try
                    {
                        stream.SetLength(totalWipeLength);
                    }
                    catch
                    {
                        totalWipeLength = originalLength;
                        clusterSlack = 0;
                    }
                }

                stream.Seek(0, SeekOrigin.Begin);

                long remaining = totalWipeLength;
                while (remaining > 0)
                {
                    if (cancellationToken != null)
                        cancellationToken.ThrowIfCancellationRequested();

                    int bytesToWrite = (remaining > MAX_BUFFER_SIZE) ? MAX_BUFFER_SIZE : (int)remaining;

                    FillBufferForPass(buffer, bytesToWrite, passIndex, totalPasses);

                    await stream.WriteAsync(buffer, 0, bytesToWrite, cancellationToken).ConfigureAwait(false);

                    if (progress != null)
                    {
                        long reportBytes = (remaining <= clusterSlack) ? 0 : Math.Min((long)bytesToWrite, remaining - clusterSlack);
                        if (reportBytes > 0)
                            progress.Report(reportBytes);
                    }

                    remaining -= bytesToWrite;
                }

                // Flush intermediate buffers and commit directly to physical media
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(true);
                NativeMethods.FlushFileBuffers(stream.SafeFileHandle.DangerousGetHandle());

                // On the final pass: zero out and truncate file length to 0 to clear MFT cluster run list
                if (passIndex == totalPasses - 1)
                {
                    stream.SetLength(0);
                    await stream.FlushAsync().ConfigureAwait(false);
                    stream.Flush(true);
                    NativeMethods.FlushFileBuffers(stream.SafeFileHandle.DangerousGetHandle());
                }
            }

            return true;
        }

        private void WipeAlternateDataStreams(string filePath)
        {
            try
            {
                var streams = NativeMethods.GetAlternateDataStreams(filePath);
                foreach (var streamName in streams)
                {
                    try
                    {
                        string streamPath = filePath + streamName;
                        using (var stream = new FileStream(
                            streamPath,
                            FileMode.Open,
                            FileAccess.Write,
                            FileShare.None,
                            bufferSize: 4096,
                            options: FileOptions.WriteThrough))
                        {
                            long len = stream.Length;
                            if (len > 0)
                            {
                                var buf = new byte[Math.Min(len, 64 * 1024)];
                                _rng.GetBytes(buf);
                                long rem = len;
                                while (rem > 0)
                                {
                                    int toWrite = (int)Math.Min(rem, buf.Length);
                                    stream.Write(buf, 0, toWrite);
                                    rem -= toWrite;
                                }
                                stream.Flush(true);
                            }
                            stream.SetLength(0);
                            stream.Flush(true);
                        }

                        NativeMethods.DeleteFileW(streamPath);
                    }
                    catch
                    {
                        try
                        {
                            NativeMethods.DeleteFileW(filePath + streamName);
                        }
                        catch { }
                    }
                }

                // Explicitly check and delete Zone.Identifier stream (source download tracking)
                try
                {
                    NativeMethods.DeleteFileW(filePath + ":Zone.Identifier");
                }
                catch { }
            }
            catch
            {
            }
        }

        private bool DestroyFileMetadata(FileInfo file)
        {
            try
            {
                file.Refresh();
                if (!file.Exists)
                    return false;

                StripAttributes(file.FullName);

                var directoryName = Path.GetDirectoryName(file.FullName);
                if (string.IsNullOrEmpty(directoryName))
                {
                    file.Delete();
                    return true;
                }

                string currentPath = file.FullName;
                int originalLength = Math.Max(file.Name.Length, 1);

                // Multi-stage rename with descending lengths to overwrite MFT $FILE_NAME directory entries
                int[] renameLengths = new int[]
                {
                    originalLength,
                    Math.Max(originalLength + 8, 32),
                    16,
                    8,
                    1
                };

                var epochTime = new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc);

                for (int i = 0; i < renameLengths.Length; i++)
                {
                    try
                    {
                        string randomName = GenerateRandomString(renameLengths[i]);
                        string newPath = Path.Combine(directoryName, randomName);

                        int attempts = 0;
                        while ((File.Exists(newPath) || Directory.Exists(newPath)) && attempts < 10)
                        {
                            newPath = Path.Combine(directoryName, GenerateRandomString(renameLengths[i] + attempts));
                            attempts++;
                        }

                        File.Move(currentPath, newPath);
                        currentPath = newPath;

                        File.SetCreationTimeUtc(currentPath, epochTime);
                        File.SetLastWriteTimeUtc(currentPath, epochTime);
                        File.SetLastAccessTimeUtc(currentPath, epochTime);
                    }
                    catch
                    {
                        // Continue if an individual rename fails
                    }
                }

                File.Delete(currentPath);
                return true;
            }
            catch
            {
                try
                {
                    if (file.Exists)
                        file.Delete();
                }
                catch { }
                return false;
            }
        }

        private bool DestroyDirectoryMetadata(DirectoryInfo folder)
        {
            try
            {
                folder.Refresh();
                if (!folder.Exists)
                    return false;

                StripAttributes(folder.FullName);

                var parent = folder.Parent?.FullName;
                if (string.IsNullOrEmpty(parent))
                {
                    folder.Delete(false);
                    return true;
                }

                string currentPath = folder.FullName;
                int originalLength = Math.Max(folder.Name.Length, 1);

                int[] renameLengths = new int[]
                {
                    originalLength,
                    Math.Max(originalLength + 8, 32),
                    16,
                    8,
                    1
                };

                var epochTime = new DateTime(1980, 1, 1, 0, 0, 0, DateTimeKind.Utc);

                for (int i = 0; i < renameLengths.Length; i++)
                {
                    try
                    {
                        string randomName = GenerateRandomString(renameLengths[i]);
                        string newPath = Path.Combine(parent, randomName);

                        int attempts = 0;
                        while ((Directory.Exists(newPath) || File.Exists(newPath)) && attempts < 10)
                        {
                            newPath = Path.Combine(parent, GenerateRandomString(renameLengths[i] + attempts));
                            attempts++;
                        }

                        Directory.Move(currentPath, newPath);
                        currentPath = newPath;

                        Directory.SetCreationTimeUtc(currentPath, epochTime);
                        Directory.SetLastWriteTimeUtc(currentPath, epochTime);
                        Directory.SetLastAccessTimeUtc(currentPath, epochTime);
                    }
                    catch
                    {
                    }
                }

                Directory.Delete(currentPath, false);
                return true;
            }
            catch
            {
                try
                {
                    if (folder.Exists)
                        folder.Delete(false);
                }
                catch { }
                return false;
            }
        }

        private static void FillBufferForPass(byte[] buffer, int count, int passIndex, int totalPasses)
        {
            if (totalPasses == 1)
            {
                _rng.GetBytes(buffer);
                return;
            }

            if (totalPasses == 3)
            {
                switch (passIndex)
                {
                    case 0:
                        Array.Clear(buffer, 0, count); // 0x00
                        break;
                    case 1:
                        for (int i = 0; i < count; i++) buffer[i] = 0xFF; // 0xFF
                        break;
                    default:
                        _rng.GetBytes(buffer); // Cryptographic random
                        break;
                }
                return;
            }

            if (totalPasses >= 5)
            {
                switch (passIndex)
                {
                    case 0:
                        for (int i = 0; i < count; i++) buffer[i] = 0x55; // 01010101
                        break;
                    case 1:
                        for (int i = 0; i < count; i++) buffer[i] = 0xAA; // 10101010
                        break;
                    case 2:
                        Array.Clear(buffer, 0, count); // 0x00
                        break;
                    case 3:
                        for (int i = 0; i < count; i++) buffer[i] = 0xFF; // 0xFF
                        break;
                    default:
                        _rng.GetBytes(buffer); // Cryptographic random
                        break;
                }
                return;
            }

            // Fallback for custom pass counts
            if (passIndex == totalPasses - 1)
            {
                Array.Clear(buffer, 0, count);
            }
            else if (passIndex % 2 == 0)
            {
                _rng.GetBytes(buffer);
            }
            else
            {
                for (int i = 0; i < count; i++) buffer[i] = 0xFF;
            }
        }

        private static string GenerateRandomString(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            var data = new byte[length];
            _rng.GetBytes(data);
            var result = new char[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = chars[data[i] % chars.Length];
            }
            return new string(result);
        }

        private static void StripAttributes(string path)
        {
            try
            {
                if (File.Exists(path) || Directory.Exists(path))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                }
            }
            catch
            {
            }
        }

        private bool DiskIsSSD(string driveName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(driveName) || driveName.StartsWith(@"\\"))
                    return false;

                var result = Detector.DetectDrive(driveName);
                return result.HardwareType == DiskDetector.Models.HardwareType.Ssd;
            }
            catch
            {
                return false;
            }
        }
        #endregion
    }
}
