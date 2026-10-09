namespace ZeroToHero.Business
{
    [System.Serializable]
    public class BusinessData
    {
        public string id;
        public bool isPurchased;
        public int stars;             // 0..3 звёзд
        public int collectCount;      // Сколько раз собрали доход
        public bool hasManager;       // Нанят ли менеджер (автосбор)
    }
}