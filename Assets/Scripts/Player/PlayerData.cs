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
        public float dayElapsedSeconds = 0f;

        // Real-time actions use UTC deadlines so they continue while the app is closed.
        public WorkTimerData workTimer = new WorkTimerData();
        public StudyTimerData studyTimer = new StudyTimerData();

        public string currentJobId = "job_courier";
        public List<string> completedEducation = new List<string>();
        public List<BusinessData> businesses = new List<BusinessData>();
        public List<DepositData> activeDeposits = new List<DepositData>();
        public List<MoneyTransactionData> transactions = new List<MoneyTransactionData>();
        public AssetData assets = new AssetData();
    }

    [System.Serializable]
    public class WorkTimerData
    {
        public bool isActive;
        public long endUtcTicks;
        public string jobId;
        public int salary;
        public float durationSeconds;
    }

    [System.Serializable]
    public class StudyTimerData
    {
        public bool isActive;
        public long endUtcTicks;
        public string educationId;
        public float durationSeconds;
    }
}
