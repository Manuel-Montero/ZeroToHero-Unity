using System.Collections.Generic;
using ZeroToHero.Business;

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
        
        // ВАЖНО: Добавляем сохраненные бизнесы
        public List<BusinessData> businesses = new List<BusinessData>();
    }
}