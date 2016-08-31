using System;
using System.IO;
using System.IO.Pipes;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using LanguageExt;
using static LanguageExt.List;
using static LanguageExt.Prelude;

namespace Psns.Common.InterProcess
{
    public class Server : IDisposable
    {
        public static Server Create(Some<string> name)
        {
            return new Server(name);
        }

        public int ThreadsRunning => _serverCount;

        readonly string _name;
        Option<Lst<NamedPipeServerStream>> _pipes;

        Server(Some<string> name)
        {
            _name = name;

            BeginListening();
        }

        Unit BeginListening()
        {
            // throws
            var pipe = new NamedPipeServerStream(_name,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous);

            // throws
            pipe.BeginWaitForConnection(result =>
            {
                using(var pipeState = (NamedPipeServerStream)result.AsyncState)
                {
                    // throws
                    pipeState.EndWaitForConnection(result);

                    BeginListening();

                    // do work with client's security token
                    pipeState.RunAsClient(() =>
                        {

                        });

                    pipeState.WaitForPipeDrain(); // wait for client to receive all sent bytes

                    _serverCount--;
                }
            }, pipe);

            _serverCount++;

            return unit;
        }

        #region IDisposable Support

        bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if(!disposedValue)
            {
                if(disposing)
                {

                }

                disposedValue = true;
            }
        }

        public void Dispose() => Dispose(true);

        #endregion
    }
}
