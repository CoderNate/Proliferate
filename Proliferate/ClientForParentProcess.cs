using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Threading.Tasks;

namespace Proliferate
{
    /// <summary>
    /// Client used by the parent process for communicating with a child process.
    /// </summary>
    public class ClientForParentProcess
    {

        private readonly string _pipeNamePrefix;
        private readonly string _serverName;
        public ClientForParentProcess(string pipeNamePrefix, string serverName = ".")
        {
            _pipeNamePrefix = pipeNamePrefix;
            _serverName = serverName;
        }

        public struct StreamPair : IDisposable
        {
            public StreamPair(System.IO.Stream outgoingRequestStream, System.IO.Stream incomingResponseStream)
            {
                this.OutgoingRequestStream = outgoingRequestStream;
                this.IncomingResponseStream = incomingResponseStream;
            }
            public readonly System.IO.Stream OutgoingRequestStream;
            public readonly System.IO.Stream IncomingResponseStream;

            public void Dispose()
            {
                OutgoingRequestStream.Dispose();
                IncomingResponseStream.Dispose();
            }
        }

        public StreamPair GetSendAndReceiveStreams()
        {
            var outgoingRequestPipe = new NamedPipeClientStream(_serverName, _pipeNamePrefix + "ParentToChild",
                    PipeDirection.Out);
            outgoingRequestPipe.Connect();
            var responseId = Guid.NewGuid();
            var idBytes = responseId.ToByteArray();
            outgoingRequestPipe.Write(idBytes, 0, idBytes.Length);

            var incomingResponsePipe = new NamedPipeServerStream(
                    responseId.ToString(), PipeDirection.In);
            incomingResponsePipe.WaitForConnection();
            return new StreamPair(outgoingRequestPipe, incomingResponsePipe);
        }

        public struct RequestWriterAndResponseReader : IDisposable
        {
            public RequestWriterAndResponseReader(System.IO.StreamWriter outgoingRequestWriter,
                    System.IO.StreamReader incomingResponseReader)
            {
                this.OutgoingRequestWriter = outgoingRequestWriter;
                this.IncomingResponseReader = incomingResponseReader;
            }
            public readonly System.IO.StreamWriter OutgoingRequestWriter;
            public readonly System.IO.StreamReader IncomingResponseReader;

            public void Dispose()
            {
                OutgoingRequestWriter.Dispose();
                IncomingResponseReader.Dispose();
            }
        }

        public RequestWriterAndResponseReader GetRequestWriterAndResponseReader()
        {
            var pair = GetSendAndReceiveStreams();
            return new RequestWriterAndResponseReader(
                new System.IO.StreamWriter(pair.OutgoingRequestStream),
                new System.IO.StreamReader(pair.IncomingResponseStream));
        }

        public void SendShutdown()
        {
            using (var outgoingRequestPipe = new NamedPipeClientStream(_serverName, _pipeNamePrefix + "ParentToChild",
                    PipeDirection.Out))
            {
                var shutdownIdBytes = Constants.ShutdownId.ToByteArray();

                try
                {
                    outgoingRequestPipe.Connect(50);
                }
                catch (TimeoutException)
                {
                    return;
                }
                outgoingRequestPipe.Write(shutdownIdBytes, 0, shutdownIdBytes.Length);
                //outgoingRequestPipe.Flush();
            }
        }

        public System.IO.Stream GetSendStream()
        {
            var outgoingRequestPipe = new NamedPipeClientStream(_serverName, _pipeNamePrefix + "ParentToChild",
              PipeDirection.Out);
            outgoingRequestPipe.Connect();
            var noResponseId = Constants.NoResponseNeededId;
            var idBytes = noResponseId.ToByteArray();
            outgoingRequestPipe.Write(idBytes, 0, idBytes.Length);
            return outgoingRequestPipe;
        }

        private System.Threading.Timer _pingingTimer;
        public void StartChildPinger(System.Threading.CancellationToken cancellationToken)
        {
            var timer = new System.Threading.Timer(state =>
            {
                using (var outgoingRequestPipe = new NamedPipeClientStream(_serverName,
                    _pipeNamePrefix + Constants.PingPipeNameSuffix,
                    PipeDirection.Out))
                {
                    outgoingRequestPipe.Connect();
                }
            }, null, Constants.PingIntervalMilliseconds, Constants.PingIntervalMilliseconds);
            cancellationToken.Register(() => timer.Dispose());
            //We only need to store the timer in a field to prevent it from being garbage collected.
            _pingingTimer = timer;
        }

    }

}
