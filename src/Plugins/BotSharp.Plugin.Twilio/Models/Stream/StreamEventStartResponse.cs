using System.Text.Json.Serialization;

namespace BotSharp.Plugin.Twilio.Models.Stream;

public class StreamEventStartResponse : StreamEventResponse
{
    [JsonPropertyName("sequenceNumber")]
    public string SequenceNumber { get; set; }

    [JsonPropertyName("streamSid")]
    public string StreamSid { get; set; }

    [JsonPropertyName("start")]
    public StreamEventStartBody Body { get; set; }
}

public class StreamEventStartBody
{
    [JsonPropertyName("accountSid")]
    public string AccountSid { get; set; }

    [JsonPropertyName("callSid")]
    public string CallSid { get; set; }

    [JsonPropertyName("tracks")]
    public string[] Tracks { get; set; }

    [JsonPropertyName("customParameters")]
    public JsonDocument CustomParameters { get; set; }
}
