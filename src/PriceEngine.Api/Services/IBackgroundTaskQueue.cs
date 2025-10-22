using System.Threading.Channels;

namespace PriceEngine.Api.Services;

public interface IBackgroundTaskQueue
{
    ValueTask QueueAsync(Func<CancellationToken, Task> work, CancellationToken ct = default);
    ValueTask<Func<CancellationToken, Task>> DequeueAsync(CancellationToken ct);
}

public sealed class ChannelBackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<Func<CancellationToken, Task>> _channel;

    public ChannelBackgroundTaskQueue(int capacity = 100)
    {
        // Bounded queue; backpressure uygular
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };
        _channel = Channel.CreateBounded<Func<CancellationToken, Task>>(options);
    }

    public ValueTask QueueAsync(Func<CancellationToken, Task> work, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(work, ct);

    public ValueTask<Func<CancellationToken, Task>> DequeueAsync(CancellationToken ct)
        => _channel.Reader.ReadAsync(ct);
}
