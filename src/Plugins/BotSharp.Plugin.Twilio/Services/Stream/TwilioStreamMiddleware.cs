using BotSharp.Abstraction.Realtime;
using BotSharp.Abstraction.Realtime.Models;
using BotSharp.Plugin.Twilio.Models.Stream;
using Microsoft.AspNetCore.Http;
using System.Net.WebSockets;
using Task = System.Threading.Tasks.Task;

namespace BotSharp.Plugin.Twilio.Services.Stream;

/// <summary>
/// Refrence to https://github.com/twilio-samples/speech-assistant-openai-realtime-api-node/blob/main/index.js
/// </summary>
public class TwilioStreamMiddleware
{
    private readonly RequestDelegate _next;

    public TwilioStreamMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext httpContext)
    {
        var request = httpContext.Request;

        if (request.Path.StartsWithSegments("/twilio/stream"))
        {
            if (httpContext.WebSockets.IsWebSocketRequest)
            {
                var services = httpContext.RequestServices;
                using WebSocket webSocket = await httpContext.WebSockets.AcceptWebSocketAsync();
                await HandleWebSocket(services, webSocket);
                httpContext.Abort();
            }
        }

        await _next(httpContext);
    }

    private async Task HandleWebSocket(IServiceProvider services, WebSocket webSocket)
    {
        var hub = services.GetRequiredService<IRealtimeHub>();
        var conn = new RealtimeHubConnection();

        await hub.Listen(webSocket, (receivedText) =>
        {
            var response = JsonSerializer.Deserialize<StreamEventResponse>(receivedText);
            conn.StreamId = response.StreamSid;
            conn.Event = response.Event switch
            {
                "start" => "user_connected",
                "media" => "user_data_received",
                "stop" => "user_disconnected",
                _ => response.Event
            };

            if (string.IsNullOrEmpty(conn.Event))
            {
                return conn;
            }

            conn.OnModelMessageReceived = message =>
                new
                {
                    @event = "media",
                    streamSid = response.StreamSid,
                    media = new { payload = message }
                };
            conn.OnModelAudioResponseDone = () =>
                new
                {
                    @event = "mark",
                    streamSid = response.StreamSid,
                    mark = new { name = "responsePart" }
                };
            conn.OnModelUserInterrupted = () =>
                new
                {
                    @event = "clear",
                    streamSid = response.StreamSid
                };

            if (response.Event == "start")
            {
                var startResponse = JsonSerializer.Deserialize<StreamEventStartResponse>(receivedText);
                conn.Data = JsonSerializer.Serialize(startResponse.Body.CustomParameters);
                conn.ConversationId = startResponse.Body.CallSid;
            }
            else if (response.Event == "media")
            {
                var mediaResponse = JsonSerializer.Deserialize<StreamEventMediaResponse>(receivedText);
                conn.Data = mediaResponse.Body.Payload;
            }

            return conn;
        });
    }
}
