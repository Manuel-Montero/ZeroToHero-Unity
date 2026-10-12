using System.Collections.Generic;
using ZeroToHero.Assets;
using ZeroToHero.Bank;
using ZeroToHero.Business;

namespace ZeroToHero.Player
{
    [System.Serializable]
    public class PlayerData
    {
        public int money = 0;
        public int health = 100;
        public int mood = 100;

        // Календарь
        public int day = 1;
        public int month = 1;

        public string currentJobId = "job_courier";
        public List<string> completedEducation = new List<string>();
        public List<BusinessData> businesses = new List<BusinessData>();
        public List<DepositData> activeDeposits = new List<DepositData>();
        public AssetData assets = new AssetData();
    }
}