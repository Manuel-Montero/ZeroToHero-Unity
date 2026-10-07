using System;
using UnityEngine;

namespace ZeroToHero.Jobs
{
    [System.Serializable]
    public class JobConfig
    {
        public string id;
        public string jobTitle;
        public int salary;
        public float durationInSeconds;
        
        // ВАЖНО: ID образования, необходимого для этой работы (null или "" если не требуется)
        public string requiredEducationId;
    }
}