using Newtonsoft.Json;
using Nostr.Client.Client;
using Nostr.Client.Json;
using Nostr.Client.Messages;
using Nostr.Client.Requests;

namespace NostrStreamer.Services;

public class ZapService
{
    private readonly ILogger<ZapService> _logger;
    private readonly Config _config;
    private readonly INostrClient _nostrClient;

    public ZapService(ILogger<ZapService> logger, Config config, INostrClient nostrClient)
    {
        _logger = logger;
        _config = config;
        _nostrClient = nostrClient;
    }

    public void HandlePaid(string pr, string? zapRequest)
    {
        try
        {
            if (string.IsNullOrEmpty(zapRequest)) return;

            var zapNote = JsonConvert.DeserializeObject<NostrEvent>(zapRequest, NostrSerializer.Settings);
            if (zapNote == default)
            {
                _logger.LogWarning("Could not parse zap note {note}", zapRequest);
                return;
            }

            var key = _config.GetPrivateKey();
            var pubKey = key.DerivePublicKey().Hex;
            var tags = zapNote.Tags!.Where(a => a.TagIdentifier.Length == 1).ToList();
            tags.Add(new("bolt11", pr));
            tags.Add(new("description", zapRequest));
            tags.Add(new("P", zapNote.Pubkey!));

            var zapReceipt = new NostrEvent()
            {
                Kind = NostrKind.Zap,
                CreatedAt = DateTime.UtcNow,
                Pubkey = pubKey,
                Content = zapNote.Content,
                Tags = new(tags.ToArray())
            };

            var zapReceiptSigned = zapReceipt.Sign(key);

            var jsonZap = JsonConvert.SerializeObject(zapReceiptSigned, NostrSerializer.Settings);
            _logger.LogInformation("Created tip receipt {json}", jsonZap);
            _nostrClient.Send(new NostrEventRequest(zapReceiptSigned));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to handle zap");
        }
    }
}
