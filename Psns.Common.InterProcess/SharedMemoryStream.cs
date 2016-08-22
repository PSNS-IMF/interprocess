using System;
using System.IO;
using System.Linq;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Psns.Common.InterProcess
{
    /// <summary>
    /// A stream backed by a ShareMemoryFile useful for object serialization
    /// </summary>
    public class SharedMemoryStream : Stream
    {
        readonly string _name;
        long _length;
        Option<Lst<byte>> _buffer;
        Option<SharedMemoryFile> _file;

        /// <summary>
        /// Create a new stream
        /// </summary>
        /// <param name="name">A unique name not used by another stream or SharedMemoryFile</param>
        /// <returns>A new stream</returns>
        public static SharedMemoryStream Create(Some<string> name) => new SharedMemoryStream(name);

        /// <summary>
        /// Open an existing stream
        /// </summary>
        /// <param name="name">Name of the existing stream</param>
        /// <param name="length">The size of the existing stream</param>
        /// <returns></returns>
        public static SharedMemoryStream Open(Some<string> name) => new SharedMemoryStream(name);

        SharedMemoryStream(Some<string> name)
        {
            _name = name;
            _buffer = List<byte>();
        }

        public override bool CanRead { get; } = true;

        public override bool CanSeek { get; } = false;

        public override bool CanWrite { get; } = true;

        public override long Length => _length;

        public override long Position { set; get; } = 0;

        public override void Flush()
        {
            ifSome(_buffer,
                b =>
                {
                    if(b != Lst<byte>.Empty)
                    {
                        var data = toArray(b);

                        var file = match(
                            _file, 
                            f => f, 
                            () => SharedMemoryFile.CreateOrOpen(_name, data.Length));

                        file.Write(data);

                        _file = file;
                    }

                    Position = 0;
                });
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var someBuffer = match(_buffer,
                Some: b => b,
                None: () => List<byte>());

            var someFile = match(_file, f => f, () => SharedMemoryFile.Open(_name));

            someFile.Read(Position, buffer, offset, count);
            someBuffer = someBuffer.AddRange(buffer.Skip(offset).Take(count));
            _buffer = someBuffer;
            _file = someFile;

            Position = Position + count;
            if(_length < Position) _length = _length + count;

            return count;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new InvalidOperationException("Seek is not supported");
        }

        public override void SetLength(long value)
        {
            var bufferList = match(_buffer, b => b, () => List<byte>());
            var currentBuffer = toArray(bufferList);
            var newBuffer = new byte[value];

            if(currentBuffer.Length > value)
                toArray(currentBuffer.Take((int)value)).CopyTo(newBuffer, 0);
            else
                currentBuffer.CopyTo(newBuffer, 0);

            bufferList = List<byte>().AddRange(newBuffer);
            _buffer = bufferList;
            _length = bufferList.Count;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var someBuffer = match(_buffer,
                Some: b => b,
                None: () => List<byte>());

            someBuffer = someBuffer.AddRange(buffer.Skip(offset).Take(count));
            Position = Position + count;
            _length = someBuffer.Count;
            _buffer = someBuffer;
        }

        #region IDisposable

        bool disposedValue = false;

        protected override void Dispose(bool disposing)
        {
            if(!disposedValue)
            {
                if(disposing)
                    ifSome(_file, f => f.Dispose());

                disposedValue = true;
            }

            base.Dispose(disposing);
        }

        #endregion
    }
}