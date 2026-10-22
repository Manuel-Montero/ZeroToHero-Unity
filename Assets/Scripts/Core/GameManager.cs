using UnityEngine;
using ZeroToHero.Assets;
using ZeroToHero.Bank;
using ZeroToHero.Business;
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
        public NeedsService Needs { get; private set; }
        public BusinessService Business { get; private set; }
        public BankService Bank { get; private set; }
        public AssetService Assets { get; private set; }
        public TimeManager TimeSystem { get; private set; } // <--- Добавили
        public SaveService SaveSystem { get; private set; }
        public LoadResult LastLoadResult { get; private set; }
        public SaveResult LastSaveResult { get; private set; }

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
            LastLoadResult = SaveSystem.Load();
            if (!LastLoadResult.Success)
            {
                Debug.LogError("[GameManager] Игра остановлена: " + LastLoadResult.Error);
                enabled = false;

                MainUI unavailableUi = Object.FindAnyObjectByType<MainUI>();
                if (unavailableUi != null) unavailableUi.enabled = false;
                return;
            }

            Player = LastLoadResult.Data;

            Economy = new EconomyService(Player);
            Needs = new NeedsService(Player, Economy);
            Business = new BusinessService(Player, Economy);
            Assets = new AssetService(Player, Economy);
            TimeSystem = new TimeManager(Player, Economy, Assets);
            Job = new JobService(Player, Economy, TimeSystem);
            Education = new EducationService(Player, Economy, TimeSystem);
            Bank = new BankService(Player, Economy, TimeSystem);

            // Автосохранения
            // Persist actions after their state is complete. Saving from the balance
            // event would capture intermediate states (for example, before a deposit
            // is marked claimed).
            Job.OnWorkStarted += SaveGame;
            Education.OnStudyStarted += SaveGame;
            Job.OnWorkCompleted += () => 
            {
                Needs.ConsumeNeeds(5, 5);
            };
            Education.OnStudyCompleted += (eduId) => 
            {
                Needs.ConsumeNeeds(10, 10);
            };
            Needs.OnNeedsChanged += SaveGame;
            Business.OnBusinessUpdated += SaveGame;
            Bank.OnBankUpdated += SaveGame;
            Assets.OnAssetsUpdated += SaveGame;
            TimeSystem.OnDayPassed += SaveGame; // <--- Сохранение при смене дня

            if (Bank.MigratedLegacyDeadlines)
                SaveGame();
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
            if (Job != null) Job.UpdateTimer(Time.unscaledDeltaTime);
            if (Education != null) Education.UpdateTimer(Time.unscaledDeltaTime);
            if (Bank != null) Bank.UpdateTimer(Time.unscaledDeltaTime);
            if (TimeSystem != null) TimeSystem.UpdateTimer(Time.unscaledDeltaTime);
        }

        public void SaveGame()
        {
            if (SaveSystem != null && Player != null)
                LastSaveResult = SaveSystem.Save(Player);
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
