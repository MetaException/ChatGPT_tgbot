using ChatGPT.Net;
using ChatGPT.Net.DTO;
using ChatGPT.Net.Enums;
using ChatGPT.Net.Session;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TgBot
{
    internal class Program
    {
        private static ITelegramBotClient bot = new TelegramBotClient(Configuration.BotToken);

        private static ChatGpt chatGpt = new ChatGpt(new ChatGptConfig
        {
            UseCache = true
        });

        private static ChatGptClient chatGptClient = chatGpt.CreateClient(new ChatGptClientConfig
        {
            SessionToken = Configuration.ChatGptApiKey,
            AccountType = AccountType.Free
        }).Result;

        private static string conversationId = "a-unique-string-id"; //Добавить разделение на беседы

        private static readonly Queue<Message> msg_queue = new Queue<Message>();

        private static void Main(string[] args)
        {
            Console.WriteLine("Запущен бот " + bot.GetMeAsync().Result.FirstName);

            bot.StartReceiving(HandleUpdateAsync, HandleErrorAsync);

            _ = QueueProc();

            Console.ReadLine();
        }

        public static async Task QueueProc()
        {
            while (true)
            {
                if (msg_queue.TryDequeue(out var item))
                {
                    await QueueMessageProc(bot, item);
                }
                await Task.Delay(1000);
            }
        }

        public static async Task QueueMessageProc(ITelegramBotClient botClient, Message message)
        {
            await chatGpt.WaitForReady();
            string response = "";
            try
            {
                response = chatGptClient.Ask(message.ReplyToMessage.Text, conversationId).Result;
            }
            catch
            {
                msg_queue.Enqueue(message);
            }

            Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(response));
            await botClient.EditMessageTextAsync(message.Chat.Id, message.MessageId, response);
        }

        public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
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
                        Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(sended_msg));
                        msg_queue.Enqueue(sended_msg);
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