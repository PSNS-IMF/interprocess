using System;
using System.IO;
using System.Linq;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Psns.Common.InterProcess
{
    public class SharedMemoryStream : Stream
    {
        readonly string _name;
        long _length;
        Option<Lst<byte>> _buffer;
        Option<SharedMemoryFile> _file;

        public static SharedMemoryStream Create(Some<string> name) => new SharedMemoryStream(name, 0L);

        public static SharedMemoryStream Open(Some<string> name, long length) => new SharedMemoryStream(name, length);

        SharedMemoryStream(Some<string> name, long length)
        {
            _name = name;
            _length = length;
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

            if(someBuffer == Lst<byte>.Empty)
            {
                var file = match(_file, f => f, () => SharedMemoryFile.Open(_name));

                var bytes = new byte[_length];
                file.Read(bytes);

                someBuffer = someBuffer.AddRange(bytes);
                _buffer = someBuffer;
                _file = file;
            }

            long takeCount = count;
            var selectionSizeRemaining = _length - Position;

            if(selectionSizeRemaining < count)
                takeCount = count - selectionSizeRemaining;

            var selection = toArray(someBuffer.Skip((int)Position).Take((int)takeCount));
            selection.CopyTo(buffer, offset);

            Position = Position + count;

            return (int)takeCount;
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