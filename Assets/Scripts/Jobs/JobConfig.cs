using System;
using UnityEngine;

namespace ZeroToHero.Jobs
{
    [Serializable]
    public class JobConfig
    {
        public string id;
        public string jobTitle;
        public int salary = 100;
        public float durationInSeconds = 3f; // Длительность рабочего цикла (таймер)

        // Требования (на будущее)
        public string requiredEducationId;
        public string requiredClothesId;
    }
}