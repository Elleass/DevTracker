using System.Threading.Channels;
using DevTracker.Application.Inerfaces;
using DevTracker.Application.Models;

namespace DevTracker.Infrastructure.Background;

public sealed class WorkSessionQueue : IWorkSessionEvents
{
    private readonly Channel<WorkSessionCompleted> _channel = Channel.CreateBounded<WorkSessionCompleted>(new BoundedChannelOptions(256)
    { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    public ChannelReader<WorkSessionCompleted> Reader => _channel.Reader;
    public bool Publish(WorkSessionCompleted completed) => _channel.Writer.TryWrite(completed);
}
