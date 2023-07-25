namespace NostrStreamer.Services.StreamManager;

public class LowBalanceException : Exception
{
    public LowBalanceException(string message) : base(message)
    {
    }
}
