namespace ZeroToHero.Assets
{
    public enum AssetType
    {
        House,
        Car
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