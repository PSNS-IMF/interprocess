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
        #region private

        [StructLayout(LayoutKind.Sequential)]
        struct FileHeader
        {
            public long FileSize;
            public long WritePosition;
        }

        static long _headerSize = Marshal.SizeOf(typeof(FileHeader));

        readonly MemoryMappedFile _file;
        readonly string _name;
        readonly long _writePosition;
        
        static Lst<string> _namesOpen = List<string>();

        SharedMemoryFile(string name, Some<MemoryMappedFile> file)
        {
            _name = name;
            _file = file;

            var header = use(
                _file.CreateViewAccessor(0, _headerSize),
                view =>
                {
                    FileHeader h = new FileHeader();
                    view.Read(0, out h);
                    return h;
                });

            Size = header.FileSize;
            _writePosition = header.WritePosition;
        }

        #endregion

        /// <summary>
        /// The size of the file in bytes
        /// </summary>
        public readonly long Size;

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

            use(
                file.CreateViewAccessor(0, _headerSize),
                view =>
                {
                    var header = new FileHeader { FileSize = size, WritePosition = _headerSize };
                    view.Write(0, ref header);
                    return unit;
                });

            _namesOpen = _namesOpen.Add(name);

            return new SharedMemoryFile(name, file);
        }

        /// <summary>
        /// Write data to MemoryMappedFile
        /// </summary>
        /// <param name="data"></param>
        /// <returns>A new SharedMemoryFile containing the new data</returns>
        public SharedMemoryFile Write(byte[] data)
        {
            return Write(data, 0, data.Length);
        }

        /// <summary>
        /// Writes buffer to MemoryMappedFile at the current write position
        /// </summary>
        /// <param name="buffer">The array containing the bytes to write.</param>
        /// <param name="offset">The index in the buffer from which to begin copying data to the file.</param>
        /// <param name="count">The number of bytes to copy from the buffer.</param>
        /// <returns>A new SharedMemoryFile containing the new data</returns>
        public SharedMemoryFile Write(byte[] buffer, int offset, int count)
        {
            return Write(_writePosition - _headerSize, buffer, offset, count);
        }

        /// <summary>
        /// Writes buffer to MemoryMappedFile
        /// </summary>
        /// <param name="position">The position within the file to begin writing</param>
        /// <param name="buffer">The array containing the bytes to write.</param>
        /// <param name="offset">The index in the buffer from which to begin copying data to the file.</param>
        /// <param name="count">The number of bytes to copy from the buffer.</param>
        /// <returns>A new SharedMemoryFile containing the new data</returns>
        public SharedMemoryFile Write(long position, byte[] buffer, int offset, int count)
        {
            use(
                _file.CreateViewAccessor(_headerSize + position, count),
                view => unit.tee(u => view.WriteArray(0, buffer, offset, count)));

            use(
                _file.CreateViewAccessor(0, _headerSize),
                view => new FileHeader().tee(header =>
                {
                    view.Read(0, out header);
                    header.WritePosition += count;
                    view.Write(0, ref header);
                }));

            var updated = SharedMemoryFile.Open(_name);
            _file.Dispose();

            return updated;
        }

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
            use(
                _file.CreateViewAccessor(_headerSize + position, count),
                view => view.ReadArray(0, buffer, offset, count));
        }

        /// <summary>
        /// Create a bigger SharedMemoryFile containing same data
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public SharedMemoryFile Resize(long size)
        {
            var existingData = new byte[Size];

            use(_file.CreateViewStream(_headerSize, Size),
                view => view.Read(existingData, 0, existingData.Length));

            Dispose();

            return SharedMemoryFile.CreateOrOpen(_name, size).Write(existingData);
        }

        #region IDisposable Support

        bool disposedValue = false;

        /// <summary>
        /// Dispose of MemoryMappedFile
        /// </summary>
        /// <param name="disposing"></param>
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