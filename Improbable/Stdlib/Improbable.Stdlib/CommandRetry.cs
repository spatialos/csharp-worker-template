using System;
using System.Threading;
using System.Threading.Tasks;
using Improbable.Worker.CInterop;

namespace Improbable.Stdlib
{
    public static class CommandRetry
    {
        public static Task<TResult> Retry<TResult>(Func<Task<TResult>> action, int maxRetries, TimeSpan delay, CancellationToken token = default)
        {
            return Task.Run(async () =>
            {
                var retriesLeft = maxRetries;

                while (retriesLeft > 0 && !token.IsCancellationRequested)
                {
                    try
                    {
                        var result = await action();
                        return result;
                    }
                    catch (CommandFailedException e)
                    {
                        if (e.Code == StatusCode.AuthorityLost)
                        {
                            retriesLeft--;

                            if (retriesLeft > 0)
                            {
                                await Task.Delay(delay, token);
                            }
                        }
                        else
                        {
                            throw;
                        }
                    }
                }

                throw new CommandFailedException(StatusCode.Timeout, $"Giving up after {maxRetries} retries");
            }, token);
        }
    }
}
