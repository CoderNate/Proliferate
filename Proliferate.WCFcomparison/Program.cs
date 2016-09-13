using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Proliferate.Example
{

    class Program
    {
        static void Main(string[] args)
        {
            //This example only references the Proliferate library to use the ExecutableGenerator.
            //Communication is done through WCF.
            
            var childProcessMainMethod = typeof(ChildProcess).GetMethod("Start");
            var exePath = ExecutableGenerator.Instance.GenerateExecutable(childProcessMainMethod,
                ExecutableType.Default, "WCFLauncher");
            var startInfo = new System.Diagnostics.ProcessStartInfo(exePath)
            { CreateNoWindow = true, UseShellExecute = false };
            var w = System.Diagnostics.Stopwatch.StartNew();
            var proc = System.Diagnostics.Process.Start(startInfo);

            ISomeService pipeProxy = null;
            ChannelFactory<ISomeService> pipeFactory =
              new ChannelFactory<ISomeService>(
                new NetNamedPipeBinding() { OpenTimeout = TimeSpan.FromSeconds(3), ReceiveTimeout = TimeSpan.FromSeconds(3) },
                new EndpointAddress("net.pipe://localhost/SomeService"));
            //Don't know a better way to wait for the other end to be ready before calling CreateChannel().
            //So just catch the EndpointNotFoundException and keep retrying.
            while (true)
            {
                try
                {
                    pipeProxy = pipeFactory.CreateChannel();
                    pipeProxy.DoSomethingWithAstring("Hello");
                    break;
                }
                catch (EndpointNotFoundException)
                {}
            }
            Console.WriteLine("Time from process start until response received: " + w.Elapsed.ToString());
            Console.ReadKey();
            pipeProxy.Close();
            
        }
    }


    [ServiceContract]
    public interface ISomeService
    {
        [OperationContract]
        string DoSomethingWithAstring(string value);

        [OperationContract]
        void Close();
    }
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class SomeService : ISomeService
    {
        public event EventHandler RequestClose;
        
        public string DoSomethingWithAstring(string value)
        {
            return value;
        }
        public void Close()
        {
            if (RequestClose != null)
                RequestClose(this, new EventArgs());
        }
    }

    
    public class ChildProcess
    {
        /// <summary>
        /// Hosts the service until <see cref="ISomeService.Close"/> is called.
        /// </summary>
        public static void Start(string[] args)
        {
            var svc = new SomeService();
            using (var host = new ServiceHost(svc, new[] { new Uri("net.pipe://localhost") }))
            {
                host.AddServiceEndpoint(typeof(ISomeService), new NetNamedPipeBinding(), "SomeService");

                host.Open();
                var requestCloseEvt = new System.Threading.ManualResetEvent(false);
                svc.RequestClose += (sender, e) =>
                {
                    requestCloseEvt.Set();
                };
                Console.WriteLine("Service is available.");
                requestCloseEvt.WaitOne();
                
                host.Close();
            }
        }
        
    }
}
