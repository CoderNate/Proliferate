using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization.Formatters.Binary;

namespace Proliferate
{
    using StreamHandlerFunc = Func<PipeReadWrapper, PipeWriteWrapper, Task>;
    using ReaderWriterHandlerFunc = Func<System.IO.StreamReader, System.IO.StreamWriter, Task>;

    public static class RequestHandlerFactory
    {
        private static readonly BinaryFormatter _binaryFormatter = new BinaryFormatter();

        public static RequestHandler FromTaskReturning(StreamHandlerFunc handler)
        {
            return new RequestHandler(handler);
        }
        public static RequestHandler FromTaskReturning(ReaderWriterHandlerFunc handler)
        {
            StreamHandlerFunc adapter = (incomingRequestStream, outgoingResponseStream) =>
            {
                var reader = new System.IO.StreamReader(incomingRequestStream);
                using (var writer = new System.IO.StreamWriter(outgoingResponseStream))
                {
                    return handler(reader, writer);
                }
            };
            return new RequestHandler(adapter);
        }
        public static RequestHandler FromTaskReturning<Trequest, Tresponse>(
                Func<Trequest, Task<Tresponse>> handler)
        {
            StreamHandlerFunc adapter = async (incomingRequestStream, outgoingResponseStream) =>
            {
                var requestObj = (Trequest)_binaryFormatter.Deserialize(incomingRequestStream);
                //Need to call CheckRemainingByteChunkSize to trigger a read from the stream to make sure
                //we've read everything from the pipe. Otherwise we'll get a hang.
                incomingRequestStream.CheckRemainingByteChunkSize();
                incomingRequestStream.Close();
                var responseObj = await handler(requestObj);
                _binaryFormatter.Serialize(outgoingResponseStream, responseObj);
            };
            return new RequestHandler(adapter);
        }

        public static RequestHandler FromAction(Action<PipeReadWrapper, PipeWriteWrapper> handler)
        {
            StreamHandlerFunc taskReturningWrapper = (incomingRequestStream, outgoingResponseStream) =>
            {
                handler(incomingRequestStream, outgoingResponseStream);
                return Task.FromResult(false); //The value false isn't used for anything, just need a task.
            };
            return new RequestHandler(taskReturningWrapper);
        }

        public static RequestHandler FromAction(Action<System.IO.StreamReader, System.IO.StreamWriter> handler)
        {
            StreamHandlerFunc taskReturningWrapper = (incomingRequestStream, outgoingResponseStream) =>
            {
                var reader = new System.IO.StreamReader(incomingRequestStream);
                using (var writer = new System.IO.StreamWriter(outgoingResponseStream))
                {
                    handler(reader, writer);
                }
                return Task.FromResult(false); //The value false isn't used for anything, just need a task.
            };
            return new RequestHandler(taskReturningWrapper);
        }

        public static RequestHandler FromAction<Trequest, Tresponse>(Func<Trequest, Tresponse> handler)
        {
            StreamHandlerFunc taskReturningWrapper = (incomingRequestStream, outgoingResponseStream) =>
            {
                var requestObj = (Trequest)_binaryFormatter.Deserialize(incomingRequestStream);
                //Need to call CheckRemainingByteChunkSize to trigger a read from the stream to make sure
                //we've read everything from the pipe. Otherwise we'll get a hang.
                incomingRequestStream.CheckRemainingByteChunkSize();
                incomingRequestStream.Close();
                var responseObj = (Tresponse)handler(requestObj);
                _binaryFormatter.Serialize(outgoingResponseStream, responseObj);
                return Task.FromResult(false); //The value false isn't used for anything, just need a task.
            };
            return new RequestHandler(taskReturningWrapper);
        }
    }

    /// <summary>
    /// Simple container class for a routine that reads a request from a stream and writes a response to another stream.
    /// </summary>
    public sealed class RequestHandler
    {
        internal RequestHandler(StreamHandlerFunc streamHandler)
        {
            this.StreamHandlerFunc = streamHandler;
        }
        internal readonly StreamHandlerFunc StreamHandlerFunc;
    }
}
