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

        public int ThreadsRunning
        {
            get
            {
                return match(_threads, 
                    threads => fold(threads, 
                        0, 
                        (state, t) => t.Status == TaskStatus.Running ? state + 1 : state + 0),
                    () => 0);
            }
        }

        readonly string _name;
        readonly CancellationTokenSource _tokenSource;
        readonly CancellationToken _token;
        Option<Lst<Task>> _threads;

        Server(Some<string> name)
        {
            _name = name;
            _tokenSource = new CancellationTokenSource();
            _token = _tokenSource.Token;
        }

        Unit BeginListening()
        {
            if(_token.IsCancellationRequested) return unit;

            var threads = match(_threads, t => t, () => List<Task>());

            threads = threads.Add(Task.Factory.StartNew(() =>
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
                    }
                }, pipe);
            }, _token));new TaskCom

            _threads = threads;
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
                    _tokenSource.Cancel();

                    ifSome(_threads, threads =>
                        iter(threads, thread => thread.Dispose()));

                    _tokenSource.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose() => Dispose(true);

        #endregion
    }
}
