namespace BetfairSpOddsBandBetPlacerGruss.Models
{
    public class BetLog
    {
        public string Track { get; set; }
        public string OddsBand { get; set; }
        public string SelectionName { get; set; }
        public double BetPrice { get; set; }
        public DateTime Timestamp { get; set; }
        public string BetReference { get; set; }
    }

}
