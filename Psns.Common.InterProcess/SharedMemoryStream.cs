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
        public static SharedMemoryStream Create(Some<string> name) => new SharedMemoryStream(name, None);

        /// <summary>
        /// Open an existing stream
        /// </summary>
        /// <param name="name">Name of the existing stream</param>
        /// <param name="length">The size of the existing stream</param>
        /// <returns></returns>
        public static SharedMemoryStream Open(Some<string> name) => new SharedMemoryStream(name, SharedMemoryFile.Open(name));

        SharedMemoryStream(Some<string> name, Option<SharedMemoryFile> file)
        {
            _name = name;
            _buffer = List<byte>();
            _file = file;
            
            _length = match(_file,
                Some: f => f.Size,
                None: () => 0);
        }

        public override bool CanRead { get; } = true;

        public override bool CanSeek { get; } = true;

        public override bool CanWrite { get; } = true;

        public override long Length => _length;

        public override long Position { set; get; } = 0;

        /// <summary>
        /// Clears buffer and writes buffered data to underlying file.
        /// </summary>
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
                            Some: f => 
                                {
                                    if(f.Size < _length)
                                        f.Dispose();

                                    return SharedMemoryFile.CreateOrOpen(_name, _length);
                                }, 
                            None: () => SharedMemoryFile.CreateOrOpen(_name, data.Length));

                        file.Write(data);

                        _file = file;

                        _buffer = empty<byte>();
                    }

                    Position = 0;
                });
        }

        /// <summary>
        /// Read data from stream
        /// </summary>
        /// <param name="buffer">An array of bytes. When this method returns, 
        ///     the buffer contains the specified byte array with the values between offset 
        ///     and (offset + count - 1) replaced by the bytes read from the current source.
        /// </param>
        /// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the current stream.</param>
        /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
        /// <returns>The total number of bytes read into the buffer. This can be less than 
        ///     the number of bytes requested if that many bytes are not currently available, 
        ///     or zero (0) if the end of the stream has been reached.
        /// </returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            var someBuffer = match(_buffer,
                Some: b => b,
                None: () => List<byte>());

            var someFile = match(_file, f => f, () => SharedMemoryFile.Open(_name));

            if(Position >= someFile.Size) return 0;

            var proposedPosition = Position + count;
            var actualCount = count;

            if(proposedPosition > someFile.Size)
                actualCount = (int)(someFile.Size - Position);

            someFile.Read(Position, buffer, offset, actualCount);
            someBuffer = someBuffer.AddRange(buffer.Skip(offset).Take(actualCount));
            _buffer = someBuffer;
            _file = someFile;

            Position += actualCount;

            if(_length < Position) _length = _length + actualCount;

            return actualCount;
        }

        /// <summary>
        /// Sets the position of the stream
        /// </summary>
        /// <param name="offset">A byte offset relative to the origin parameter.</param>
        /// <param name="origin">A value of type SeekOrigin indicating the reference point used to obtain the new position.</param>
        /// <returns></returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            var proposedPosition = Position;

            switch(origin)
            {
                case SeekOrigin.Begin:
                    proposedPosition = offset;
                    break;
                
                case SeekOrigin.Current:
                    proposedPosition += offset;
                    break;

                case SeekOrigin.End:
                    proposedPosition = Length + offset;
                    break;
            }

            if(proposedPosition < 0) throw new ArgumentException("Can't seek before beginning of stream");
            if(proposedPosition > _length) throw new ArgumentException("Can't seek beyond end of stream");

            Position = proposedPosition;
            return Position;
        }

        /// <summary>
        /// If current length < value, stream is truncated to value; else, stream is expanded to value
        /// </summary>
        /// <param name="value">New stream size</param>
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

        /// <summary>
        /// Writes to stream
        /// </summary>
        /// <param name="buffer">Buffer of data to copy to stream</param>
        /// <param name="offset">The position in buffer at which begin copying data to stream</param>
        /// <param name="count">The number of bytes to be written to the stream</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            var someBuffer = match(_buffer,
                Some: b => b,
                None: () => List<byte>());

            someBuffer = someBuffer.AddRange(buffer.Skip(offset).Take(count));
            Position = Position + count;
            _length = _length + count;
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