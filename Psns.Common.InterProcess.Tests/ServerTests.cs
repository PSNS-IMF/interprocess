using NUnit.Framework;

namespace Psns.Common.InterProcess.Tests
{
    [TestFixture]
    public class ServerTests
    {
        [Test]
        public void Returns_correct_number_of_threads_running()
        {
            var server = Server.Create<string>("server1", message => { });
        }
    }
}
