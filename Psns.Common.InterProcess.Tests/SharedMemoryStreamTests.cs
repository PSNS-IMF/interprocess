using NUnit.Framework;
using System;
using System.IO;
using System.Threading.Tasks;

using LanguageExt;
using static LanguageExt.List;
using static LanguageExt.Prelude;

namespace Psns.Common.InterProcess.Tests
{
    [TestFixture]
    public class SharedMemorySerializationTests
    {
        byte[] _toSend, _received;
        Lst<SharedMemoryStream> _streams;

        [SetUp]
        public void Setup()
        {
            _streams = List<SharedMemoryStream>();
            _toSend = new byte[] { 1, 2, 3, 4, 5 };
            _received = new byte[5];
        }

        [TearDown]
        public void Teardown()
        {
            Assert.AreEqual(map(_toSend, b => string.Join("", b)), 
                map(_received, b => string.Join("", b)));

            iter(_streams, s => s.Dispose());
        }

        SharedMemoryStream MakeStream(Some<string> name, bool store = true)
        {
            var stream = SharedMemoryStream.Create(name);
            stream.Write(_toSend, 0, _toSend.Length);

            if(store)
                _streams = _streams.Add(stream);

            return stream;
        }

        [Test]
        public void Returns_data_sent_intraprocess()
        {
            var stream = MakeStream("stream1");
            stream.Seek(0, SeekOrigin.Begin);
            stream.Read(_received, 0, _received.Length);
        }

        [Test]
        public void Returns_data_sent_interprocess_when_file_isopen()
        {
            var stream = MakeStream("stream2", false);
            stream.Flush();

            var task = Task.Factory.StartNew(() =>
            {
                var pickupStream = SharedMemoryStream.Open("stream2");
                _streams = _streams.Add(pickupStream);
                pickupStream.Read(_received, 0, _received.Length);
            });

            Task.WaitAll(task);
        }

        [Test]
        public void Returns_two_datas_when_reserialized()
        {
            var stream = MakeStream("stream3", false);

            var extra = new byte[] { 6, 7, 8 };
            stream.Write(extra, 0, extra.Length);
            stream.Seek(0, SeekOrigin.Begin);

            stream.Read(_received, 0, _received.Length);
            stream.Read(extra, 0, extra.Length);

            Assert.AreEqual(new byte[] { 6, 7, 8 }, extra);
            
            var bytesRead = stream.Read(new byte[1], 0, 1);
            Assert.AreEqual(0, bytesRead);

            Assert.AreEqual(8, stream.Length);
            Assert.AreEqual(8, stream.Position);

            stream.Dispose();
        }
    }

    [TestFixture]
    public class SharedMemoryStreamLengthChangingTests
    {     
        [TestCase(100)]
        [TestCase(700)]
        [TestCase(1200)]
        [TestCase(2000)]
        public void Changes_size_when_new_length_is_smaller_and_larger_using_backing_file(int length)
        {
            var stream = SharedMemoryStream.Create(length.ToString());

            stream.Write(new byte[1000], 0, 1000);
            Assert.That(stream.Length, Is.EqualTo(1000));

            stream.SetLength(length);
            Assert.That(stream.Length, Is.EqualTo(length));

            var newLengthData = new byte[length];
            stream.Read(newLengthData, 0, length);
            Assert.That(newLengthData.Length, Is.EqualTo(length));

            stream.Dispose();
        }
    }

    [TestFixture]
    public class SharedMemoryStreamSeekTests
    {
        Lst<SharedMemoryStream> _streams;

        SharedMemoryStream MakeStream(Some<string> name, bool store = true)
        {
            var stream = SharedMemoryStream.Create(name);
            Assert.AreEqual(0, stream.Position);

            stream.Write(new byte[10], 0, 10);
            Assert.AreEqual(10, stream.Position);

            if(store)
                _streams = _streams.Add(stream);

            return stream;
        }

        [SetUp]
        public void Setup()
        {
            _streams = List<SharedMemoryStream>();
        }

        [TearDown]
        public void TearDown() => iter(_streams, stream => stream.Dispose());

        [Test]
        public void Should_update_positon_from_begin()
        {
            var stream = MakeStream("begin");

            stream.Seek(5, System.IO.SeekOrigin.Begin);

            Assert.AreEqual(5, stream.Position);
        }

        [Test]
        public void Should_throw_if_seeking_before_beginning()
        {
            var stream = MakeStream("negative");
            
            Assert.Throws<ArgumentException>(() => stream.Seek(-1, System.IO.SeekOrigin.Begin))
                .Message.Equals("Can't seek before beginning of stream");
        }

        [Test]
        public void Should_update_positon_from_current()
        {
            var stream = MakeStream("current");

            stream.Seek(-3, System.IO.SeekOrigin.Current);

            Assert.AreEqual(7, stream.Position);
        }

        [Test]
        public void Should_update_positon_from_end()
        {
            var stream = MakeStream("end");

            stream.Seek(-4, System.IO.SeekOrigin.End);

            Assert.AreEqual(6, stream.Position);
        }

        [Test]
        public void Should_throw_if_seeking_beyond_end()
        {
            var stream = MakeStream("overflow");
            
            Assert.Throws<ArgumentException>(() => stream.Seek(11, System.IO.SeekOrigin.Begin))
                .Message.Equals("Can't seek beyond end of stream");
        }
    }

    [TestFixture]
    public class SharedMemorySettingTests
    {
        SharedMemoryStream _stream;

        [SetUp]
        public void Setup() => _stream = SharedMemoryStream.Create("settings");

        [TearDown]
        public void Teardown() => _stream.Dispose();

        [Test]
        public void Should_set_position_and_length()
        {
            Assert.AreEqual(0, _stream.Position);
            Assert.AreEqual(0, _stream.Length);
        }

        [Test]
        public void Should_support_read_write_and_seek()
        {
            Assert.IsTrue(_stream.CanRead);
            Assert.IsTrue(_stream.CanSeek);
            Assert.IsTrue(_stream.CanWrite);
        }
    }
}