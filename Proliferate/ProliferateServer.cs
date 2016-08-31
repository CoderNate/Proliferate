using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Proliferate
{
    using StreamHandlerFunc = Func<PipeReadWrapper, PipeWriteWrapper, Task>;
    
    /// <summary>
    /// Server used by the child process to handle requests from the parent process.
    /// </summary>
    public class ProliferateServer
    {
        private readonly StreamHandlerFunc _messageHandler;
        private readonly int _maxConcurrentConnections;
        private readonly string _pipeNamePrefix;
        private readonly TaskScheduler _handlerTaskScheduler;
        public ProliferateServer(RequestHandler requestHandler,
                int maxConcurrentConnections, string pipeNamePrefix, TaskScheduler handlerTaskScheduler = null)
        {
            _messageHandler = requestHandler.StreamHandlerFunc;
            _maxConcurrentConnections = maxConcurrentConnections;
            _pipeNamePrefix = pipeNamePrefix;
            _handlerTaskScheduler = handlerTaskScheduler ?? TaskScheduler.Default;
        }

        /// <summary>
        /// Starts the server and returns a task that resolves once the server receives a shutdown request or stops receiving pings.
        /// </summary>
        public Task RunAsync(CancellationToken cancellationToken)
        {
            return ProduceConnectionHandlers(cancellationToken, _maxConcurrentConnections,
                    _pipeNamePrefix, _messageHandler, _handlerTaskScheduler);
        }

        /// <summary>
        /// Starts the server and blocks until a shutdown request is sent or the server stops receiving pings.
        /// </summary>
        public void Run(CancellationToken cancellationToken)
        {
            Internal.AsyncPump.Run(() => RunAsync(cancellationToken));
        }

        private static async Task ProduceConnectionHandlers(
                CancellationToken cancellationToken,
                int maxConcurrentConnections,
                string pipeNamePrefix,
                StreamHandlerFunc messageHandler,
                TaskScheduler handlerTaskScheduler)
        {
            //Make the connectionLostInterval slightly longer than the interval the server uses for sending pings.
            const int connectionLostInterval = Constants.PingIntervalMilliseconds + 100;
            var establishConnectionTasks = new List<Task>();
            var handleRequestTasks = new List<Task<ConnectionHandlerResult>>();
            Task pingHandlerTask = null;
            var receivedPing = true;
            var delayTask = Task.Delay(connectionLostInterval);
            while (!cancellationToken.IsCancellationRequested)
            {
                //When there are no remaining establishConnectionTasks it means there are no free handlers waiting for
                //a connection from the parent process, so start a new handler unless we've reached
                //the maximum number of concurrent connections.
                while (establishConnectionTasks.Count == 0 && handleRequestTasks.Count < maxConcurrentConnections)
                {
                    var tcs = new TaskCompletionSource<bool>();
                    //This is the same as calling Task.Run except that we're specifying the TaskScheduler.
                    var handleRequestTask = Task.Factory.StartNew(
                        () => ConnectionHandler(tcs, messageHandler, maxConcurrentConnections, pipeNamePrefix),
                        CancellationToken.None,
                        TaskCreationOptions.DenyChildAttach,
                        handlerTaskScheduler
                        ).Unwrap();
                    handleRequestTasks.Add(handleRequestTask);
                    establishConnectionTasks.Add(tcs.Task);
                }
                var allTasks = establishConnectionTasks.Concat(handleRequestTasks).ToList();
                if (pingHandlerTask == null)
                {
                    pingHandlerTask = Task.Run(() => PingConnectionHandler(pipeNamePrefix));
                }
                allTasks.Add(pingHandlerTask);
                if (!allTasks.Contains(delayTask))
                    allTasks.Add(delayTask);
                var finishedTask = await Task.WhenAny(allTasks);
                if (finishedTask == delayTask)
                {
                    if (!receivedPing)
                        break;
                    allTasks.Remove(delayTask);
                    receivedPing = false;
                    delayTask = Task.Delay(connectionLostInterval);
                }
                else if (RemoveFromListIfPresent(establishConnectionTasks, finishedTask))
                { }
                else if (finishedTask == pingHandlerTask)
                {
                    receivedPing = true;
                    pingHandlerTask = null;
                }
                else if (finishedTask is Task<ConnectionHandlerResult>)
                {
                    var handlerTask = finishedTask as Task<ConnectionHandlerResult>;
                    if (handlerTask.Result == ConnectionHandlerResult.Shutdown)
                        break;
                    handleRequestTasks.Remove(handlerTask);
                }
                else { throw new Exception("Unknown task."); }
            }
        }
        private static bool RemoveFromListIfPresent<T>(IList<T> list, T item)
        {
            var doesContain = list.Contains(item);
            if (doesContain)
                list.Remove(item);
            return doesContain;
        }

        private static void PingConnectionHandler(string pipeNamePrefix)
        {
            using (var incomingRequestPipe = new NamedPipeServerStream(
                pipeNamePrefix + Constants.PingPipeNameSuffix, PipeDirection.In))
            {
                incomingRequestPipe.WaitForConnection();
                //No need to read anything from the pipe. The fact that the connection was made is all we need.
            }
        }

        private enum ConnectionHandlerResult { Normal, Shutdown }

        private static async Task<ConnectionHandlerResult> ConnectionHandler(TaskCompletionSource<bool> tcs,
                StreamHandlerFunc messageHandler, int maxConcurrentConnections, string pipeNamePrefix)
        {
            using (var incomingRequestPipe = new NamedPipeServerStream(
                pipeNamePrefix + "ParentToChild", PipeDirection.InOut, maxConcurrentConnections))
            {
                incomingRequestPipe.WaitForConnection();
                tcs.SetResult(true); //Use the TaskCompletionSource to signal back that the pipe received a connection.
                var readWrapper = new PipeReadWrapper(incomingRequestPipe);
                if (readWrapper.CheckRemainingByteChunkSize() == Constants.ShutdownId)
                {
                    return ConnectionHandlerResult.Shutdown;
                }
                await messageHandler(readWrapper,
                    new PipeWriteWrapper(incomingRequestPipe));
            }
            return ConnectionHandlerResult.Normal;
        }

    }
}
