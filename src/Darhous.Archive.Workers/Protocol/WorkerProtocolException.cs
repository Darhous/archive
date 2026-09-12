namespace Darhous.Archive.Workers.Protocol;

public class WorkerProtocolException : IOException
{
    public WorkerProtocolException(string message)
        : base(message)
    {
    }

    public WorkerProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class WorkerHandshakeException : WorkerProtocolException
{
    public WorkerHandshakeException(string message)
        : base(message)
    {
    }

    public WorkerHandshakeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
