# Proliferate
Proliferate dynamically generates executable launcher files that are used to start child process and provides a means to communicate with the processes over named pipes. This way the code that runs in your main process can live in the same assembly as the code that runs in the child process (no need to add another project to your solution for the child EXE).
Available on [NuGet](http://nuget.org/packages/Proliferate)
### Features:
* Uses Reflection.Emit to dynamically generate a tiny executable file for launching a child process (don't worry this only takes ~20ms).
* Using raw Named Pipes means a little bit faster startup time than using WCF (Windows Communication Foundation).
* Child process shuts down when signaled to do so by parent or when it stops receiving pings.
* Provides ability to directly write to/read from a stream or to send strings.
* Allows for sending serializable objects (using BinaryFormatter internally).
* Supports multiple simultaneous connections.
* Can optionally use Async/Await style asynchronous code in the request handler of the child process.
* No dependency on System.ServiceModel.

### Limitations
* Cannot be ported to .NET Core because saving dynamic assemblies isn't currently supported: https://github.com/dotnet/corefx/issues/4491
* Errors in the child process are not propagated to the parent process (the way they would be in WCF when includeExceptionDetailInFaults is enabled).
* BinaryFormatter isn't as good as [Wire](https://github.com/akkadotnet/Wire) or [MS Bond](https://github.com/Microsoft/bond), but using it doesn't require any extra library dependencies.

### Serialized Message Example
Also refer to the Proliferate.Example project.
```csharp
    class Program
    {
        static void Main(string[] args)
        {
            var childProcessMainMethod = typeof(ChildProcess).GetMethod("Start");
            var exePath = ExecutableGenerator.Instance.GenerateExecutable(childProcessMainMethod, 
                ExecutableType.Default, "Launcher");
            var proc = System.Diagnostics.Process.Start(exePath, "MyPipeNameHere");
            
            var client = new Proliferate.ProliferateClient("MyPipeNameHere");
            using (var cancelTokenSource = new System.Threading.CancellationTokenSource())
            {
                using (var messenger = client.GetMessenger<DataTransferObj, DataTransferObj>())
                {
                    var response = messenger.SendRequest(new DataTransferObj() { Message = "Hi" });
                }
            }
            client.SendShutdown();
        }
    }

    [Serializable]
    public class DataTransferObj
    {
        public string Message;
    }

    public class ChildProcess
    {
        public static void Start(string[] args)
        {
            var pipeNamePrefix = args[0];
            var handler = Proliferate.RequestHandlerFactory.
                FromTaskReturning<DataTransferObj, DataTransferObj>(TestHandler);
            var server = new Proliferate.ProliferateServer(handler, 1, pipeNamePrefix);
            var cancelTokenSrc = new System.Threading.CancellationTokenSource();
            server.Run(cancelTokenSrc.Token);
        }
        private static async Task<DataTransferObj> TestHandler(DataTransferObj requestMsg)
        {
            await Task.Delay(1); //Simulate some task
            Console.WriteLine("Child process finished handling a request.");
            return requestMsg;
        }
    }
```