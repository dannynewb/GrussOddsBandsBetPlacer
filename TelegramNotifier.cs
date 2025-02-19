using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace BetfairSpOddsBandBetPlacerGruss
{
    public static class TelegramNotifier
    {
        private static readonly string BotToken = ConfigManager.Instance.TelegramBotToken;
        private static readonly string ChatId = ConfigManager.Instance.TelegramChatId;

        public static async Task SendMessageAsync(string message)
        {
            if (string.IsNullOrEmpty(BotToken) || string.IsNullOrEmpty(ChatId))
            {
                Console.WriteLine("[TelegramNotifier] Missing bot token or chat ID. Skipping Telegram notification.");
                return;
            }

            string url = $"https://api.telegram.org/bot{BotToken}/sendMessage";
            var values = new Dictionary<string, string>
            {
                { "chat_id", ChatId },
                { "text", message }
            };

            using (HttpClient client = new HttpClient())
            {
                try
                {
                    var response = await client.PostAsync(url, new FormUrlEncodedContent(values));
                    Console.WriteLine($"[TelegramNotifier] Sent Telegram message: {message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TelegramNotifier] Error sending Telegram message: {ex.Message}");
                }
            }
        }
    }
}
