using System.Security.Cryptography;
using System.Text;
using BTCPayServer.Lightning;
using LNURL;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Nostr.Client.Json;
using Nostr.Client.Messages;
using Nostr.Client.Utils;
using NostrStreamer.Database;
using NostrStreamer.Services;

namespace NostrStreamer.Controllers;

[Route("/api/pay")]
[EnableCors]
public class LnurlController : Controller
{
    private readonly Config _config;
    private readonly UserService _userService;

    public LnurlController(Config config, UserService userService)
    {
        _config = config;
        _userService = userService;
    }

    [HttpGet("/.well-known/lnurlp/{key}")]
    public async Task<IActionResult> GetPayService([FromRoute] string key)
    {
        var user = await _userService.GetUser(key);
        if (user == default) return LnurlError("User not found");

        var metadata = GetMetadata(user);
        var pubKey = _config.GetPubKey();
        return Json(new LNURLPayRequest
        {
            Callback = new Uri(_config.ApiHost, $"/api/pay/{key}"),
            Metadata = JsonConvert.SerializeObject(metadata),
            MinSendable = LightMoney.Satoshis(1),
            MaxSendable = LightMoney.Coins(1),
            Tag = "payRequest",
            NostrPubkey = pubKey,
            AllowsNostr = true,
        });
    }

    [HttpGet("{key}")]
    public async Task<IActionResult> PayUserBalance([FromRoute] string key, [FromQuery] ulong amount,
        [FromQuery] string? nostr)
    {
        try
        {
            var user = await _userService.GetUser(key);
            if (user == default) return LnurlError("User not found");

            string? descHash;
            if (!string.IsNullOrEmpty(nostr))
            {
                var ev = JsonConvert.DeserializeObject<NostrEvent>(nostr, NostrSerializer.Settings);
                var amountTag = ev?.Tags?.FindFirstTagValue("amount");
                if (ev?.Kind != NostrKind.ZapRequest ||
                    (amountTag != default && amountTag != amount.ToString()) ||
                    !ev.IsSignatureValid())
                {
                    throw new Exception("Invalid nostr event");
                }

                descHash = SHA256.HashData(Encoding.UTF8.GetBytes(nostr)).ToHex();
            }
            else
            {
                var metadata = GetMetadata(user);
                descHash = SHA256.HashData(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(metadata))).ToHex();
            }

            var invoice = await _userService.CreateTopup(key, amount, descHash, nostr);
            return Json(new LNURLPayRequest.LNURLPayRequestCallbackResponse
            {
                Pr = invoice
            });
        }
        catch (Exception ex)
        {
            return LnurlError($"Failed to create invoice (${ex.Message})");
        }
    }

    private List<KeyValuePair<string, string>> GetMetadata(User u)
    {
        return new List<KeyValuePair<string, string>>()
        {
            new("text/plain", $"Topup for {u.PubKey}")
        };
    }

    private IActionResult LnurlError(string reason)
    {
        return Json(new LNUrlStatusResponse()
        {
            Reason = reason,
            Status = "ERROR"
        });
    }
}