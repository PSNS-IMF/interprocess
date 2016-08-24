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

            creator = creator.Write(buffer);

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
            var creator = SharedMemoryFile.CreateOrOpen("file2", 2);
            var buffer = new byte[]{ 1, 2 };
            creator.Write(buffer);

            var creator2 = SharedMemoryFile.CreateOrOpen("file2", 5);
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
            use(SharedMemoryFile.CreateOrOpen("file3", 1), file => file);
            use(SharedMemoryFile.CreateOrOpen("file3", 1), file => file);
        }

        [Test]
        public void Reads_specified_view_of_bytes()
        {
            use(
                SharedMemoryFile.CreateOrOpen("fil4", 5),
                file =>
                {
                    var existing = new byte[] { 1, 2, 3, 4, 5 };
                    file = file.Write(existing);

                    var buffer = new byte[10];
                    file.Read(2, buffer, 7, 3);

                    Assert.AreEqual(new byte[] { 3, 4, 5 }, buffer.Skip(7));

                    return file;
                });
        }

        [Test]
        public void Returns_correct_file_size()
        {
            use(
                SharedMemoryFile.CreateOrOpen("fileSize", 0XEE6B2800),
                file =>
                {
                    Assert.AreEqual(0XEE6B2800, file.Size);
                    return file;
                });
        }

        [Test]
        public void Changes_file_size_as_more_data_is_written()
        {
            var file = SharedMemoryFile.CreateOrOpen("writer", 4);
            Assert.AreEqual(4, file.Size);

            var data = new byte[] { 0x01, 0x2, 0x3, 0x4 };
            file = file.Write(data, 0, 4);
            var buffer = new byte[4];
            file.Read(buffer);
            Assert.AreEqual(data, buffer);

            file = file.Write(0, new byte[] { 0x7, 0x8 }, 0, 2);
            file.Read(buffer);
            Assert.AreEqual(new byte[] { 0x7, 0x8, 0x3, 0x4 }, buffer);

            file.Dispose();
        }

        [TestCase(-2)]
        [TestCase(2)]
        public void Truncates_or_expands_when_resizing(int changeBy)
        {
            var file = SharedMemoryFile.CreateOrOpen("resize", 4);

            var data = new byte[] { 0x1, 0x2, 0x3, 0x4 };
            file = file.Write(data);

            var newSize = data.Length + changeBy;
            file = file.Resize(newSize);
            Assert.AreEqual(newSize, file.Size);

            var newData = new byte[newSize];
            file.Read(0, newData, 0, newData.Length);
            
            for(var i = 0; i < newData.Length && i < data.Length; i++)
                Assert.AreEqual(newData[i], data[i]);

            file.Dispose();
        }
    }
}