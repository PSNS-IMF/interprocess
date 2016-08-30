using System;
using System.IO;
using System.IO.Pipes;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LanguageExt;
using static LanguageExt.Prelude;

namespace Psns.Common.InterProcess
{
    public class Client
    {
        readonly string _serverName;

        Client(Some<string> serverName)
        {
            _serverName = serverName;
        }

        public static Client Create(Some<string> serverName)
        {
            return new Client(serverName);
        }

        public string Send(Some<string> message)
        {
            var pipe = new NamedPipeClientStream(".", 
                _serverName, 
                PipeDirection.InOut, 
                PipeOptions.None, 
                System.Security.Principal.TokenImpersonationLevel.None);

            pipe.Connect(); // idefinite timeout

            var response = use(new StreamReader(pipe),
                reader => reader.ReadLine());

            pipe.Dispose();

            return response;
        }
    }
}
