using NUnit.Framework;
using System;
using System.Threading.Tasks;
using System.Runtime.Serialization.Formatters.Binary;
using static LanguageExt.Prelude;

namespace Psns.Common.InterProcess.Tests
{
    [TestFixture()]
    public class SharedMemoryStreamTests
    {
        [Serializable]
        struct TestData
        {
            public string Name;
            public int[] Ids;
        }

        [Test()]
        public void Returns_data_sent_intraprocess()
        {
            var data = new TestData { Ids = new[] { 1, 2, 3, 4 }, Name = "test data" };
            var stream = SharedMemoryStream.Create("process1");
            var formatter = new BinaryFormatter();
            formatter.Serialize(stream, data);

            var received = (TestData)formatter.Deserialize(stream);

            stream.Dispose();

            Assert.That(map(received, r => string.Format("", r.Name, string.Join("", r.Ids))),
                Is.EqualTo(map(data, d => string.Format("", d.Name, string.Join("", d.Ids)))));
        }

        [Test()]
        public void Returns_data_sent_interprocess_when_file_isopen()
        {
            var data = new TestData { Ids = new[] { 1, 2, 3, 4 }, Name = "test data" };
            var formatter = new BinaryFormatter();
            var stream = SharedMemoryStream.Create("process2");
            formatter.Serialize(stream, data);

            var received = new TestData();
            var task = Task.Factory.StartNew(() =>
            {
                using(var pickupStream = SharedMemoryStream.Open("process2", stream.Length))
                {
                    received = (TestData)formatter.Deserialize(pickupStream);
                }
            });

            Task.WaitAll(task);
            stream.Dispose();

            Assert.That(map(received, r => string.Format("", r.Name, string.Join("", r.Ids))),
                Is.EqualTo(map(data, d => string.Format("", d.Name, string.Join("", d.Ids)))));
        }

        [Test()]
        public void Changes_size_when_new_length_is_smaller()
        {
            using(var stream = SharedMemoryStream.Create("stream1"))
            {
                stream.Write(new byte[1000], 0, 1000);
                Assert.That(stream.Length, Is.EqualTo(1000));

                stream.SetLength(700);
                Assert.That(stream.Length, Is.EqualTo(700));

                var smaller = new byte[700];
                stream.Read(smaller, 0, 700);
                Assert.That(smaller.Length, Is.EqualTo(700));
            }
        }

        [Test()]
        public void Changes_size_when_new_length_is_larger()
        {
            using(var stream = SharedMemoryStream.Create("stream1"))
            {
                stream.Write(new byte[1000], 0, 1000);
                Assert.That(stream.Length, Is.EqualTo(1000));

                stream.SetLength(1200);
                Assert.That(stream.Length, Is.EqualTo(1200));

                var smaller = new byte[1200];
                stream.Read(smaller, 0, 1200);
                Assert.That(smaller.Length, Is.EqualTo(1200));
            }
        }
    }
}