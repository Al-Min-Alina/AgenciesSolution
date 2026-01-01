using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Agencies.Client.Helpers
{
    public static class AsyncHelper
    {
        public static DispatcherAwaiter Dispatcher(this Dispatcher dispatcher)
        {
            return new DispatcherAwaiter(dispatcher);
        }

        public static Task RunInBackground(Action action, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new TaskCanceledException();

                action();
            }, cancellationToken);
        }

        public static Task<T> RunInBackground<T>(Func<T> func, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new TaskCanceledException();

                return func();
            }, cancellationToken);
        }

        public static async Task<T> WithTimeout<T>(this Task<T> task, TimeSpan timeout)
        {
            var delayTask = Task.Delay(timeout);
            var completedTask = await Task.WhenAny(task, delayTask);

            if (completedTask == delayTask)
            {
                throw new TimeoutException($"Operation timed out after {timeout.TotalSeconds} seconds");
            }

            return await task;
        }

        public static async Task<T> RetryOnException<T>(Func<Task<T>> action, int retryCount = 3, TimeSpan? delay = null)
        {
            var exceptions = new System.Collections.Generic.List<Exception>();

            for (int retry = 0; retry <= retryCount; retry++)
            {
                try
                {
                    return await action();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);

                    if (retry == retryCount)
                        break;

                    if (delay.HasValue)
                        await Task.Delay(delay.Value);
                }
            }

            throw new AggregateException("Failed after all retries", exceptions);
        }
    }

    public struct DispatcherAwaiter : System.Runtime.CompilerServices.INotifyCompletion
    {
        private readonly Dispatcher _dispatcher;

        public DispatcherAwaiter(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public bool IsCompleted => _dispatcher.CheckAccess();

        public void OnCompleted(Action continuation)
        {
            _dispatcher.BeginInvoke(continuation);
        }

        public void GetResult() { }

        public DispatcherAwaiter GetAwaiter() => this;
    }
}