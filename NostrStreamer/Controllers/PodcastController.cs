using System.Xml.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nostr.Client.Keys;
using NostrStreamer.Database;

namespace NostrStreamer.Controllers;

[Route("/api/podcast")]
public class PodcastController(StreamerContext db, Config config) : Controller
{
    [HttpGet("{id:guid}.xml")]
    public async Task GetFeed([FromRoute] Guid id)
    {
        var stream = await db.Streams
            .AsNoTracking()
            .Include(a => a.User)
            .Where(a => a.Id == id)
            .SingleOrDefaultAsync();

        if (stream == default)
        {
            Response.StatusCode = 404;
            return;
        }

        var pod = new PodcastRoot();
        var link = stream.ToIdentifier();
        pod.LiveItem = new()
        {
            Guid = stream.Id,
            Title = stream.Title ?? "",
            Description = stream.Summary,
            Status = stream.State.ToString().ToLower(),
            Start = stream.Starts,
            End = stream.Ends ?? new DateTime(),
            ContentLink = new()
            {
                Href = $"https://zap.stream/{link.ToBech32()}",
                Text = "Watch live on zap.stream!"
            },
            Enclosure = new()
            {
                Url = new Uri(config.DataHost, $"stream/{stream.Id}.m3u8").ToString(),
                Type = "application/x-mpegurl",
                Length = 0
            }
        };
        pod.SocialInteract = new()
        {
            Url = $"nostr:{link.ToBech32()}",
            Protocol = "nostr",
            AccountId = NostrPublicKey.FromHex(stream.PubKey).Bech32
        };

        Response.ContentType = "text/xml";
        var ns = new XmlSerializerNamespaces();
        ns.Add("podcast", "https://podcastindex.org/namespace/1.0");

        var ser = new XmlSerializer(typeof(PodcastRoot));
        using var ms = new MemoryStream();
        ser.Serialize(ms, pod, ns);
        ms.Seek(0, SeekOrigin.Begin);
        await ms.CopyToAsync(Response.Body);
    }
}

[XmlRoot(ElementName = "enclosure")]
public class Enclosure
{
    [XmlAttribute(AttributeName = "url")]
    public string Url { get; set; }

    [XmlAttribute(AttributeName = "type")]
    public string Type { get; set; }

    [XmlAttribute(AttributeName = "length")]
    public int Length { get; set; }
}

[XmlRoot(ElementName = "socialInteract")]
public class SocialInteract
{
    [XmlAttribute(AttributeName = "url")]
    public string Url { get; set; }

    [XmlAttribute(AttributeName = "protocol")]
    public string Protocol { get; set; } = "nostr";

    [XmlAttribute(AttributeName = "accountId")]
    public string AccountId { get; set; }
}

[XmlRoot(ElementName = "contentLink")]
public class ContentLink
{
    [XmlAttribute(AttributeName = "href")]
    public string Href { get; set; }

    [XmlText]
    public string Text { get; set; }
}

[XmlRoot(ElementName = "liveItem", Namespace = "https://podcastindex.org/namespace/1.0")]
public class LiveItem
{
    [XmlElement(ElementName = "title", Namespace = "")]
    public string Title { get; set; } = null!;

    [XmlElement(ElementName = "description", Namespace = "")]
    public string? Description { get; set; }
    
    [XmlElement(ElementName = "guid", Namespace = "")]
    public Guid Guid { get; set; }

    [XmlElement(ElementName = "enclosure", Namespace = "")]
    public Enclosure Enclosure { get; set; } = null!;

    [XmlElement(ElementName = "contentLink", Namespace = "https://podcastindex.org/namespace/1.0")]
    public ContentLink ContentLink { get; set; } = null!;

    [XmlAttribute(AttributeName = "status")]
    public string Status { get; set; } = "live";

    [XmlAttribute(AttributeName = "start")]
    public DateTime Start { get; set; }

    [XmlAttribute(AttributeName = "end")]
    public DateTime End { get; set; }
}

[XmlRoot(ElementName = "rss")]
public class PodcastRoot
{
    [XmlElement(ElementName = "liveItem", Namespace = "https://podcastindex.org/namespace/1.0")]
    public LiveItem? LiveItem { get; set; }
    
    [XmlElement(ElementName = "socialInteract", Namespace = "https://podcastindex.org/namespace/1.0")]
    public SocialInteract? SocialInteract { get; set; }
}
