using System.Security.Cryptography.X509Certificates;
using BTCPayServer.Lightning;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using NBitcoin;
using Lnrpc;
using Routerrpc;

namespace NostrStreamer.Services;

public class LndNode
{
    private readonly Network _network;
    private readonly ILogger<LndNode> _logger;

    public LndNode(Config config, ILogger<LndNode> logger)
    {
        _logger = logger;
        _network = Network.GetNetwork(config.Network) ?? Network.RegTest;
        var channelOptions = new GrpcChannelOptions();
        ConfigureClient(channelOptions, config.Lnd);

        var channel = GrpcChannel.ForAddress(config.Lnd.Endpoint, channelOptions);
        LightningClient = new(channel);
        InvoicesClient = new(channel);
        RouterClient = new(channel);
    }

    public Lightning.LightningClient LightningClient { get; }

    public Router.RouterClient RouterClient { get; }

    public Invoicesrpc.Invoices.InvoicesClient InvoicesClient { get; }

    public async Task<AddInvoiceResponse> AddInvoice(ulong mSats, TimeSpan? expire = null, string? memo = null,
        string? descriptionHash = null)
    {
        var req = new Invoice()
        {
            ValueMsat = (long)mSats,
            Expiry = (long)(expire ?? TimeSpan.FromHours(1)).TotalSeconds
        };

        if (!string.IsNullOrEmpty(descriptionHash))
        {
            req.DescriptionHash = ByteString.CopyFrom(Convert.FromHexString(descriptionHash));
        }
        else if (!string.IsNullOrEmpty(memo))
        {
            req.Memo = memo;
        }

        return await LightningClient.AddInvoiceAsync(req);
    }

    public async Task<Payment?> GetPayment(string paymentHash)
    {
        using var trackPayment = RouterClient.TrackPaymentV2(new()
        {
            NoInflightUpdates = true,
            PaymentHash = ByteString.CopyFrom(Convert.FromHexString(paymentHash))
        });

        if (await trackPayment.ResponseStream.MoveNext())
        {
            return trackPayment.ResponseStream.Current;
        }

        return default;
    }

    public async Task<Payment?> SendPayment(string paymentRequest, long feeLimit = 0)
    {
        using var payment = RouterClient.SendPaymentV2(new()
        {
            PaymentRequest = paymentRequest,
            TimeoutSeconds = 120,
            FeeLimitMsat = feeLimit,
            NoInflightUpdates = true
        });

        if (await payment.ResponseStream.MoveNext())
        {
            return payment.ResponseStream.Current;
        }

        return default;
    }

    public async Task<string> GenerateAddress()
    {
        var rsp = await LightningClient.NewAddressAsync(new());
        return rsp.Address;
    }

    public async Task<Invoicesrpc.AddHoldInvoiceResp?> WrapInvoice(string pr, ulong fee)
    {
        const double minExpireMins = 8;
        const long minCltvDelta = 10;
        const long maxCltvDelta = 2016;
        var now = DateTimeOffset.UtcNow;
        var decoded = BOLT11PaymentRequest.Parse(pr, _network);
        var newAmount = decoded.MinimumAmount.MilliSatoshi + (long)fee;

        if (decoded.ExpiryDate <= now)
        {
            throw new InvalidOperationException("Invoice already expired");
        }

        if ((decoded.ExpiryDate - now).TotalMinutes < minExpireMins)
        {
            throw new InvalidOperationException("Expiry too soon");
        }

        if (decoded.MinFinalCLTVExpiry < minCltvDelta)
        {
            throw new InvalidOperationException("CLTV delta too low");
        }

        if (decoded.MinFinalCLTVExpiry > maxCltvDelta)
        {
            throw new InvalidOperationException("CLTV delta too high");
        }

        var knownBits = new[] { 8, 9, 14, 15, 16, 17 };
        for (var x = 0; x < 64; x++)
        {
            var n = 1L << x;
            var fb = (FeatureBits)n;
            if (decoded.FeatureBits.HasFlag(fb) && (!knownBits.Contains(x) || !Enum.IsDefined(fb)))
            {
                throw new InvalidCastException("Unknown feature bit set");
            }
        }

        var req = new Invoicesrpc.AddHoldInvoiceRequest
        {
            CltvExpiry = (ulong)decoded.MinFinalCLTVExpiry + 18,
            Expiry = (long)(decoded.ExpiryDate - DateTimeOffset.UtcNow).TotalSeconds,
            Hash = ByteString.CopyFrom(decoded.PaymentHash!.ToBytes(false)),
            ValueMsat = newAmount
        };

        if (!string.IsNullOrEmpty(decoded.ShortDescription))
        {
            req.Memo = decoded.ShortDescription;
        }
        else if (decoded.DescriptionHash != default)
        {
            req.DescriptionHash = ByteString.CopyFrom(decoded.DescriptionHash.ToBytes(false));
        }

        return await InvoicesClient.AddHoldInvoiceAsync(req);
    }

    private void ConfigureClient(GrpcChannelOptions opt, LndConfig conf)
    {
        try
        {
            var macaroon = File.ReadAllBytes(Environment.ExpandEnvironmentVariables(conf.MacaroonPath));
            var cert = File.ReadAllBytes(Environment.ExpandEnvironmentVariables(conf.CertPath));

            var asyncInterceptor = new AsyncAuthInterceptor((_, meta) =>
            {
                meta.Add("macaroon", Convert.ToHexString(macaroon));
                return Task.CompletedTask;
            });

            var httpHandler = new HttpClientHandler();
            httpHandler.ServerCertificateCustomValidationCallback = (_, certificate2, _, _) =>
            {
                var serverCert = new X509Certificate2(cert);
                return certificate2!.Thumbprint == serverCert.Thumbprint;
            };

            opt.HttpHandler = httpHandler;
            opt.Credentials = ChannelCredentials.Create(ChannelCredentials.SecureSsl,
                CallCredentials.FromInterceptor(asyncInterceptor));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Configure failed");
        }
    }
}