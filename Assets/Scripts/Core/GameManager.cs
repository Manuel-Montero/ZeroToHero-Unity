using UnityEngine;
using ZeroToHero.Jobs;
using ZeroToHero.Player;

namespace ZeroToHero.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public PlayerData Player { get; private set; }
        public EconomyService Economy { get; private set; }
        public JobService Job { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Initialize();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Initialize()
        {
            Player = new PlayerData();
            Economy = new EconomyService(Player);
            Job = new JobService(Player, Economy);
        }

        private void Update()
        {
            Job?.UpdateTimer(Time.deltaTime);
        }
    }
}