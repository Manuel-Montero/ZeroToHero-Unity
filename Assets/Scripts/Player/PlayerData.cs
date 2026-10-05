using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZeroToHero.Player
{
    [Serializable]
    public class PlayerData
    {
        public int money = 0;
        public int tickets = 0;
        public int health = 100;
        public int mood = 100;

        public string currentJobId = "job_courier";
        public int dayIndex = 1;

       public List<string> completedEducation = new List<string>(); 
       public List<string> ownedAssets = new List<string>();
    }
}