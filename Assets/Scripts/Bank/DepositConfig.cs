namespace ZeroToHero.Bank
{
    [System.Serializable]
    public class DepositConfig
    {
        public string id;
        public string title;
        public float durationInSeconds; // Время в секундах (120с для 2 мин)
        public float profitPercent;      // Процент прибыли (например, 0.10f = +10%)
    }
}