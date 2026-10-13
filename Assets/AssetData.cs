using System.Collections.Generic;

namespace ZeroToHero.Assets
{
    [System.Serializable]
    public class AssetData
    {
        public List<string> ownedHouses = new List<string>();
        public List<string> ownedCars = new List<string>();
        public List<string> ownedClothing = new List<string>(); // <--- Добавили одежду
    }
}