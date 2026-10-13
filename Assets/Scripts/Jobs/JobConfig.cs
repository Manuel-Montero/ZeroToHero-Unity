using System.Collections.Generic;

namespace ZeroToHero.Jobs
{
    [System.Serializable]
    public class JobConfig
    {
        public string id;
        public string jobTitle;
        public int salary;
        public float workDurationInSeconds; // Время смены в секундах

        // Требования для получения работы
        public string requiredEducationId; // Требуемое образование
        public string requiredHouseId;     // Требуемое жилье
        public string requiredCarId;       // Требуемый транспорт
        public string requiredClothingId;  // Требуемая одежда
    }
}