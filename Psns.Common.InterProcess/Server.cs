using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;

using LanguageExt;
using static LanguageExt.Map;
using static LanguageExt.Prelude;

namespace Psns.Common.InterProcess
{
    public class Server : IDisposable
    {
        public static Server Create(Some<string> name)
        {
            return new Server(name);
        }

        public int ThreadsRunning => 0;

        readonly string _name;
        Option<Map<int, NamedPipeServerStream>> _pipes;

        Server(Some<string> name)
        {
            _name = name;

            BeginListening();
        }

        Unit BeginListening()
        {
            var pipes = match(
                _pipes, 
                Some: p => p, 
                None: () => Map<int, NamedPipeServerStream>());

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

                    pipes = remove(pipes, pipeState.GetHashCode());
                    _pipes = pipes;
                }
            }, pipe);

            pipes = add(pipes, pipe.GetHashCode(), pipe);
            _pipes = pipes;

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
                    match(_pipes,
                        Some: p => 
                            {
                                iter(p, (k, v) => v.Dispose());
                                return unit;
                            },
                        None: () => unit);
                }

                disposedValue = true;
            }
        }

        public void Dispose() { Dispose(true); }

        #endregion
    }
}
