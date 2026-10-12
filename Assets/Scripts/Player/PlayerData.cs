using System.Collections.Generic;
using ZeroToHero.Business;
using ZeroToHero.Bank;
using ZeroToHero.Assets;

namespace ZeroToHero.Player
{
    [System.Serializable]
    public class PlayerData
    {
        public int money = 0;
        public int health = 100;
        public int mood = 100;

        public string currentJobId = "job_courier";
        public List<string> completedEducation = new List<string>();
        public List<BusinessData> businesses = new List<BusinessData>();
        public List<DepositData> activeDeposits = new List<DepositData>();

        // ВАЖНО: Хранение купленного жилья и авто
        public AssetData assets = new AssetData();
    }
}