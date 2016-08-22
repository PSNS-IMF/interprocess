using System;
using System.IO.MemoryMappedFiles;
using LanguageExt;

namespace Psns.Common.InterProcess
{
    public class SharedMemoryFile : IDisposable
    {
        readonly long _size;
        readonly MemoryMappedFile _file;

        SharedMemoryFile(long size, Some<MemoryMappedFile> file)
        {
            _size = size;
            _file = file;
        }

        public static SharedMemoryFile Open(Some<string> name, long size)
        {
            return new SharedMemoryFile(size, MemoryMappedFile.OpenExisting(name));
        }

        public static SharedMemoryFile Create(Some<string> name, long size)
        {
            var file = MemoryMappedFile.CreateNew(name, size);
            var buffer = new SharedMemoryFile(size, file);

            return buffer;
        }

        public void Write(byte[] data)
        {
            using(var view = _file.CreateViewAccessor(0, data.Length))
            {
                view.WriteArray(0, data, 0, data.Length);
            }
        }

        public byte[] Read()
        {
            var buffer = new byte[_size];

            using(var view = _file.CreateViewAccessor())
            {
                view.ReadArray(0, buffer, 0, buffer.Length);
            }

            return buffer;
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
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
}