namespace ZeroToHero.Business
{
    [System.Serializable]
    public class BusinessConfig
    {
        public string id;
        public string title;
        public int buyCost;           // Стоимость покупки
        public int baseIncome;        // Базовый доход за один сбор
        public int managerCost;       // Стоимость найма менеджера
    }
}