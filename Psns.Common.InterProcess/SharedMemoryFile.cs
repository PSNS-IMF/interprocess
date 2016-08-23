using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Psns.Common.InterProcess
{
    /// <summary>
    /// Useful for interprocess communication (IPC); backed by a non-persisted MemoryMappedFile
    /// </summary>
    public class SharedMemoryFile : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        struct FileHeader
        {
            public long FileSize;
        }

        static long _headerSize = Marshal.SizeOf(typeof(FileHeader));

        readonly MemoryMappedFile _file;
        readonly string _name;
        
        static Lst<string> _namesOpen = List<string>();

        /// <summary>
        /// The size of the file in bytes
        /// </summary>
        public readonly long Size;

        SharedMemoryFile(string name, Some<MemoryMappedFile> file)
        {
            _name = name;
            _file = file;

            using(var view = _file.CreateViewAccessor(0, _headerSize))
            {
                FileHeader header;
                view.Read(0, out header);

                Size = header.FileSize;
            }
        }

        /// <summary>
        /// Open an existing file
        /// </summary>
        /// <param name="name">The name of the existing file</param>
        /// <returns>The existing file</returns>
        /// <exception cref="System.InvalidOperationException">If file doesn't exist</exception>
        public static SharedMemoryFile Open(Some<string> name)
        {
            if(!_namesOpen.Exists(n => n == name)) 
                throw new InvalidOperationException(string.Format("SharedMemoryFile {0} does not exist", name));

            return new SharedMemoryFile(name, MemoryMappedFile.OpenExisting(name));
        }

        /// <summary>
        /// Creates new or Opens existing file
        /// </summary>
        /// <param name="name">The name of the file</param>
        /// <param name="size">Memory in bytes to be allocated to the new file. Irrelevant for existing files.</param>
        /// <returns></returns>
        public static SharedMemoryFile CreateOrOpen(Some<string> name, long size)
        {
            if(_namesOpen.Exists(n => n == name))
                return Open(name);

            var file = MemoryMappedFile.CreateNew(
                name, 
                _headerSize + size, 
                MemoryMappedFileAccess.ReadWrite, 
                MemoryMappedFileOptions.DelayAllocatePages, 
                null, 
                HandleInheritability.None
            );

            using(var view = file.CreateViewAccessor(0, _headerSize))
            {
                var header = new FileHeader { FileSize = size };
                view.Write(0, ref header);
            }

            _namesOpen = _namesOpen.Add(name);

            var buffer = new SharedMemoryFile(name, file);
            return buffer;
        }

        /// <summary>
        /// Write data to MemoryMappedFile
        /// </summary>
        /// <param name="data"></param>
        public void Write(byte[] data)
        {
            using(var view = _file.CreateViewAccessor(_headerSize, data.Length))
            {
                view.WriteArray(0, data, 0, data.Length);
            }
        }

        //TODO write with offset

        /// <summary>
        /// Read entire contents of MemoryMappedFile
        /// </summary>
        /// <param name="buffer">The buffer needs to be the correct size in order to store all of the data</param>
        public void Read(byte[] buffer)
        {
            Read(0, buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Read a sequence of bytes from the MemoryMappedFile
        /// </summary>
        /// <param name="position">The position in the file at which to begin reading</param>
        /// <param name="buffer">The array in which to store the bytes read from the file</param>
        /// <param name="offset">The index in the buffer in which to place the first byte read</param>
        /// <param name="count">The number of bytes to read from file</param>
        public void Read(long position, byte[] buffer, int offset, int count)
        {
            using(var view = _file.CreateViewAccessor(_headerSize + position, count))
            {
                view.ReadArray(0, buffer, offset, count);
            }
        }

        #region IDisposable Support

        bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if(!disposedValue)
            {
                if(disposing)
                {
                    _file.Dispose();
                    _namesOpen = _namesOpen.Remove(_name);
                }

                disposedValue = true;
            }
        }

        /// <summary>
        /// Disposes MemoryMappedFile and frees name to be re-used
        /// </summary>
        public void Dispose() => Dispose(true);

        #endregion
    }
}