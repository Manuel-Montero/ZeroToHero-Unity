namespace ZeroToHero.Bank
{
    [System.Serializable]
    public class DepositData
    {
        public string id;
        public int investedAmount;      // Вложенная сумма
        public float timer;             // Текущий таймер
        public long endUtcTicks;        // UTC deadline for offline progression
        public bool isActive;          // Вклад открыт прямо сейчас
        public bool isReadyToClaim;    // Доход готов к выдаче
    }
}
