using BotSharp.Abstraction.Files;
using BotSharp.Abstraction.Infrastructures.Enums;
using BotSharp.Abstraction.Options;
using BotSharp.Abstraction.Routing;
using BotSharp.Core.Infrastructures;
using BotSharp.Plugin.Twilio.Interfaces;
using BotSharp.Plugin.Twilio.Models;
using BotSharp.Plugin.Twilio.OutboundPhoneCallHandler.LlmContexts;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace BotSharp.Plugin.Twilio.OutboundPhoneCallHandler.Functions
{
    public class HandleOutboundPhoneCallFn : IFunctionCallback
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<HandleOutboundPhoneCallFn> _logger;
        private readonly BotSharpOptions _options;
        private readonly TwilioSetting _twilioSetting;

        public string Name => "util-twilio-twilio_outbound_phone_call";
        public string Indication => "Dialing the number";

        public HandleOutboundPhoneCallFn(
            IServiceProvider services,
            ILogger<HandleOutboundPhoneCallFn> logger,
            BotSharpOptions options,
            TwilioSetting twilioSetting)
        {
            _services = services;
            _logger = logger;
            _options = options;
            _twilioSetting = twilioSetting;
        }

        public async Task<bool> Execute(RoleDialogModel message)
        {
            var args = JsonSerializer.Deserialize<LlmContextIn>(message.FunctionArgs, _options.JsonSerializerOptions);
            if (args.PhoneNumber.Length != 12 || !args.PhoneNumber.StartsWith("+1", StringComparison.OrdinalIgnoreCase))
            {
                var error = $"Invalid phone number format: {args.PhoneNumber}";
                _logger.LogError(error);
                message.Content = error;
                return false;
            }

            if (string.IsNullOrWhiteSpace(args.InitialMessage))
            {
                _logger.LogError("Initial message is empty.");
                message.Content = "There is an error when generating phone message.";
                return false;
            }

            var convService = _services.GetRequiredService<IConversationService>();
            var convStorage = _services.GetRequiredService<IConversationStorage>();
            var routing = _services.GetRequiredService<IRoutingContext>();
            var fileStorage = _services.GetRequiredService<IFileStorageService>();
            var sessionManager = _services.GetRequiredService<ITwilioSessionManager>();
            var states = _services.GetRequiredService<IConversationStateService>();

            // Fork conversation
            var entryAgentId = routing.EntryAgentId;
            var newConv = await convService.NewConversation(new Abstraction.Conversations.Models.Conversation
            {
                AgentId = entryAgentId,
                Channel = ConversationChannel.Phone
            });
            var conversationId = newConv.Id;
            convStorage.Append(conversationId, new List<RoleDialogModel>
            {
                new RoleDialogModel(AgentRole.User, "Hi, I'm calling to check my work order quote status, please help me locate my work order number and let me know what to do next.")
                {
                    CurrentAgentId = entryAgentId
                },
                new RoleDialogModel(AgentRole.Assistant, args.InitialMessage)
                {
                    CurrentAgentId = entryAgentId
                }
            });
            states.SetState(StateConst.SUB_CONVERSATION_ID, conversationId);

            // Generate audio
            /*var completion = CompletionProvider.GetAudioCompletion(_services, "openai", "tts-1");
            var data = await completion.GenerateAudioFromTextAsync(args.InitialMessage);
            var fileName = $"intial.mp3";
            fileStorage.SaveSpeechFile(conversationId, fileName, data);

            // Call phone number
            await sessionManager.SetAssistantReplyAsync(conversationId, 0, new AssistantMessage
            {
                Content = args.InitialMessage,
                SpeechFileName = fileName
            });*/

            var call = await CallResource.CreateAsync(
                // url: new Uri($"{_twilioSetting.CallbackHost}/twilio/voice/init-call?conversationId={conversationId}"),
                url: new Uri($"{_twilioSetting.CallbackHost}/twilio/stream?conversation_id={conversationId}"),
                to: new PhoneNumber(args.PhoneNumber),
                from: new PhoneNumber(_twilioSetting.PhoneNumber),
                asyncAmd: "true",
                machineDetection: "DetectMessageEnd");

            message.Content = $"The generated phone message: {args.InitialMessage}." ?? message.Content;
            message.StopCompletion = true;
            return true;
        }
    }
}
