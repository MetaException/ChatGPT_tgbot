using Telegram.Bot;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using ChatGPT.Net;
using ChatGPT.Net.DTO;
using ChatGPT.Net.Enums;
using ChatGPT.Net.Session;

namespace TgBot
{
    internal class Program
    {
        private static ITelegramBotClient bot;
        private static ChatGpt chatGpt;
        private static string conversationId = "a-unique-string-id"; //We have only free version
        private static ChatGptClient chatGptClient;

        private static void Main(string[] args)
        {
            bot = new TelegramBotClient(Configuration.BotToken);

            Console.WriteLine("Запущен бот " + bot.GetMeAsync().Result.FirstName);

            chatGpt = new ChatGpt(new ChatGptConfig
            {
                UseCache = true
            });

            chatGptClient = chatGpt.CreateClient(new ChatGptClientConfig
            {
                SessionToken = Configuration.ChatGptApiKey,
                AccountType = AccountType.Free
            }).Result;

            var cts = new CancellationTokenSource();
            var cancellationToken = cts.Token;
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = { }, // receive all update types
            };
            bot.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cancellationToken
            );
            Console.ReadLine();
        }

        public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            // Некоторые действия
            //лог
            Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(update));

            if (update.Type == Telegram.Bot.Types.Enums.UpdateType.Message)
            {
                var message = update.Message;

                if (Convert.ToString(update.Message.Type) != "Text")
                    return;

                switch (message.Text.ToLower())
                {
                    case "/start":
                        await botClient.SendTextMessageAsync(message.Chat, "Привет!");
                        break;

                    default:
                        await chatGpt.WaitForReady();
                        var response = await chatGptClient.Ask(message.Text, conversationId);
                        Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(response));
                        if (!string.IsNullOrWhiteSpace(response))
                        {
                            try
                            {
                                await botClient.SendTextMessageAsync(message.Chat.Id, response, replyToMessageId: message.MessageId);
                            }
                            catch
                            {
                                await botClient.SendTextMessageAsync(message.Chat.Id, response);
                            }
                        }
                        break;
                }
            }
        }

        public static async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            // Некоторые действия
            Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(exception));
        }

        internal static class Configuration
        {
            public static string BotToken = "";
            public static string ChatGptApiKey = "";
        }
    }
}