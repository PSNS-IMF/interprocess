using System;
using System.IO.Pipes;

using LanguageExt;
using static LanguageExt.Prelude;

namespace Psns.Common.InterProcess
{
    public static class Server
    {
        /// <summary>
        /// Server creation function
        /// </summary>
        /// <param name="name">A name that calling clients should know.</param>
        /// <param name="messageHandler">An function to call when a message is received.</param>
        /// <returns>A new Server</returns>
        public static Server<T> Create<T>(Some<string> name, Action<T> messageHandler)
        {
            return new Server<T>(name, messageHandler);
        }
    }

    public class Server<T>
    {
        readonly string _name;
        readonly Action<T> _messageHandler;

        internal Server(Some<string> name, Some<Action<T>> messageHandler)
        {
            _name = name;
            _messageHandler = messageHandler;

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
                            //_messageHandler(message)
                        });

                    pipeState.WaitForPipeDrain(); // wait for client to receive all sent bytes
                }
            }, pipe);

            return unit;
        }
    }
}