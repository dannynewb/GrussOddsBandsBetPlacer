using BetfairSpOddsBandBetPlacerGruss.Models;
using BettingAssistantCom.Application;
using Newtonsoft.Json;

namespace BetfairSpOddsBandBetPlacerGruss
{
    public class MarketBot
    {
        private readonly List<BetLog> _betLogs = [];

        private readonly int _tabIndex;
        private readonly string _track;
        private readonly List<BfEvent> _races;
        private readonly ComClass _gruss;
        private readonly Dictionary<string, List<string>> _positiveDifferences;
        private readonly string _normalizedTrackName;

        public MarketBot(int tabIndex, string track, List<BfEvent> races, ComClass gruss, Dictionary<string, List<string>> positiveDifferences)
        {
            _tabIndex = tabIndex;
            _track = track;
            _races = races;
            _gruss = gruss;
            _positiveDifferences = positiveDifferences;
            _normalizedTrackName = track.ToLower(); // Normalize track name for case-insensitive comparison
        }

        public async Task MonitorMarkets()
        {
            Console.WriteLine($"[MarketBot] Starting market monitoring for track: {_track}");

            var orderedRaces = _races.OrderBy(r => r.startTime);

            foreach (var race in orderedRaces)
            {
                Console.WriteLine($"[MarketBot] --- Monitoring race: {race.eventName} - {race.startTime.ToShortTimeString()} ---");

                // Open market for the race
                if (!OpenMarket(race))
                {
                    Console.WriteLine($"[MarketBot] Failed to open market for race: {race.eventName}");
                    continue;
                }

                Thread.Sleep(2000);

                Console.WriteLine($"[MarketBot] Market opened for race: {race.eventName}");

                if (IsMarketClosed())
                {
                    continue;
                }

                // Wait until the market goes in-play
                await WaitForInPlay();

                Console.WriteLine($"[MarketBot] Market is in-play for race: {race.eventName}");

                // Get the selection prices for the market
                var selectionPrices = GetSelectionPrices();

                Console.WriteLine($"[MarketBot] Retrieved prices for {_track} - {race.eventName}");

                // Check if the current track has positive SP bands
                if (HasPositiveBands())
                {
                    var positiveBands = GetPositiveBands();
                    Console.WriteLine($"[MarketBot] Positive SP bands found for track containing: {_track}");
                    Console.WriteLine($"[MarketBot] Positive bands: {string.Join(", ", positiveBands)}");

                    // Evaluate odds and place bets
                    EvaluateAndPlaceBets(selectionPrices, positiveBands);
                }
                else
                {
                    Console.WriteLine($"[MarketBot] No positive SP bands for track: {_track}");
                }


                Console.WriteLine($"[MarketBot] --- Finished monitoring race: {race.eventName} ---");

                WriteAllBetsToJson();
                await SendTelegramMessagesForAllBets();

                _betLogs.Clear();
                Console.WriteLine("[MarketBot] Cleared in-memory bet logs.");
            }

            Console.WriteLine($"[MarketBot] Finished market monitoring for track: {_track}");
        }

        /// <summary>
        /// Opens the market for the given race.
        /// </summary>
        /// <param name="race">Race event</param>
        /// <returns>True if the market is opened successfully, otherwise false</returns>
        private bool OpenMarket(BfEvent race)
        {
            Console.WriteLine($"[MarketBot] Opening market for race: {race.eventName}");
            var openMarketFailure = _gruss.openMarket((int)race.eventId, race.exchangeId);

            if (openMarketFailure != "")
            {
                Console.WriteLine($"[MarketBot] Error opening market for {_track} - {race.eventName}, ex: {openMarketFailure}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if the market is closed.
        /// </summary>
        private bool IsMarketClosed()
        {
            Console.WriteLine($"[MarketBot] Checking to see if market is closed for {_track}...");

            var inPlayCheck = _gruss.getPrices();
            var parsedPrice = (Price)inPlayCheck[0];

            if (parsedPrice.closed)
            {
                Console.WriteLine($"[MarketBot] Market is closed for {_track}");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Waits for the market to go in-play.
        /// </summary>
        private async Task WaitForInPlay()
        {
            Console.WriteLine($"[MarketBot] Waiting for market to go in-play for {_track}...");

            while (true)
            {
                var inPlayCheck = _gruss.getPrices();
                var parsedPrice = (Price)inPlayCheck[0];

                if (parsedPrice.inPlay)
                {
                    Console.WriteLine($"[MarketBot] Market is now in-play for {_track}");
                    break;
                }

                await Task.Delay(2000);
            }
        }

        /// <summary>
        /// Retrieves the selection prices for the current market.
        /// </summary>
        /// <returns>List of MarketDepthSelection prices</returns>
        private MarketDepthSelection[] GetSelectionPrices()
        {
            Console.WriteLine($"[MarketBot] Retrieving selection prices for {_track}...");
            var selectionPrices = _gruss.getMarketDepth(true);

            var parsedObjects = new List<MarketDepthSelection>();

            var test = (object[])selectionPrices;
            foreach (var price in test)
            {
                parsedObjects.Add((MarketDepthSelection)price);
            }

            return [.. parsedObjects];
        }

        /// <summary>
        /// Checks if the current track contains any positive SP bands key.
        /// </summary>
        /// <returns>True if a matching SP band key is found, otherwise false</returns>
        private bool HasPositiveBands()
        {
            return _positiveDifferences.Keys.Any(key => _normalizedTrackName.Contains(key.ToLower()));
        }

        /// <summary>
        /// Retrieves positive SP bands for the current track by checking if the track name contains any key.
        /// </summary>
        /// <returns>List of positive SP bands</returns>
        private List<string> GetPositiveBands()
        {
            var matchingKey = _positiveDifferences
                .FirstOrDefault(pair => _normalizedTrackName.Contains(pair.Key.ToLower())).Key;

            return matchingKey != null ? _positiveDifferences[matchingKey] : new List<string>();
        }

        /// <summary>
        /// Evaluates odds against positive bands and places bets if conditions are met.
        /// </summary>
        /// <param name="selectionPrices">Array of MarketDepthSelection prices</param>
        /// <param name="positiveBands">List of positive SP bands</param>
        private void EvaluateAndPlaceBets(MarketDepthSelection[] selectionPrices, List<string> positiveBands)
        {
            Console.WriteLine($"[MarketBot] Evaluating odds for track: {_track}");

            var prices = _gruss.getPrices();
            var parsedPrices = new List<Price>();

            var test = (object[])selectionPrices;
            foreach (var price in prices)
            {
                parsedPrices.Add((Price)price);
            }

            foreach (var price in selectionPrices)
            {
                var selection = parsedPrices.FirstOrDefault(p => p.selection == price.selection);
                if (selection == null) continue;

                // Retrieve the selection name (horse name)
                string selectionName = selection.selection;

                // Check odds against all positive SP bands for this track
                foreach (var band in positiveBands)
                {
                    var bandParts = band.Split('-');
                    decimal lowerBound = decimal.Parse(bandParts[0]);
                    decimal upperBound = decimal.Parse(bandParts[1]);

                    // Place a bet only if the odds are within the positive band
                    if ((decimal)price.actualSPPrice >= lowerBound && (decimal)price.actualSPPrice <= upperBound)
                    {
                        Console.WriteLine($"[MarketBot] Placing bet on {_track} - {selectionName} at SP {price.actualSPPrice} for Odds Band: {band}");
                        PlaceBet(selectionName, Array.IndexOf(selectionPrices, price), price.actualSPPrice, band);
                    }
                }
            }
        }


        /// <summary>
        /// Places a bet on the given selection and collects the bet details for logging and Telegram notification.
        /// </summary>
        /// <param name="selectionName">Selection Name (Horse Name)</param>
        /// <param name="selectionIndex">Selection Index</param>
        /// <param name="price">SP Price</param>
        /// <param name="oddsBand">Odds Band for the bet</param>
        private void PlaceBet(string selectionName, int selectionIndex, double price, string oddsBand)
        {
            const double STAKE = 0.05;
            var result = _gruss.placeBet(selectionIndex, "B", 1.01, STAKE, true, "", -1, 1);

            Console.WriteLine($"[MarketBot] Bet placed on {selectionName} at SP {price}");

            // Collect the bet log in memory
            _betLogs.Add(new BetLog
            {
                Track = _track,
                OddsBand = oddsBand,
                SelectionName = selectionName,
                BetPrice = price,
                Timestamp = DateTime.Now,
                BetReference = result
            });
        }

        /// <summary>
        /// Sends a Telegram message for each bet after all bets have been placed.
        /// </summary>
        private async Task SendTelegramMessagesForAllBets()
        {
            foreach (var bet in _betLogs)
            {
                string message = $"📢 *New Bet Placed!*\n\n" +
                                 $"🏇 *Selection:* {bet.SelectionName}\n" +
                                 $"📍 *Track:* {bet.Track}\n" +
                                 $"🎯 *Odds Band:* {bet.OddsBand}\n" +
                                 $"💰 *SP Price:* {bet.BetPrice}\n" +
                                 $"🕒 *Time:* {bet.Timestamp:HH:mm:ss}\n" +
                                 $"📄 *Bet Reference:* {bet.BetReference}";

                await TelegramNotifier.SendMessageAsync(message);
            }
            Console.WriteLine($"[MarketBot] All Telegram messages sent.");
        }

        /// <summary>
        /// Writes all bet logs to JSON in a structured format:
        /// - Track
        ///   - Odds Band
        ///     - Bet Details
        /// Ensures thread safety and additive writing.
        /// </summary>
        private void WriteAllBetsToJson()
        {
            string filePath = "BetfairBetsLog.json";
            Dictionary<string, Dictionary<string, List<BetLog>>> structuredLogs = new Dictionary<string, Dictionary<string, List<BetLog>>>();

            // Read existing logs if the file exists
            if (File.Exists(filePath))
            {
                try
                {
                    string existingJson = File.ReadAllText(filePath);
                    structuredLogs = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, List<BetLog>>>>(existingJson)
                                     ?? new Dictionary<string, Dictionary<string, List<BetLog>>>();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MarketBot] Error reading existing bet log file: {ex.Message}");
                }
            }

            // Group bets by Track and Odds Band
            foreach (var bet in _betLogs)
            {
                if (!structuredLogs.ContainsKey(bet.Track))
                {
                    structuredLogs[bet.Track] = new Dictionary<string, List<BetLog>>();
                }

                if (!structuredLogs[bet.Track].ContainsKey(bet.OddsBand))
                {
                    structuredLogs[bet.Track][bet.OddsBand] = new List<BetLog>();
                }

                structuredLogs[bet.Track][bet.OddsBand].Add(bet);
            }

            // Write back to file with thread safety
            try
            {
                lock (this) // Ensure thread safety
                {
                    File.WriteAllText(filePath, JsonConvert.SerializeObject(structuredLogs, Formatting.Indented));
                }
                Console.WriteLine($"[MarketBot] All bets logged to {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MarketBot] Error writing to bet log file: {ex.Message}");
            }
        }

    }
}
