namespace ZeroToHero.Player
{
    [System.Serializable]
    public class MoneyTransactionData
    {
        public string id;
        public string type;
        public string source;
        public int amount;
        public int balanceBefore;
        public int balanceAfter;
        public long timestampUtcTicks;
    }
}
