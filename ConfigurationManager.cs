using Newtonsoft.Json;
using System;
using System.IO;

namespace BetfairSpOddsBandBetPlacerGruss
{
    public class ConfigManager
    {
        private static readonly Lazy<ConfigManager> _instance = new(() => new ConfigManager());
        public static ConfigManager Instance => _instance.Value;

        public string PositiveDifferencesPath { get; private set; }
        public string TelegramBotToken { get; private set; }
        public string TelegramChatId { get; private set; }

        private ConfigManager()
        {
            LoadConfig();
        }

        private void LoadConfig()
        {
            string configPath = "config.json";

            if (!File.Exists(configPath))
            {
                Console.WriteLine($"[ConfigManager] Config file not found: {configPath}. Using default values.");
                SetDefaults();
                return;
            }

            try
            {
                string json = File.ReadAllText(configPath);
                var configData = JsonConvert.DeserializeObject<ConfigData>(json);

                PositiveDifferencesPath = configData?.PositiveDifferencesPath ?? "BetfairSpPositiveDifferences.json";
                TelegramBotToken = configData?.TelegramBotToken ?? "";
                TelegramChatId = configData?.TelegramChatId ?? "";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ConfigManager] Error reading config file: {ex.Message}. Using default values.");
                SetDefaults();
            }
        }

        private void SetDefaults()
        {
            PositiveDifferencesPath = "BetfairSpPositiveDifferences.json";
            TelegramBotToken = "";
            TelegramChatId = "";
        }

        private class ConfigData
        {
            public string PositiveDifferencesPath { get; set; }
            public string TelegramBotToken { get; set; }
            public string TelegramChatId { get; set; }
        }
    }
}
