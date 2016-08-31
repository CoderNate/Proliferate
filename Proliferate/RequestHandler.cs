using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Proliferate
{
    using StreamHandlerFunc = Func<PipeReadWrapper, PipeWriteWrapper, Task>;
    using ReaderWriterHandlerFunc = Func<System.IO.StreamReader, System.IO.StreamWriter, Task>;

    public static class RequestHandlerFactory
    {
        public static RequestHandler FromTaskReturning(StreamHandlerFunc handler)
        {
            return new RequestHandler(handler);
        }
        public static RequestHandler FromTaskReturning(ReaderWriterHandlerFunc handler)
        {
            StreamHandlerFunc adapter = (incomingRequestStream, outgoingResponseStream) =>
            {
                var reader = new System.IO.StreamReader(incomingRequestStream);
                var writer = new System.IO.StreamWriter(outgoingResponseStream);
                return handler(reader, writer);
            };
            return new RequestHandler(adapter);
        }

        public static RequestHandler FromAction(Action<PipeReadWrapper, PipeWriteWrapper> handler)
        {
            StreamHandlerFunc taskReturningWrapper = (incomingRequestStream, outgoingResponseStream) =>
            {
                handler(incomingRequestStream, outgoingResponseStream);
                return Task.FromResult(false);
            };
            return new RequestHandler(taskReturningWrapper);
        }

        public static RequestHandler FromAction(Action<System.IO.StreamReader, System.IO.StreamWriter> handler)
        {
            StreamHandlerFunc taskReturningWrapper = (incomingRequestStream, outgoingResponseStream) =>
            {
                var reader = new System.IO.StreamReader(incomingRequestStream);
                var writer = new System.IO.StreamWriter(outgoingResponseStream);
                handler(reader, writer);
                try
                {
                    writer.Flush();
                }
                catch (System.IO.IOException)
                {
                    //If the pipe is already closed, we'll get an IOException, but that's okay.
                }
                return Task.FromResult(false);
            };
            return new RequestHandler(taskReturningWrapper);
        }
    }

    /// <summary>
    /// Simple container class for a routine that reads a request from a stream and writes a response to another stream.
    /// </summary>
    public class RequestHandler
    {
        internal RequestHandler(StreamHandlerFunc streamHandler)
        {
            this.StreamHandlerFunc = streamHandler;
        }
        internal readonly StreamHandlerFunc StreamHandlerFunc;
    }
}
