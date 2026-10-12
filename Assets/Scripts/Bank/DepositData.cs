namespace ZeroToHero.Bank
{
    [System.Serializable]
    public class DepositData
    {
        public string id;
        public int investedAmount;      // Вложенная сумма
        public float timer;             // Текущий таймер
        public bool isActive;          // Вклад открыт прямо сейчас
        public bool isReadyToClaim;    // Доход готов к выдаче
    }
}