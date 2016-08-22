using NUnit.Framework;
using System;
using System.Threading.Tasks;
using System.Runtime.Serialization.Formatters.Binary;
using LanguageExt;
using static LanguageExt.Prelude;

namespace Psns.Common.InterProcess.Tests
{
    [TestFixture]
    public class SharedMemorySerializationTests
    {
        [Serializable]
        struct TestData
        {
            public string Name;
            public int[] Ids;
        }

        TestData _toSend, _received;
        Lst<SharedMemoryStream> _streams;
        BinaryFormatter _formatter;

        [SetUp]
        public void Setup()
        {
            _streams = List<SharedMemoryStream>();
            _toSend = new TestData { Ids = new[] { 1, 2, 3, 4 }, Name = "test data" };
            _formatter = new BinaryFormatter();
        }

        [TearDown]
        public void Teardown()
        {
            Assert.That(map(_received, r => string.Format("", r.Name, string.Join("", r.Ids))),
                Is.EqualTo(map(_toSend, d => string.Format("", d.Name, string.Join("", d.Ids)))));

            _streams.Iter(s => 
            { 
                Assert.AreEqual(251, s.Length);
                Assert.AreEqual(251, s.Position);

                s.Dispose(); 
            });
        }

        SharedMemoryStream MakeStream(Some<string> name, bool store = true)
        {
            var stream = SharedMemoryStream.Create(name);
            _formatter.Serialize(stream, _toSend);

            if(store)
                _streams = _streams.Add(stream);

            return stream;
        }

        [Test]
        public void Returns_data_sent_intraprocess()
        {
            var stream = MakeStream("stream1");
            _received = (TestData)_formatter.Deserialize(stream);

            Assert.AreEqual(251, stream.Length);
            Assert.AreEqual(251, stream.Position);
        }

        [Test]
        public void Returns_data_sent_interprocess_when_file_isopen()
        {
            var stream = MakeStream("stream2", false);

            var task = Task.Factory.StartNew(() =>
            {
                var pickupStream = SharedMemoryStream.Open("stream2", stream.Length);
                _streams = _streams.Add(pickupStream);
                _received = (TestData)_formatter.Deserialize(pickupStream);
            });

            Task.WaitAll(task);
        }
    }

    [TestFixture]
    public class SharedMemoryStreamLengthChangingTests
    {
        SharedMemoryStream _stream;

        [SetUp]
        public void Setup()
        {
            _stream = SharedMemoryStream.Create("stream3");

            _stream.Write(new byte[1000], 0, 1000);
            Assert.That(_stream.Length, Is.EqualTo(1000));
        }

        [TearDown]
        public void Teardown() => _stream.Dispose();

        [TestCase(700)]
        [TestCase(1200)]
        public void Changes_size_when_new_length_is_smaller_and_larger(int newLength)
        {
            _stream.SetLength(newLength);
            Assert.That(_stream.Length, Is.EqualTo(newLength));

            var smaller = new byte[newLength];
            _stream.Read(smaller, 0, newLength);
            Assert.That(smaller.Length, Is.EqualTo(newLength));
        }
    }

    [TestFixture]
    public class SharedMemorySettingTests
    {
        SharedMemoryStream _stream;

        [SetUp]
        public void Setup() => _stream = SharedMemoryStream.Create("stream3");

        [TearDown]
        public void Teardown() => _stream.Dispose();

        [Test]
        public void Should_set_position_and_length()
        {
            Assert.AreEqual(0, _stream.Position);
            Assert.AreEqual(0, _stream.Length);
        }

        [Test]
        public void Should_only_support_read_and_write()
        {
            Assert.IsTrue(_stream.CanRead);
            Assert.IsFalse(_stream.CanSeek);
            Assert.IsTrue(_stream.CanWrite);
        }

        [Test]
        public void Show_throw_when_seek_called() => Assert.Throws<InvalidOperationException>(() => _stream.Seek(0, 0));
    }
}