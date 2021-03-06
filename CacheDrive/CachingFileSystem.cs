﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Timers;

using Dokan;

namespace CacheDrive
{
    internal class CachingFileSystem : DokanOperations
    {
        private readonly string basePath;
        private CacheRepositry cache;

        public CachingFileSystem(string basePath)
        {
            this.basePath = basePath;

            this.cache = new CacheRepositry();
        }

        private string Map(string filename)
        {
            if (filename.Equals("\\"))
                return basePath;

            string withEnvironmentVar = ReplaceEnvironmentVar(filename);
            if (!string.IsNullOrEmpty(withEnvironmentVar))
                return withEnvironmentVar;

            return string.Concat(basePath, filename);
        }

        private string ReplaceEnvironmentVar(string filename)
        {
            var environmentVariables = System.Environment.GetEnvironmentVariables();
            foreach (var pathKey in environmentVariables.Keys)
            {
                string pathWithPercent = string.Concat('%', pathKey, '%');
                if (filename.Contains(pathWithPercent))
                {
                    int indexOfVariable = filename.IndexOf(pathWithPercent);
                    string shortened = filename.Substring(indexOfVariable, filename.Length - indexOfVariable);
                    return shortened.Replace(pathWithPercent, (string)environmentVariables[pathKey]);
                }
            }

            return string.Empty;
        }

        public int CreateFile(string filename, System.IO.FileAccess access, FileShare share, FileMode mode, FileOptions options, DokanFileInfo info)
        {
            string f = Map(filename);
            //if(access == FileAccess.ReadWrite)
            //File.Create(filename, 256, options);
            return 0;
        }

        public int OpenDirectory(string filename, DokanFileInfo info)
        {
            return 0;
        }

        public int CreateDirectory(string filename, DokanFileInfo info)
        {
            string f = Map(filename);
            Directory.CreateDirectory(f);
            return 0;
        }

        public int Cleanup(string filename, DokanFileInfo info)
        {
            return 0;
        }

        public int CloseFile(string filename, DokanFileInfo info)
        {
            return 0;
        }

        public int ReadFile(string filename, byte[] buffer, ref uint readBytes, long offset, DokanFileInfo info)
        {
            string f = Map(filename);
            if (!File.Exists(f))
                return -1;

            using (Stream stream = this.cache.GetStream(f))
            {
                stream.Seek(offset, SeekOrigin.Begin);

                int numBytesToRead = (int)buffer.Length;
                int numBytesRead = 0;

                while (numBytesToRead > 0)
                {
                    // Read may return anything from 0 to numBytesToRead.
                    int n = stream.Read(buffer, numBytesRead, numBytesToRead - 1);

                    // Break when the end of the file is reached.
                    if (n == 0)
                        break;

                    numBytesRead += n;
                    numBytesToRead -= n;
                }

                readBytes = Convert.ToUInt32(numBytesRead);
            }
            return 0;
        }

        public int WriteFile(string filename, byte[] buffer, ref uint writtenBytes, long offset, DokanFileInfo info)
        {
            string f = Map(filename);
            if (!File.Exists(f))
                return -1;

            using (Stream stream = File.OpenWrite(f))
            {
                int iOffset = Convert.ToInt32(offset);
                int length = Convert.ToInt32(writtenBytes);
                stream.Write(buffer, iOffset, length);
            }
            return 0;
        }

        public int FlushFileBuffers(string filename, DokanFileInfo info)
        {
            return -1;
        }

        public int GetFileInformation(string filename, FileInformation fileinfo, DokanFileInfo info)
        {
            var f = Map(filename);
            if (File.Exists(f))
            {
                FileInfo fi = new FileInfo(f);
                fileinfo.Length = fi.Length;
                fileinfo.Attributes = fi.Attributes;
                fileinfo.CreationTime = fi.CreationTime;
                fileinfo.FileName = fi.FullName;
                fileinfo.LastAccessTime = fi.LastAccessTime;
                fileinfo.LastWriteTime = fi.LastWriteTime;
                return 0;
            }
            else if(Directory.Exists(f))
            {
                info.IsDirectory = true;

                DirectoryInfo fi = new DirectoryInfo(f);
                fileinfo.Attributes = fi.Attributes;
                fileinfo.CreationTime = fi.CreationTime;
                fileinfo.FileName = fi.FullName;
                fileinfo.LastAccessTime = fi.LastAccessTime;
                fileinfo.LastWriteTime = fi.LastWriteTime;
                return 0; 
            }

            return -1;
        }

        public int FindFiles(string filename, ArrayList files, DokanFileInfo info)
        {
            var f = Map(filename);

            if (!Directory.Exists(f))
                return 0;

            foreach (var dir in Directory.GetDirectories(f))
            {
                DirectoryInfo di = new DirectoryInfo(dir);
                files.Add(new Dokan.FileInformation()
                              {
                                  Attributes = di.Attributes,
                                  CreationTime = di.CreationTime,
                                  FileName = Path.GetFileName(di.FullName), 
                                  LastAccessTime = di.LastAccessTime,
                                  LastWriteTime = di.LastWriteTime,
                                  Length = 0
                              });
            }

            foreach (var file in Directory.GetFiles(f))
            {
                FileInfo fi = new FileInfo(file);

                files.Add(new Dokan.FileInformation()
                {
                    Attributes = fi.Attributes,
                    CreationTime = fi.CreationTime,
                    FileName = Path.GetFileName(fi.FullName),
                    LastAccessTime = fi.LastAccessTime,
                    LastWriteTime = fi.LastWriteTime,
                    Length = fi.Length
                });
            } 
            return 0;
        }

        public int SetFileAttributes(string filename, FileAttributes attr, DokanFileInfo info)
        {
            string f = Map(filename);
            File.SetAttributes(f, attr);
            return 0;
        }

        public int SetFileTime(string filename, DateTime ctime, DateTime atime, DateTime mtime, DokanFileInfo info)
        {
            string f = Map(filename);
            try
            {
                File.SetLastAccessTime(f, atime);
                File.SetCreationTime(f, ctime);
                File.SetLastWriteTime(f, mtime);
                return 0;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        public int DeleteFile(string filename, DokanFileInfo info)
        {
            string f = Map(filename);
            File.Delete(f);
            return 0;
        }

        public int DeleteDirectory(string filename, DokanFileInfo info)
        {
            string f = Map(filename);
            Directory.Delete(f);
            return 0;
        }

        public int MoveFile(string filename, string newname, bool replace, DokanFileInfo info)
        {
            string f = Map(filename);
            newname = Map(newname);
            if (!File.Exists(f))
                return -1;

            File.Move(f, newname);
            return 0;
        }

        public int SetEndOfFile(string filename, long length, DokanFileInfo info)
        {
            return -1;
        }

        public int SetAllocationSize(string filename, long length, DokanFileInfo info)
        {
            return -1;
        }

        public int LockFile(string filename, long offset, long length, DokanFileInfo info)
        {
            return 0;
        }

        public int UnlockFile(string filename, long offset, long length, DokanFileInfo info)
        {
            return 0;
        }

        public int GetDiskFreeSpace(ref ulong freeBytesAvailable, ref ulong totalBytes, ref ulong totalFreeBytes, DokanFileInfo info)
        {
            totalFreeBytes = freeBytesAvailable = ulong.MaxValue / 4;
            totalBytes = totalFreeBytes * 2;

            return 0;
        }

        public int Unmount(DokanFileInfo info)
        {
            return 0;
        }
    }

    internal class CacheRepositry
    {
        Dictionary<string, FileAccess> cacheRepository = new Dictionary<string, FileAccess>();

        public Stream GetStream(string filePath)
        {
            RemoveIfChanged(filePath);

            ThreadSafeAdd(filePath);

            if (this.cacheRepository.ContainsKey(filePath))
                return new MemoryStream(this.cacheRepository[filePath].Data);
            else
                return File.OpenRead(filePath);
        }

        private void RemoveIfChanged(string filePath)
        {
            if (!this.cacheRepository.ContainsKey(filePath))
                return;

            var lastPhysicalWrite = File.GetLastWriteTime(filePath);
            if (this.cacheRepository[filePath].LastWrite < lastPhysicalWrite)
            {
                this.cacheRepository.Remove(filePath);
                Trace.WriteLine(string.Format("Removed because a newer Version exists: {0}", filePath));
            }
        }

        private void ThreadSafeAdd(string filePath)
        {
            if (!this.cacheRepository.ContainsKey(filePath))
            {
                lock (this.cacheRepository)
                {
                    if (!this.cacheRepository.ContainsKey(filePath))
                        this.Add(filePath);
                }  
            }
        }

        private void Add(string filePath)
        {
            if (this.cacheRepository.ContainsKey(filePath))
                return;

            FileInfo fi = new FileInfo(filePath);

            //if(File.Lenght > X)
            byte[] data = File.ReadAllBytes(filePath);
            Trace.WriteLine(String.Format("{0} - Size: {1}", filePath, data.Length));
            this.cacheRepository.Add(filePath, new FileAccess() { Data = data, LastAccess = DateTime.Now, LastWrite = fi.LastWriteTime});
        }
    }

    internal class FileAccess
    {
        public byte[] Data { get; set; }
        public DateTime LastAccess { get; set; }
        public DateTime LastWrite { get; set; }
    }
}