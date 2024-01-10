using System.Net.Http.Formatting;

namespace NostrStreamer.Services;

public class DiscordWebhook
{
    private readonly HttpClient _client;

    public DiscordWebhook(HttpClient client)
    {
        _client = client;
    }

    public async Task SendMessage(Uri webhook, string msg)
    {
        await _client.PostAsync(webhook, new
        {
            content = msg
        }, new JsonMediaTypeFormatter());
    }
}
