using System.Text.Json.Serialization;

namespace BotSharp.Plugin.Twilio.Models.Stream;

public class StreamEventResponse
{
    /// <summary>
    /// connected, start, media, stop
    /// </summary>
    [JsonPropertyName("event")]
    public string Event { get; set; }
}
