using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;

namespace Proliferate
{
    /// <summary>
    /// Client used by the parent process for communicating with a child process.
    /// </summary>
    public class ProliferateClient
    {

        private readonly string _pipeNamePrefix;
        private readonly string _serverName;
        public ProliferateClient(string pipeNamePrefix, string serverName = ".")
        {
            _pipeNamePrefix = pipeNamePrefix;
            _serverName = serverName;
        }

        /// <summary>
        /// Provides write and read wrappers for an underlying pipe connection.
        /// </summary>
        public struct StreamPair : IDisposable
        {
            public StreamPair(PipeWriteWrapper outgoingRequestStream, PipeReadWrapper incomingResponseStream,
                Action disposeUnderlyingPipe)
            {
                this.OutgoingRequestStream = outgoingRequestStream;
                this.IncomingResponseStream = incomingResponseStream;
                this._disposeUnderlyingPipe = disposeUnderlyingPipe;
            }
            public readonly PipeWriteWrapper OutgoingRequestStream;
            public readonly PipeReadWrapper IncomingResponseStream;
            private readonly Action _disposeUnderlyingPipe;
            public void Dispose()
            {
                _disposeUnderlyingPipe();
            }
        }

        public StreamPair GetSendAndReceiveStreams()
        {
            var outgoingRequestPipe = new NamedPipeClientStream(_serverName, _pipeNamePrefix + "ParentToChild",
                    PipeDirection.InOut);
            outgoingRequestPipe.Connect();
            return new StreamPair(new PipeWriteWrapper(outgoingRequestPipe), 
                new PipeReadWrapper(outgoingRequestPipe), outgoingRequestPipe.Dispose);
        }

        /// <summary>
        /// Provides StreamWriter and StreamReader for an underlying pipe connection.
        /// </summary>
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

        public class Messenger<Trequest, Tresponse>: IDisposable
        {
            private readonly StreamPair _streamPair;
            private readonly BinaryFormatter _formatter = new BinaryFormatter();
            public Messenger(StreamPair streamPair)
            {
                _streamPair = streamPair;
            }

            public void Dispose()
            {
                _streamPair.Dispose();
            }

            public Tresponse SendRequest(Trequest requestMessage)
            {
                //Using a buffered stream speeds things up by making fewer/larger
                //writes to the pipe.
                using (var bufferedStream = new System.IO.BufferedStream(_streamPair.OutgoingRequestStream))
                {
                    _formatter.Serialize(bufferedStream, requestMessage);
                }
                _streamPair.IncomingResponseStream.CheckRemainingByteChunkSize();
                return (Tresponse)_formatter.Deserialize(_streamPair.IncomingResponseStream);
            }
        }

        public Messenger<Trequest, Tresponse> GetMessenger<Trequest, Tresponse>()
        {
            var pair = GetSendAndReceiveStreams();
            return new Messenger<Trequest, Tresponse>(pair);
        }

        public void SendShutdown()
        {
            using (var outgoingRequestPipe = new NamedPipeClientStream(_serverName, _pipeNamePrefix + "ParentToChild",
                    PipeDirection.Out))
            {
                var shutdownIdBytes = BitConverter.GetBytes(Constants.ShutdownId); // Constants.ShutdownId.ToByteArray();

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
        
        /// <summary>
        /// Start a <see cref="System.Threading.Timer"/> that sends pings to the child process to keep it from shutting down.
        /// </summary>
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
            //No need to store the timer in a private field; it will not get garbage collected while it's running.
        }

    }

}
