namespace WiFiBroadcastNet;

public class UserStream
{
    public required byte StreamId { get; init; }

    public required bool IsFecEnabled { get; init; }

    public required IStreamAccessor StreamAccessor { get; init; }
}