using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Proliferate.Tests
{
    [TestFixture]
    public class StreamReaderWriterTests
    {

        [Test]
        public void TestStreamReaderWriter()
        {
            var client = CreateClient("Launcher-StreamReaderWriter");
            using (var cancelTokenSource = new System.Threading.CancellationTokenSource())
            {
                client.StartChildPinger(cancelTokenSource.Token);
                using (var readerWriter = client.GetRequestWriterAndResponseReader())
                {
                    var msg = "Hi";
                    readerWriter.OutgoingRequestWriter.Write(msg);
                    readerWriter.OutgoingRequestWriter.Close();
                    var resp = readerWriter.IncomingResponseReader.ReadToEnd();
                    Assert.AreEqual(msg, resp);
                }
            }
            client.SendShutdown();
        }

        [Test]
        public void TestSimultaneousRequests()
        {
            var client = CreateClient("Launcher-SimultaneousRequests");
            using (var cancelTokenSource = new System.Threading.CancellationTokenSource())
            {
                client.StartChildPinger(cancelTokenSource.Token);
                Parallel.For(0, 10, i =>
                {
                    using (var readerWriter = client.GetRequestWriterAndResponseReader())
                    {
                        var msg = "Hi";
                        readerWriter.OutgoingRequestWriter.Write(msg);
                        readerWriter.OutgoingRequestWriter.Close();
                        var resp = readerWriter.IncomingResponseReader.ReadToEnd();
                        Assert.AreEqual(msg, resp);
                    }
                });
            }
            client.SendShutdown();
        }

        private Proliferate.ProliferateClient CreateClient(string launcherName)
        {
            var pipeNamePrefix = System.Guid.NewGuid().ToString("N");
            var childProcessMainMethod = typeof(ChildProcess).GetMethod("Start");
            var exePath = ExecutableGenerator.Instance.GenerateExecutable(childProcessMainMethod,
                    ExecutableType.Default, launcherName);
            var proc = Process.Start(new ProcessStartInfo(exePath, pipeNamePrefix));
            return new Proliferate.ProliferateClient(pipeNamePrefix);
        }

        public class ChildProcess
        {
            public static void Start(string[] args)
            {
                var pipeNamePrefix = args[0];
                var handler = Proliferate.RequestHandlerFactory.FromAction(TestHandler);
                var server = new Proliferate.ProliferateServer(handler, 10, pipeNamePrefix);
                var cancelTokenSrc = new System.Threading.CancellationTokenSource();
                server.Run(cancelTokenSrc.Token);
            }
            private static void TestHandler(System.IO.StreamReader reader,
                    System.IO.StreamWriter writer)
            {
                var msg = reader.ReadToEnd();
                writer.Write(msg);
                Console.WriteLine("Child process finished handling a request.");
            }
        }
        
    }
}
