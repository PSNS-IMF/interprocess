using System;
using System.IO.MemoryMappedFiles;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Psns.Common.InterProcess
{
    public class SharedMemoryFile : IDisposable
    {
        readonly MemoryMappedFile _file;
        readonly string _name;

        static Lst<string> _namesOpen = List<string>();

        SharedMemoryFile(string name, Some<MemoryMappedFile> file)
        {
            _name = name;
            _file = file;
        }

        public static SharedMemoryFile Open(Some<string> name)
        {
            if(!_namesOpen.Exists(n => n == name)) 
                throw new InvalidOperationException(string.Format("SharedMemoryFile {0} does not exist", name));

            return new SharedMemoryFile(name, MemoryMappedFile.OpenExisting(name));
        }

        public static SharedMemoryFile CreateOrOpen(Some<string> name, long size)
        {
            if(_namesOpen.Exists(n => n == name))
                return Open(name);

            var file = MemoryMappedFile.CreateNew(name, size);
            var buffer = new SharedMemoryFile(name, file);

            _namesOpen = _namesOpen.Add(name);

            return buffer;
        }

        public void Write(byte[] data)
        {
            using(var view = _file.CreateViewAccessor(0, data.Length))
            {
                view.WriteArray(0, data, 0, data.Length);
            }
        }

        public void Read(byte[] buffer)
        {
            using(var view = _file.CreateViewAccessor())
            {
                view.ReadArray(0, buffer, 0, buffer.Length);
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

        public void Dispose() => Dispose(true);

        #endregion
    }
}