using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Nostr.Client.Json;
using Nostr.Client.Messages;
using NostrStreamer.Database;

namespace NostrStreamer;

public static class NostrAuth
{
    public const string Scheme = "Nostr";
    public const string RoleAdmin = "admin";
}

public class NostrAuthOptions : AuthenticationSchemeOptions
{
}

public class NostrAuthHandler : AuthenticationHandler<NostrAuthOptions>
{
    private readonly StreamerContext _db;
    public NostrAuthHandler(IOptionsMonitor<NostrAuthOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock,
        StreamerContext db)
        : base(options, logger, encoder, clock)
    {
        _db = db;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var auth = Request.Headers.Authorization.FirstOrDefault()?.Trim();
        if (string.IsNullOrEmpty(auth))
        {
            return AuthenticateResult.Fail("Missing Authorization header");
        }

        if (!auth.StartsWith(NostrAuth.Scheme))
        {
            return AuthenticateResult.Fail("Invalid auth scheme");
        }

        var token = auth[6..];
        var bToken = Convert.FromBase64String(token);
        if (string.IsNullOrEmpty(token) || bToken.Length == 0 || bToken[0] != '{')
        {
            return AuthenticateResult.Fail("Invalid token");
        }

        var ev = JsonConvert.DeserializeObject<NostrEvent>(Encoding.UTF8.GetString(bToken), NostrSerializer.Settings);
        if (ev == default)
        {
            return AuthenticateResult.Fail("Invalid nostr event");
        }

        if (!ev.IsSignatureValid())
        {
            return AuthenticateResult.Fail("Invalid nostr event, invalid sig");
        }

        if (ev.Kind != (NostrKind)27_235)
        {
            return AuthenticateResult.Fail("Invalid nostr event, wrong kind");
        }

        var diffTime = Math.Abs((ev.CreatedAt!.Value - DateTime.UtcNow).TotalSeconds);
        if (diffTime > 120d)
        {
            return AuthenticateResult.Fail("Invalid nostr event, timestamp out of range");
        }

        var urlTag = ev.Tags!.FirstOrDefault(a => a.TagIdentifier == "u");
        var methodTag = ev.Tags!.FirstOrDefault(a => a.TagIdentifier == "method");
        if (string.IsNullOrEmpty(urlTag?.AdditionalData[0]) ||
            !new Uri(urlTag.AdditionalData[0]).AbsolutePath.Equals(Request.Path, StringComparison.InvariantCultureIgnoreCase))
        {
            return AuthenticateResult.Fail("Invalid nostr event, url tag invalid");
        }

        if (string.IsNullOrEmpty(methodTag?.AdditionalData[0]) ||
            !methodTag.AdditionalData[0].Equals(Request.Method, StringComparison.InvariantCultureIgnoreCase))
        {
            return AuthenticateResult.Fail("Invalid nostr event, method tag invalid");
        }

        var principal = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, ev.Pubkey!)
        });

        var user = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.PubKey == ev.Pubkey!);

        if (user is {IsAdmin: true})
        {
            principal.AddClaim(new Claim(ClaimTypes.Role, NostrAuth.RoleAdmin));
        }

        return AuthenticateResult.Success(new(new ClaimsPrincipal(new[] {principal}), Scheme.Name));
    }
}
