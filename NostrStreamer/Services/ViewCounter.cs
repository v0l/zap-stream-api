using System.Collections.Concurrent;

namespace NostrStreamer.Services;

public class ViewCounter
{
    private readonly ConcurrentDictionary<string, Dictionary<string, DateTime>> _sessions = new();

    public void Activity(string key, string token)
    {
        if (!_sessions.ContainsKey(key))
        {
            _sessions.TryAdd(key, new());
        }
        if (_sessions.TryGetValue(key, out var x))
        {
            x[token] = DateTime.Now;
        }
    }

    public void Decay()
    {
        foreach (var k in _sessions.Keys)
        {
            if (_sessions.TryGetValue(k, out var x))
            {
                _sessions[k] = x
                    .Where(a => a.Value > DateTime.Now.Subtract(TimeSpan.FromMinutes(2)))
                    .ToDictionary(a => a.Key, b => b.Value);

                if (_sessions[k].Count == 0)
                {
                    _sessions.TryRemove(k, out _);
                }
            }
        }
    }

    public int Current(string key)
    {
        if (_sessions.TryGetValue(key, out var x))
        {
            return x.Count;
        }

        return 0;
    }
}

public class ViewCounterDecay : BackgroundService
{
    private readonly ViewCounter _viewCounter;
    private readonly ILogger<ViewCounterDecay> _logger;

    public ViewCounterDecay(ViewCounter viewCounter, ILogger<ViewCounterDecay> logger)
    {
        _viewCounter = viewCounter;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _viewCounter.Decay();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Something went wrong");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
