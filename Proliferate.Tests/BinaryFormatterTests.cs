using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Proliferate.Tests
{
    [TestFixture]
    public class BinaryFormatterTests
    {

        [Test]
        public void TestBinaryFormatterWithSmallMessage()
        {
            TestBinaryFormatter("Hi", "Launcher1");
        }
        [Test]
        public void TestBinaryFormatterWithLargeMessage()
        {
            TestBinaryFormatter(new string('a', 1000000), "Launcher2");
        }
        /// <summary>
        /// Tests the binary formatter communication method.
        /// </summary>
        /// <param name="message">Message to transmit</param>
        /// <param name="launcherExeName">Name of EXE (necessary to allow multiple tests to run concurrently).</param>
        private void TestBinaryFormatter(string message, string launcherExeName)
        {
            var pipeNamePrefix = System.Guid.NewGuid().ToString("N");

            var childProcessMainMethod = typeof(ChildProcess).GetMethod("Start");
            var exePath = ExecutableGenerator.Instance.GenerateExecutable(childProcessMainMethod,
                ExecutableType.Default, launcherExeName);
            //The CreateNoWindow option reduces startup time of the child process.
            var startInfo = new System.Diagnostics.ProcessStartInfo(exePath, pipeNamePrefix);
            var proc = System.Diagnostics.Process.Start(startInfo);
            
            var client = new Proliferate.ProliferateClient(pipeNamePrefix);
            using (var cancelTokenSource = new System.Threading.CancellationTokenSource())
            {
                client.StartChildPinger(cancelTokenSource.Token);
                using (var messenger = client.GetMessenger<Msg, Msg>())
                {
                    var resp = messenger.SendRequest(new Msg() { Message = message });
                    Assert.IsTrue(resp.Message == message);
                }
                
            }
            client.SendShutdown();

        }

        [Serializable]
        public class Msg
        {
            public string Message;
        }

        public class ChildProcess
        {
            public static void Start(string[] args)
            {
                var pipeNamePrefix = args[0];
                var handler = Proliferate.RequestHandlerFactory.FromTaskReturning<Msg, Msg>(TestHandler);
                var server = new Proliferate.ProliferateServer(handler, 1, pipeNamePrefix);
                var cancelTokenSrc = new System.Threading.CancellationTokenSource();
                server.Run(cancelTokenSrc.Token);
            }
            private static async Task<Msg> TestHandler(Msg requestMsg)
            {
                Console.WriteLine("Child process is handling a request on thread "
                        + System.Threading.Thread.CurrentThread.ManagedThreadId.ToString());
                await Task.Delay(1);
                Console.WriteLine("Child process finished handling a request.");
                return requestMsg;
            }
        }
        
    }
}
