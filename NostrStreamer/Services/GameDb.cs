using Igdb;
using Newtonsoft.Json;

namespace NostrStreamer.Services;

public class GameDb
{
    private readonly HttpClient _client;
    private readonly ILogger<GameDb> _logger;
    private readonly TwitchApi _config;
    private Token? _currentToken = null;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    public GameDb(HttpClient client, ILogger<GameDb> logger, Config config)
    {
        _client = client;
        _logger = logger;
        _config = config.Twitch;
    }

    private async Task RefreshToken()
    {
        bool NeedsRefresh() => _currentToken == null || _currentToken.Loaded.AddSeconds(_currentToken.ExpiresIn) < DateTime.UtcNow;
        if (NeedsRefresh())
        {
            await _tokenLock.WaitAsync();
            if (!NeedsRefresh()) return;

            try
            {
                var url =
                    $"https://id.twitch.tv/oauth2/token?client_id={_config.ClientId}&client_secret={_config.ClientSecret}&grant_type=client_credentials";

                var rsp = await _client.PostAsync(url, null);
                if (rsp.IsSuccessStatusCode)
                {
                    var newToken = JsonConvert.DeserializeObject<Token>(await rsp.Content.ReadAsStringAsync());
                    if (newToken != default)
                    {
                        _currentToken = newToken;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh token {msg}", ex.Message);
            }
            finally
            {
                _tokenLock.Release();
            }
        }
    }

    public async Task<List<Game>?> SearchGames(string s, int limit = 10)
    {
        await RefreshToken();

        var req = new HttpRequestMessage(HttpMethod.Post, "https://api.igdb.com/v4/games.pb");
        req.Headers.Add("Client-ID", _config.ClientId);
        req.Headers.Authorization = new("Bearer", _currentToken?.AccessToken);
        req.Content = new StringContent($"search \"{s}\"; fields id,cover.image_id,genres.name,name; limit {limit};");

        var rsp = await _client.SendAsync(req);
        if (rsp.IsSuccessStatusCode)
        {
            var rspStream = await rsp.Content.ReadAsStreamAsync();

            var ret = GameResult.Parser.ParseFrom(rspStream);
            return ret.Games.ToList();
        }
        else
        {
            var content = await rsp.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to fetch games {msg}", content);
        }

        return default;
    }

    class Token
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; init; } = null!;

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; init; }

        [JsonProperty("token_type")]
        public string TokenType { get; init; } = null!;

        [JsonIgnore]
        public DateTime Loaded { get; } = DateTime.UtcNow;
    }
}
