using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Proliferate.Example
{
    class Program
    {
        static void Main(string[] args)
        {
            var thisEXEname = System.IO.Path.GetFileName(typeof(Program).Assembly.Location);
            var childProcessClassName = typeof(ChildProcess).FullName;
            var pipeNamePrefix = "MyChild";
            var childProcessArgs = string.Format(
                "-assemblyFile \"{0}\" -typeName {1} -methodName {2} -pipeNamePrefix {3}", 
                thisEXEname, childProcessClassName, typeof(ChildProcess).GetMethod("Start").Name, pipeNamePrefix);
            var proc = System.Diagnostics.Process.Start("Proliferate.exe", childProcessArgs);
            var client = new Proliferate.ClientForParentProcess(pipeNamePrefix);
            var cancelTokenSource = new System.Threading.CancellationTokenSource();
            client.StartChildPinger(cancelTokenSource.Token);
            using (var streams = client.GetSendAndReceiveStreams())
            {
                streams.OutgoingRequestStream.Dispose();
                streams.IncomingResponseStream.Dispose();
            }
            while (true)
            {
                Console.WriteLine("Enter a string to send to the child process or press enter to end the program...");
                var request = Console.ReadLine();
                if (System.Text.RegularExpressions.Regex.IsMatch(request, @"^\r*\n*$"))
                {
                    client.SendShutdown();
                    cancelTokenSource.Cancel();
                    return;
                }
                string response;
                using (var writerAndReader = client.GetRequestWriterAndResponseReader())
                {
                    writerAndReader.OutgoingRequestWriter.Write(request);
                    //writerAndReader.OutgoingRequestWriter.Flush();
                    writerAndReader.OutgoingRequestWriter.Close();
                    response = writerAndReader.IncomingResponseReader.ReadToEnd();
                }
                Console.WriteLine("Child process sent back: " + response);
            }
        }
    }


    public class ChildProcess
    {
        public static void Start(IDictionary<string, string> argsDict)
        {
            Console.WriteLine("Child process arguments: "
                + String.Join(" ", argsDict.Select(a => a.ToString()).ToArray()));
            var pipeNamePrefix = argsDict["-pipeNamePrefix"];
            Console.WriteLine("Child process is starting...");
            var handler = Proliferate.RequestHandlerFactory.FromTaskReturning(Handler);
            var server = new Proliferate.ServerForChildProcess(handler, 1, pipeNamePrefix);
            var cancelTokenSrc = new System.Threading.CancellationTokenSource();
            server.Run(cancelTokenSrc.Token);
            //System.Console.WriteLine("Press any key to exit.");
            //Console.ReadKey();
        }

        public static async Task Handler(System.IO.Stream incomingRequestStream, System.IO.Stream outgoingResponseStream)
        {
            Console.WriteLine("Child process is handling a request on thread "
                    + System.Threading.Thread.CurrentThread.ManagedThreadId.ToString());
            byte[] theBytes = null;
            using (var memStream = new System.IO.MemoryStream())
            {
                byte[] buffer = new byte[16 * 1024];
                int read;
                while ((read = await incomingRequestStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    memStream.Write(buffer, 0, read);
                }
                theBytes = memStream.ToArray();
            }
            var writer = new System.IO.StreamWriter(outgoingResponseStream);
            await writer.WriteAsync(string.Format("Hey, thanks for saying '{0}'.", System.Text.Encoding.UTF8.GetString(theBytes)));
            try
            {
                writer.Flush();
            }
            catch (System.IO.IOException)
            {
                Console.WriteLine("Got an IO exception. The parent process must've closed the pipe without waiting for a reply.");
            }
            Console.WriteLine("Child process finished handling a request.");
        }
        //public static async Task Handler(System.IO.StreamReader incomingRequestReader, System.IO.StreamWriter outgoingResponseWriter)
        //{
        //    Console.WriteLine("Child process is handling a request on thread "
        //            + System.Threading.Thread.CurrentThread.ManagedThreadId.ToString());
        //    var requestString = await incomingRequestReader.ReadToEndAsync();
        //    await outgoingResponseWriter.WriteAsync("Hey, thanks for saying '" + requestString + "'.");
        //    Console.WriteLine("Child process finished handling a request.");
        //}
    }
}
