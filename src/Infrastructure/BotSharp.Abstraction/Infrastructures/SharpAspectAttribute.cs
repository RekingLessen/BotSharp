using BotSharp.Abstraction.Conversations;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Rougamo.Context;
using System.Diagnostics;
using BotSharp.Abstraction.Repositories;
using BotSharp.Abstraction.Shared;

namespace BotSharp.Core.Infrastructures
{
    [AttributeUsage(AttributeTargets.All, Inherited = true)]
    public class SharpAspectAttribute : Attribute
    {
        private static IMongoDatabase GetMongoDatabase(IServiceProvider serviceProvider)
        {
            var dbSettings = serviceProvider.GetService<BotSharpDatabaseSettings>();
            if (dbSettings == null)
                throw new InvalidOperationException("Database settings not found.");

            var client = new MongoClient(dbSettings.BotSharpMongoDb);
            return client.GetDatabase("OneBrain");
        }

        private static async Task<bool> CheckCallChainTrackingAsync(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
                return false;

            var conversationService = serviceProvider.GetService<IConversationService>();
            if (conversationService == null)
                return false;

            var conversationId = conversationService.ConversationId;
            if (string.IsNullOrEmpty(conversationId))
                return false;

            var mongoDatabase = GetMongoDatabase(serviceProvider);
            var collection = mongoDatabase.GetCollection<BsonDocument>("OneBrain_Conversations");
            var filter = Builders<BsonDocument>.Filter.Eq("_id", conversationId);

            try
            {
                var result = await collection.FindAsync(filter);
                if (result != null && result.Any())
                {
                    var config = result.First();
                    return config.Contains("callchaintracking") && config["callchaintracking"].AsBoolean;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error checking call chain tracking configuration: {ex.Message}", ex);
            }

            return false;
        }

        private static async Task<bool> NeedPrintTrackInfo(MethodContext context, string functionName)
        {
            try
            {
                var serviceProvider = ((IHaveServiceProvider?)context.Target)?.ServiceProvider;
                if (serviceProvider == null)
                    return false;

                var service = serviceProvider.GetService<IConversationService>();
                if (service == null)
                    return false;

                var fullName = $"{context.Method.DeclaringType?.FullName}.{context.Method.Name}";
                if (context.Arguments != null && context.Method.DeclaringType?.FullName != null && !fullName.Equals(functionName))
                {
                    return await CheckCallChainTrackingAsync(serviceProvider);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error in NeedPrintTrackInfo: {ex.Message}", ex);
            }

            return false;
        }

        public async void OnEntry(MethodContext context)
        {
            if (await NeedPrintTrackInfo(context, "OnEntry"))
            {
                Debug.WriteLine($"{DateTime.UtcNow} Begin calling: {context.Method.DeclaringType?.FullName}.{context.Method.Name}");
            }
        }

        public async void OnExit(MethodContext context)
        {
            if (await NeedPrintTrackInfo(context, "OnExit"))
            {
                var message = $"{DateTime.UtcNow} End calling: {context.Method.DeclaringType?.FullName}.{context.Method.Name}";
                Debug.WriteLine(message);
            }
        }
    }
}
