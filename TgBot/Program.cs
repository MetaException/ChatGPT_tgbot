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
            UseCache = false
        });

        private static ChatGptClient chatGptClient = chatGpt.CreateClient(new ChatGptClientConfig
        {
            SessionToken = Configuration.ChatGptApiKey,
            AccountType = AccountType.Free
        }).Result;

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
                if (msg_queue.TryDequeue(out var message))
                {
                    await QueueMessageProc(bot, message);
                }
                else
                    await Task.Delay(1000);
            }
        }

        public static async Task QueueMessageProc(ITelegramBotClient botClient, Message message)
        {
            await chatGpt.WaitForReady();

            string response = chatGptClient.Ask(message.ReplyToMessage.Text, message.ReplyToMessage.Chat.Id.ToString()).Result;

            if (string.IsNullOrEmpty(response))
            {
                msg_queue.Enqueue(message);
                return;
            }

            await botClient.EditMessageTextAsync(message.Chat.Id, message.MessageId, message.Text = response);
            Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(message) + '\n');
        }

        public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(update) + '\n');

            if (update.Type == Telegram.Bot.Types.Enums.UpdateType.Message)
            {
                var message = update.Message;

                if (update.Message.Type.ToString() != "Text")
                    return;

                switch (message.Text.ToLower())
                {
                    case "/start":
                        await botClient.SendTextMessageAsync(message.Chat, "Привет!");
                        break;

                    default:
                        await botClient.SendChatActionAsync(message.Chat.Id, Telegram.Bot.Types.Enums.ChatAction.Typing);
                        var sended_msg = await botClient.SendTextMessageAsync(message.Chat.Id, "⏳ Ожидайте ответ...", replyToMessageId: message.MessageId);
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