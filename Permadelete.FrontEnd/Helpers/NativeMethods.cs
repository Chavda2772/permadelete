using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Permadelete.Helpers
{
    internal static class NativeMethods
    {
        #region Win32 API
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FlushFileBuffers(IntPtr hFile);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetDiskFreeSpace(
            string lpRootPathName,
            out uint lpSectorsPerCluster,
            out uint lpBytesPerSector,
            out uint lpNumberOfFreeClusters,
            out uint lpTotalNumberOfClusters);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr FindFirstStreamW(
            string lpFileName,
            int InfoLevel,
            IntPtr lpFindStreamData,
            uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FindNextStreamW(
            IntPtr hFindStream,
            IntPtr lpFindStreamData);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FindClose(IntPtr hFindStream);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteFileW(string lpFileName);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WIN32_FIND_STREAM_DATA
        {
            public long StreamSize;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 296)]
            public string cStreamName;
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// Retrieves all Alternate Data Streams (ADS) for a specified file, excluding the default ::$DATA stream.
        /// </summary>
        public static List<string> GetAlternateDataStreams(string filePath)
        {
            var streams = new List<string>();
            var findStreamData = new WIN32_FIND_STREAM_DATA();
            int size = Marshal.SizeOf(findStreamData);
            IntPtr pData = Marshal.AllocHGlobal(size);

            try
            {
                IntPtr hFind = FindFirstStreamW(filePath, 0, pData, 0);
                if (hFind != IntPtr.Zero && hFind != new IntPtr(-1))
                {
                    try
                    {
                        do
                        {
                            findStreamData = (WIN32_FIND_STREAM_DATA)Marshal.PtrToStructure(pData, typeof(WIN32_FIND_STREAM_DATA));
                            string streamName = findStreamData.cStreamName;
                            if (!string.IsNullOrEmpty(streamName) &&
                                !streamName.Equals("::$DATA", StringComparison.OrdinalIgnoreCase))
                            {
                                streams.Add(streamName);
                            }
                        } while (FindNextStreamW(hFind, pData));
                    }
                    finally
                    {
                        FindClose(hFind);
                    }
                }
            }
            catch
            {
                // Fallback for non-NTFS or unsupported filesystems
            }
            finally
            {
                Marshal.FreeHGlobal(pData);
            }

            return streams;
        }

        /// <summary>
        /// Gets the cluster size in bytes for the specified path's drive.
        /// Defaults to 4096 bytes if retrieval fails or for network paths.
        /// </summary>
        public static int GetClusterSize(string path)
        {
            try
            {
                string root = Path.GetPathRoot(path);
                if (string.IsNullOrEmpty(root))
                    return 4096;

                if (!root.EndsWith("\\"))
                    root += "\\";

                if (GetDiskFreeSpace(root, out uint sectorsPerCluster, out uint bytesPerSector, out _, out _))
                {
                    uint clusterSize = sectorsPerCluster * bytesPerSector;
                    if (clusterSize >= 512 && clusterSize <= 2 * 1024 * 1024)
                        return (int)clusterSize;
                }
            }
            catch
            {
                // Ignored - fallback to default
            }

            return 4096;
        }
        #endregion
    }
}
