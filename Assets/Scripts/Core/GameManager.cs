using UnityEngine;
using ZeroToHero.Education;
using ZeroToHero.Jobs;
using ZeroToHero.Player;
using ZeroToHero.UI;

namespace ZeroToHero.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public PlayerData Player { get; private set; }
        public EconomyService Economy { get; private set; }
        public JobService Job { get; private set; }
        public EducationService Education { get; private set; }
        public NeedsService Needs { get; private set; } // <--- Добавили
        public SaveService SaveSystem { get; private set; }

        private MainUI _mainUI;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitServices();
        }

        private void InitServices()
        {
            SaveSystem = new SaveService();
            
            Player = SaveSystem.Load();

            Economy = new EconomyService(Player);
            Job = new JobService(Player, Economy);
            Education = new EducationService(Player, Economy);
            Needs = new NeedsService(Player, Economy); // <--- Добавили

            // Автосохранение при изменениях
            Economy.OnBalanceChanged += (amount, reason) => SaveGame();
            Job.OnWorkCompleted += () => 
            {
                Needs.ConsumeNeeds(5, 5); // Снимаем 5 HP и 5 Mood за каждую смену работы
                SaveGame();
            };
            Education.OnStudyCompleted += (eduId) => 
            {
                Needs.ConsumeNeeds(10, 10); // Снимаем 10 HP и 10 Mood за учебу
                SaveGame();
            };
            Needs.OnNeedsChanged += SaveGame;
        }

        private void Start()
        {
            _mainUI = Object.FindAnyObjectByType<MainUI>();
            if (_mainUI != null)
            {
                _mainUI.UpdateUI();
            }
        }

        private void Update()
        {
            if (Job != null) Job.UpdateTimer(Time.deltaTime);
            if (Education != null) Education.UpdateTimer(Time.deltaTime);
        }

        public void SaveGame()
        {
            if (SaveSystem != null && Player != null) SaveSystem.Save(Player);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) SaveGame();
        }

        private void OnApplicationQuit()
        {
            SaveGame();
        }
    }
}