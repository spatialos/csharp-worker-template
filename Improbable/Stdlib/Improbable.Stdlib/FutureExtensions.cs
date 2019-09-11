using System.Threading;
using System.Threading.Tasks;
using Improbable.Worker.CInterop;

namespace Improbable.Stdlib
{
    public static class FutureExtensions
    {
        public static Task<T> ToTask<T>(this Future<T> future, CancellationToken cancellation = default)
        {
            var tcs = new TaskCompletionSource<T>();
            cancellation.Register(() => tcs.TrySetCanceled(cancellation));

            Task.Run(() =>
            {
                while (true)
                {
                    cancellation.ThrowIfCancellationRequested();

                    if (future.TryGet(out var value, 50))
                    {
                        tcs.TrySetResult(value);
                        break;
                    }
                }
            }, cancellation);

            return tcs.Task;
        }
    }
}
