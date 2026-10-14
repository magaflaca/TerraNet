namespace TerraNet.Protocol;

public static class StreamPipe
{
    private const int BufferSize = 16 * 1024;

    public static async Task RunAsync(Stream first, Stream second, CancellationToken cancellation = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        var forward = CopyAsync(first, second, linked.Token);
        var backward = CopyAsync(second, first, linked.Token);
        await Task.WhenAny(forward, backward);
        await linked.CancelAsync();
        await Task.WhenAll(Quiet(forward), Quiet(backward));
    }

    private static async Task CopyAsync(Stream source, Stream destination, CancellationToken cancellation)
    {
        var buffer = new byte[BufferSize];
        int read;
        while ((read = await source.ReadAsync(buffer, cancellation)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellation);
            await destination.FlushAsync(cancellation);
        }
    }

    private static async Task Quiet(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
        }
    }
}
