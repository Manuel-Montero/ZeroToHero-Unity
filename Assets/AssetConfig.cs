namespace ZeroToHero.Assets
{
    public enum AssetType
    {
        House,
        Car,
        Clothing // <--- Добавили одежду
    }

    [System.Serializable]
    public class AssetConfig
    {
        public string id;
        public string title;
        public AssetType type;
        public int price;
    }
}