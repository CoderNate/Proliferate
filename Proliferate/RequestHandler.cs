using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Proliferate
{
    using StreamHandlerFunc = Func<System.IO.Stream, System.IO.Stream, Task>;
    using ReaderWriterHandlerFunc = Func<System.IO.StreamReader, System.IO.StreamWriter, Task>;

    public static class RequestHandlerFactory
    {
        public static RequestHandler FromTaskReturning(StreamHandlerFunc handler)
        {
            return new RequestHandler(handler);
        }
        public static RequestHandler FromTaskReturning(ReaderWriterHandlerFunc handler)
        {
            StreamHandlerFunc adapter = (requestStream, responseStream) =>
            {
                var reader = new System.IO.StreamReader(requestStream);
                var writer = responseStream == null ? null : new System.IO.StreamWriter(responseStream);
                return handler(reader, writer);
            };
            return new RequestHandler(adapter);
        }

        public static RequestHandler FromAction(Action<System.IO.Stream, System.IO.Stream> handler)
        {
            StreamHandlerFunc taskReturningWrapper = (requestStream, responseStream) =>
            {
                handler(requestStream, responseStream);
                return Task.FromResult(false);
            };
            return new RequestHandler(taskReturningWrapper);
        }

        public static RequestHandler FromAction(Action<System.IO.StreamReader, System.IO.StreamWriter> handler)
        {
            StreamHandlerFunc taskReturningWrapper = (requestStream, responseStream) =>
            {
                var reader = new System.IO.StreamReader(requestStream);
                var writer = responseStream == null ? null : new System.IO.StreamWriter(responseStream);
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
