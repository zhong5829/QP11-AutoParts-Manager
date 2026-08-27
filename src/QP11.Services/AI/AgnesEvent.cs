namespace QP11.Services.AI;

public abstract record AgnesEvent
{
    public sealed record TextDelta(string Text) : AgnesEvent;

    public sealed record ToolCalling(string Name, string ArgsJson) : AgnesEvent;

    public sealed record ToolResult(string Name, string Result, bool Success) : AgnesEvent;

    public sealed record Finished() : AgnesEvent;

    public sealed record Error(string Message) : AgnesEvent;
}
