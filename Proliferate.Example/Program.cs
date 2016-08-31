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

            //Make sure pipeNamePrefix is random so separate instances of your program don't talk to each other's child processes.
            var pipeNamePrefix = System.Guid.NewGuid().ToString("N");

            var childProcessMainMethod = typeof(ChildProcess).GetMethod("Start");
            var w2 = System.Diagnostics.Stopwatch.StartNew();
            var exePath = ExecutableGenerator.Instance.GenerateExecutable(childProcessMainMethod, 
                ExecutableType.Default, "Launcher");
            var elapsed = w2.Elapsed.ToString();
            //The CreateNoWindow option reduces startup time of the child process.
            var startInfo = new System.Diagnostics.ProcessStartInfo(exePath, pipeNamePrefix)
            { CreateNoWindow = true, UseShellExecute = false };
            var w = System.Diagnostics.Stopwatch.StartNew();
            var proc = System.Diagnostics.Process.Start(startInfo);
            

            var client = new Proliferate.ProliferateClient(pipeNamePrefix);
            using (var cancelTokenSource = new System.Threading.CancellationTokenSource())
            {
                client.StartChildPinger(cancelTokenSource.Token);
                using (var streams = client.GetSendAndReceiveStreams())
                {
                    streams.OutgoingRequestStream.Close();
                    var buffer = new byte[1024];
                    var read = streams.IncomingResponseStream.Read(buffer, 0, buffer.Length);
                }
                Console.WriteLine("Time from process start until response received: " + w.Elapsed.ToString());
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
                    var writerAndReader = client.GetRequestWriterAndResponseReader();
                    {
                        writerAndReader.OutgoingRequestWriter.Write(request);
                        writerAndReader.OutgoingRequestWriter.Close();
                        response = writerAndReader.IncomingResponseReader.ReadToEnd();
                        writerAndReader.IncomingResponseReader.Close();
                    }
                    Console.WriteLine("Child process sent back: " + response);
                }
            }
        }
        
    }

    
    public class ChildProcess
    {
        public static void Start(string[] args)
        {
            
            if (args.Length != 1)
            {
                throw new ArgumentException(
                    "Expected a single command line argument representing the named pipe name prefix of the child process.");
            }
            var pipeNamePrefix = args[0];
            Console.WriteLine("Child process is starting...");
            var handler = Proliferate.RequestHandlerFactory.FromTaskReturning(Handler);
            var server = new Proliferate.ProliferateServer(handler, 1, pipeNamePrefix);
            var cancelTokenSrc = new System.Threading.CancellationTokenSource();
            const bool waitForReadKey = false;
            if (waitForReadKey)
            {
                server.RunAsync(cancelTokenSrc.Token);
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
            }
            else
            {
                server.Run(cancelTokenSrc.Token);
            }
        }

        private static async Task Handler(PipeReadWrapper incomingRequestStream, PipeWriteWrapper outgoingResponseStream)
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
            incomingRequestStream.Close();
            var writer = new System.IO.StreamWriter(outgoingResponseStream);
            await writer.WriteAsync(string.Format("Hey, thanks for saying '{0}'.", System.Text.Encoding.UTF8.GetString(theBytes)));
            try
            {
                writer.Close();
            }
            catch (System.IO.IOException)
            {
                Console.WriteLine("Got an IO exception. The parent process must've closed the pipe without waiting for a reply.");
            }
            Console.WriteLine("Child process finished handling a request.");
        }

        private static async Task SimpleHandler(System.IO.StreamReader incomingRequestReader,
                System.IO.StreamWriter outgoingResponseWriter)
        {
            Console.WriteLine("Child process is handling a request on thread "
                    + System.Threading.Thread.CurrentThread.ManagedThreadId.ToString());
            var requestString = await incomingRequestReader.ReadToEndAsync();
            await outgoingResponseWriter.WriteAsync("Hey, thanks for saying '" + requestString + "'.");
            Console.WriteLine("Child process finished handling a request.");
        }
    }
}
