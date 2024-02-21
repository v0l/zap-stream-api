namespace NostrStreamer.ApiModel;

public class BalanceUpdateException(string? msg, Exception? inner) : Exception(msg, inner);