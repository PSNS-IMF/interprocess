using NUnit.Framework;

namespace Psns.Common.InterProcess.Tests
{
    [TestFixture]
    public class ServerTests
    {
        [Test]
        public void Returns_correct_number_of_threads_running()
        {
            var server = Server.Create("server1");
            Assert.AreEqual(1, server.ThreadsRunning);
            server.Dispose();
        }
    }
}
