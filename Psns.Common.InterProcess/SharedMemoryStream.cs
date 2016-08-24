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
        #region private initialization

        readonly string _name;
        long _length;
        Option<Lst<byte>> _buffer;
        Option<SharedMemoryFile> _file;

        SharedMemoryStream(Some<string> name, Option<SharedMemoryFile> file)
        {
            _name = name;
            _buffer = List<byte>();
            _file = file;

            _length = match(_file,
                Some: f => f.Size,
                None: () => 0);
        }

        #endregion

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
        /// <returns></returns>
        public static SharedMemoryStream Open(Some<string> name) => new SharedMemoryStream(name, SharedMemoryFile.Open(name));

        /// <summary>
        /// This stream supports reading.
        /// </summary>
        public override bool CanRead { get; } = true;

        /// <summary>
        /// This stream supports seeking.
        /// </summary>
        public override bool CanSeek { get; } = true;

        /// <summary>
        /// This stream supports writing.
        /// </summary>
        public override bool CanWrite { get; } = true;

        /// <summary>
        /// Stream length in bytes.
        /// </summary>
        public override long Length => _length;

        /// <summary>
        /// Gets or sets the position within the stream.
        /// </summary>
        public override long Position { set; get; } = 0;

        /// <summary>
        /// Clears buffer and writes buffered data to underlying file.
        /// </summary>
        public override void Flush()
        {
            _buffer = _buffer.Match(
                Some: list =>
                {
                    var data = toArray(list);

                    var file = match(
                        _file,
                        Some: f => f.Size < _length ? f.Resize(_length) : f,
                        None: () => SharedMemoryFile.CreateOrOpen(_name, data.Length));

                    _file = file.Write(data, 0, data.Length);
                    Position = 0;

                    return Lst<byte>.Empty;
                },
                None: () => _buffer);
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

            var sizeToReadFunc = match(
                match(_file,
                    Some: f => Right<Lst<byte>, SharedMemoryFile>(f),
                    None: () => Left<Lst<byte>, SharedMemoryFile>(someBuffer)),

                Right: r => Tuple<long, ReadFunc>(r.Size, FileRead),
                Left: l => Tuple<long, ReadFunc>(l.Count, StreamRead));

            return Position >= sizeToReadFunc.Item1
                ? 0
                : fun(() =>
                {
                    var proposedPosition = Position + count;

                    var actualCount = proposedPosition > sizeToReadFunc.Item1
                        ?(int)(sizeToReadFunc.Item1 - Position)
                        : count;

                    sizeToReadFunc.Item2(buffer, offset, actualCount, someBuffer);

                    Position += actualCount;

                    _length = _length < Position
                        ? _length + actualCount
                        : _length;

                    return actualCount;
                })();
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
        /// If current length greater than value, stream is truncated to value; else, stream is expanded to value with undefined data
        /// </summary>
        /// <param name="value">New stream size</param>
        public override void SetLength(long value)
        {
            var bufferList = match(
                _buffer, 
                Some: b => b, 
                None: () => List<byte>());

            bufferList = bufferList.Count > value
                ? List<byte>().AddRange(bufferList.Take((int)value))
                : bufferList.AddRange(new byte[value - bufferList.Count]);

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

        #region private helpers

        delegate void ReadFunc(byte[] buffer, int offset, int count, Some<Lst<byte>> streamBuffer);

        /// <summary>
        /// Read from backing file
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="streamBuffer"></param>
        void FileRead(byte[] buffer, int offset, int count, Some<Lst<byte>> streamBuffer) 
        {
            var file = match(_file, f => f, () => SharedMemoryFile.Open(_name));

            file.Read(Position, buffer, offset, count);
            streamBuffer = streamBuffer.Value.AddRange(buffer.Skip(offset).Take(count));
            _buffer = streamBuffer;
            _file = file;
        }

        /// <summary>
        /// Read from buffer
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="streamBuffer"></param>
        void StreamRead(byte[] buffer, int offset, int count, Some<Lst<byte>> streamBuffer) 
        {
            toArray(streamBuffer.Value.Skip((int)Position).Take(count)).CopyTo(buffer, offset);
        }

        #endregion

        #region IDisposable

        bool disposedValue = false;

        /// <summary>
        /// Dispose SharedMemoryFile
        /// </summary>
        /// <param name="disposing"></param>
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