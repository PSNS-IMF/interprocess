using NUnit.Framework;
using System;
using System.Linq;
using static LanguageExt.Prelude;

namespace Psns.Common.InterProcess.Tests
{
    [TestFixture]
    public class SharedMemoryFileTests
    {
        [Test]
        public void Truncates_written_data_when_larger_than_file()
        {
            var creator = SharedMemoryFile.CreateOrOpen("file1", 5);
            var buffer = new byte[] { 1, 2, 3, 4, 5, 6 };

            creator.Write(buffer);

            var reader = SharedMemoryFile.Open("file1");
            var readBuffer = new byte[5];
            reader.Read(readBuffer);

            Assert.AreEqual(new byte[] { 1, 2, 3, 4, 5 }, readBuffer);

            creator.Dispose();
            reader.Dispose();
        }

        [Test]
        public void Opens_existing_when_creating_a_file_with_existing_name()
        {
            var creator = SharedMemoryFile.CreateOrOpen("file1", 2);
            var buffer = new byte[]{ 1, 2 };
            creator.Write(buffer);

            var creator2 = SharedMemoryFile.CreateOrOpen("file1", 5);
            var readBuffer = new byte[5];
            creator2.Read(readBuffer);

            Assert.AreEqual(buffer, readBuffer.Take(2));

            creator.Dispose();
            creator2.Dispose();
        }

        [Test]
        public void Throws_when_opening_a_nonexisting_file()
        {
            Assert.That(() => SharedMemoryFile.Open("null"), 
                Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo("SharedMemoryFile null does not exist"));
        }

        [Test]
        public void Allows_same_name_created_after_dispose()
        {
            var file = SharedMemoryFile.CreateOrOpen("file1", 1);
            file.Dispose();

            file = SharedMemoryFile.CreateOrOpen("file1", 1);
            file.Dispose();
        }

        [Test]
        public void Reads_specified_view_of_bytes()
        {
            using(var file = SharedMemoryFile.CreateOrOpen("file", 5))
            {
                var existing = new byte[] { 1, 2, 3, 4, 5 };
                file.Write(existing);

                var buffer = new byte[10];
                file.Read(2, buffer, 7, 3);

                Assert.AreEqual(new byte[] { 3, 4, 5 }, buffer.Skip(7));
            }
        }
    }
}