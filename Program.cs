using BetfairSpOddsBandBetPlacerGruss;
using BettingAssistantCom.Application;
using Newtonsoft.Json;

class Program
{
    static async Task Main()
    {
        try
        {
            Console.WriteLine("Starting Gruss Betting Assistant...");

            var positiveDifferences = LoadPositiveDifferences();

            var gruss = new ComClass();
            ClearAllTabs(gruss);

            var marketTasks = new List<Task>();
            var allSports = gruss.getSports();

            foreach (var sport in allSports.OfType<BfSport>())
            {
                if (sport.sport != "Horse Racing") continue;

                var horseRacingEvents = gruss.getEvents(sport.sportId);

                foreach (var horseRacingEvent in horseRacingEvents.OfType<BfEvent>())
                {
                    if (!IsUkOrIrelandEvent(horseRacingEvent)) continue;

                    var trackEvents = gruss.getEvents(horseRacingEvent.eventId);
                    foreach (var trackEvent in trackEvents.OfType<BfEvent>())
                    {
                        if (!IsTodayEvent(trackEvent)) continue;

                        var winEvents = GetWinEvents(gruss, trackEvent.eventId);
                        if (winEvents.Any())
                        {
                            var newTabIndex = gruss.addTabPage();
                            var marketBot = new MarketBot(newTabIndex, trackEvent.eventName, winEvents, new ComClass() { tabIndex = newTabIndex - 1 }, positiveDifferences);
                            marketTasks.Add(marketBot.MonitorMarkets());

                            // Send a Telegram notification for each market set up
                            await SendMarketSetupMessage(trackEvent.eventName, winEvents.Count);
                        }
                    }
                }
            }

            await Task.WhenAll(marketTasks);

            // Send a completion message when all markets are processed
            await SendCompletionMessage();

            Console.WriteLine("[Program] All markets have been processed. Exiting application...");
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error using Gruss Betting Assistant: " + ex.Message);

            // Send failure notification
            await SendFailureMessage(ex.Message, ex.StackTrace);
        }
    }

    private static Dictionary<string, List<string>> LoadPositiveDifferences()
    {
        string filePath = ConfigManager.Instance.PositiveDifferencesPath;

        if (!File.Exists(filePath))
        {
            Console.WriteLine($"[Program] Positive Differences file not found: {filePath}");
            return new Dictionary<string, List<string>>();
        }

        try
        {
            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json) ?? new Dictionary<string, List<string>>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Program] Error reading Positive Differences file: {ex.Message}");
            return new Dictionary<string, List<string>>();
        }
    }

    private static void ClearAllTabs(ComClass gruss)
    {
        var tabPageCount = gruss.tabPageCount;
        for (int i = 0; i < tabPageCount + 1; i++)
        {
            gruss.deleteTabPage(0);

            Thread.Sleep(2000);
        }
    }

    private static bool IsUkOrIrelandEvent(BfEvent horseRacingEvent)
    {
        return horseRacingEvent.eventName == "GB" || horseRacingEvent.eventName == "IRE";
    }

    private static bool IsTodayEvent(BfEvent trackEvent)
    {
        return trackEvent.eventName.Contains(DateTime.Today.Date.Day.ToString());
    }

    private static List<BfEvent> GetWinEvents(ComClass gruss, long eventId)
    {
        var winEvents = new List<BfEvent>();
        var races = gruss.getEvents(eventId);

        foreach (var race in races.OfType<BfEvent>())
        {
            if (IsRaceType(race.eventName))
            {
                winEvents.Add(race);
            }
        }

        return winEvents;
    }

    private static bool IsRaceType(string eventName)
    {
        return eventName.Contains("Hcap") ||
               eventName.Contains("Mdn") ||
               eventName.Contains("Nov") ||
               eventName.Contains("NHF");
    }

    /// <summary>
    /// Sends a Telegram message when a market is successfully set up.
    /// </summary>
    /// <param name="trackName">Track name for the market</param>
    /// <param name="eventCount">Number of events in the market</param>
    private static async Task SendMarketSetupMessage(string trackName, int eventCount)
    {
        string message = $"🎯 *New Market Set Up!*\n\n" +
                         $"📍 *Track:* {trackName}\n" +
                         $"🏇 *Races Set Up:* {eventCount}\n" +
                         $"⏰ *Time:* {DateTime.Now:dd-MM-yyyy HH:mm:ss}";

        await TelegramNotifier.SendMessageAsync(message);
    }

    /// <summary>
    /// Sends a Telegram message when all markets have been processed.
    /// </summary>
    private static async Task SendCompletionMessage()
    {
        string message = $"✅ *All Markets Processed!*\n\n" +
                         $"🕒 *Completed At:* {DateTime.Now:dd-MM-yyyy HH:mm:ss}";

        await TelegramNotifier.SendMessageAsync(message);
    }

    /// <summary>
    /// Sends a Telegram message if an error occurs.
    /// </summary>
    /// <param name="errorMessage">Error message</param>
    /// <param name="stackTrace">Stack trace of the error</param>
    private static async Task SendFailureMessage(string errorMessage, string stackTrace = "")
    {
        string message = $"❌ *Error Occurred in Gruss Betting Assistant!*\n\n" +
                         $"💥 *Error Message:* {errorMessage}\n" +
                         $"🕒 *Time:* {DateTime.Now:dd-MM-yyyy HH:mm:ss}\n";

        if (!string.IsNullOrEmpty(stackTrace))
        {
            message += $"📄 *Stack Trace:*\n`{stackTrace}`";
        }

        await TelegramNotifier.SendMessageAsync(message);
    }
}
