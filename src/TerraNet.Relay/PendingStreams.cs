using System.Collections.Concurrent;
using TerraNet.Protocol;

namespace TerraNet.Relay;

public sealed class PendingStreams
{
    private readonly ConcurrentDictionary<StreamToken, TaskCompletionSource<Stream>> _pending = new();

    public async Task<Stream?> WaitAsync(StreamToken token, TimeSpan timeout, CancellationToken cancellation)
    {
        var completion = new TaskCompletionSource<Stream>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[token] = completion;
        try
        {
            return await completion.Task.WaitAsync(timeout, cancellation);
        }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException)
        {
            return null;
        }
        finally
        {
            _pending.TryRemove(token, out _);
        }
    }

    public bool Complete(StreamToken token, Stream stream) =>
        _pending.TryRemove(token, out var completion) && completion.TrySetResult(stream);
}
