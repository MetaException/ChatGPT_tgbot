using ChatGPT.Net;
using ChatGPT.Net.DTO;
using ChatGPT.Net.Enums;
using ChatGPT.Net.Session;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace TgBot
{
    internal class Program
    {
        private static ITelegramBotClient bot;
        private static ChatGpt chatGpt;
        private static string conversationId = "a-unique-string-id"; //We have only free version
        private static ChatGptClient chatGptClient;

        internal class QueueElement
        {
            public Message message;
            public string prompt;
        }

        private static Queue<QueueElement> msg_queue = new Queue<QueueElement>();

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

            bot.ReceiveAsync(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cancellationToken
            );

            QueueProc();

            Console.ReadLine();
        }

        public static async Task QueueProc()
        {
            while (true)
            {
                if (msg_queue.Count > 0)
                {
                    var msg = msg_queue.Dequeue();
                    await Task.Run(() => QueueMessageProc(bot, msg));
                }
                await Task.Delay(1000);
            }
        }

        public static async Task QueueMessageProc(ITelegramBotClient botClient, QueueElement element)
        {
            await chatGpt.WaitForReady();
            string response = "";

            for (int i = 0; ; i++)
            {
                try { response = await chatGptClient.Ask(element.prompt, conversationId); }
                catch { Console.WriteLine("Неизвестная ошибка получения ответа"); }

                if (!string.IsNullOrWhiteSpace(response))
                    break;

                if (i > 50)
                    Environment.Exit(102);
            }

            Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(response));
            await botClient.EditMessageTextAsync(element.message.Chat.Id, element.message.MessageId, response);
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
                        await botClient.SendChatActionAsync(message.Chat.Id, Telegram.Bot.Types.Enums.ChatAction.Typing);
                        var sended_msg = await botClient.SendTextMessageAsync(message.Chat.Id, "⏳ Ожидайте ответ...", replyToMessageId: message.MessageId);
                        QueueElement element = new QueueElement
                        {
                            message = sended_msg,
                            prompt = message.Text
                        };
                        msg_queue.Enqueue(element);
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