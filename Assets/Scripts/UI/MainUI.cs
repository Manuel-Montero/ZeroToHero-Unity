using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZeroToHero.Business;
using ZeroToHero.Core;

namespace ZeroToHero.UI
{
    public class MainUI : MonoBehaviour
    {
        [Header("Status Bars")]
        public TMP_Text moneyText;
        public TMP_Text healthText;
        public TMP_Text moodText;
        public TMP_Text dateText; // <--- Добавляем текст для отображения дня и месяца

        [Header("Job Panel")]
        public TMP_Text jobTitleText;
        public TMP_Text salaryText;
        public Button workButton;
        public Image workProgressBar;
        public Button promoteWaiterButton;
        public Button promoteManagerButton;

        [Header("Education Panel")]
        public TMP_Text studyTitleText;
        public Button studyButton;
        public Image studyProgressBar;

        [Header("Needs Panel")]
        public Button healButton;
        public Button restButton;

        [Header("Business Panel")]
        public TMP_Text businessStatusText;
        public Button buyBusinessButton;
        public Button collectIncomeButton;

        [Header("Bank Panel")]
        public TMP_Text bankStatusText;
        public Button openDepositButton;
        public Button claimDepositButton;

        [Header("Assets Panel")]
        public TMP_Text houseStatusText;   // Статус жилья
        public Button buyHouseButton;      // Кнопка покупки жилья (Койко-место / Квартира)
        public TMP_Text carStatusText;     // Статус авто
        public Button buyCarButton;        // Кнопка покупки транспорта (Велосипед / Седан)
        public TMP_Text clothingStatusText; // <--- Статус одежды
        public Button buyCasualClothingButton; // <--- Кнопка покупки "Повседневная одежда" (100 KZT)
        public Button buySuitClothingButton;   // <--- Кнопка покупки "Деловой костюм" (1200 KZT)

        private TMP_Text _workButtonLabel;

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                if (GameManager.Instance.Job != null)
                {
                    GameManager.Instance.Job.OnTimerTick += OnWorkProgress;
                    GameManager.Instance.Job.OnWorkCompleted += OnWorkDone;
                    GameManager.Instance.Job.OnWorkStarted += OnWorkStart;
                    GameManager.Instance.Job.OnSalaryClaimed += UpdateUI;
                }

                if (GameManager.Instance.Education != null)
                {
                    GameManager.Instance.Education.OnStudyProgress += OnStudyProgress;
                    GameManager.Instance.Education.OnStudyCompleted += OnStudyDone;
                    GameManager.Instance.Education.OnStudyStarted += OnStudyStart;
                }

                if (GameManager.Instance.Needs != null)
                {
                    GameManager.Instance.Needs.OnNeedsChanged += UpdateUI;
                }

                if (GameManager.Instance.Business != null)
                {
                    GameManager.Instance.Business.OnBusinessUpdated += UpdateUI;
                }

                if (GameManager.Instance.Bank != null)
                {
                    GameManager.Instance.Bank.OnBankUpdated += UpdateUI;
                }

                if (GameManager.Instance.Assets != null)
                {
                    GameManager.Instance.Assets.OnAssetsUpdated += UpdateUI;
                }

                if (GameManager.Instance.TimeSystem != null)
                {
                    GameManager.Instance.TimeSystem.OnDayPassed += UpdateUI;
                }
            }

            if (workButton != null)
            {
                workButton.onClick.AddListener(OnWorkButtonClicked);
                _workButtonLabel = workButton.GetComponentInChildren<TMP_Text>(true);
            }
            if (studyButton != null) studyButton.onClick.AddListener(OnStudyButtonClicked);
            if (promoteWaiterButton != null) promoteWaiterButton.onClick.AddListener(OnPromoteWaiterClicked);
            
            if (healButton != null) healButton.onClick.AddListener(OnHealClicked);
            if (restButton != null) restButton.onClick.AddListener(OnRestClicked);

            if (buyBusinessButton != null) buyBusinessButton.onClick.AddListener(OnBuyBusinessClicked);
            if (collectIncomeButton != null) collectIncomeButton.onClick.AddListener(OnCollectIncomeClicked);

            if (openDepositButton != null) openDepositButton.onClick.AddListener(OnOpenDepositClicked);
            if (claimDepositButton != null) claimDepositButton.onClick.AddListener(OnClaimDepositClicked);

            if (buyHouseButton != null) buyHouseButton.onClick.AddListener(OnBuyHouseClicked);
            if (buyCarButton != null) buyCarButton.onClick.AddListener(OnBuyCarClicked);

            if (promoteManagerButton != null) promoteManagerButton.onClick.AddListener(OnPromoteManagerClicked);
            if (buyCasualClothingButton != null) buyCasualClothingButton.onClick.AddListener(() => BuyClothing("wear_casual"));
            if (buySuitClothingButton != null) buySuitClothingButton.onClick.AddListener(() => BuyClothing("wear_suit"));

            UpdateUI();
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                if (GameManager.Instance.Job != null)
                {
                    GameManager.Instance.Job.OnTimerTick -= OnWorkProgress;
                    GameManager.Instance.Job.OnWorkCompleted -= OnWorkDone;
                    GameManager.Instance.Job.OnWorkStarted -= OnWorkStart;
                    GameManager.Instance.Job.OnSalaryClaimed -= UpdateUI;
                }

                if (GameManager.Instance.Education != null)
                {
                    GameManager.Instance.Education.OnStudyProgress -= OnStudyProgress;
                    GameManager.Instance.Education.OnStudyCompleted -= OnStudyDone;
                    GameManager.Instance.Education.OnStudyStarted -= OnStudyStart;
                }

                if (GameManager.Instance.Needs != null)
                {
                    GameManager.Instance.Needs.OnNeedsChanged -= UpdateUI;
                }

                if (GameManager.Instance.Business != null)
                {
                    GameManager.Instance.Business.OnBusinessUpdated -= UpdateUI;
                }

                if (GameManager.Instance.Bank != null)
                {
                    GameManager.Instance.Bank.OnBankUpdated -= UpdateUI;
                }

                if (GameManager.Instance.Assets != null)
                {
                    GameManager.Instance.Assets.OnAssetsUpdated -= UpdateUI;
                }
            }
        }

        private void Update()
        {
            bool refresh = false;
            if (GameManager.Instance != null && GameManager.Instance.Bank != null)
            {
                var depData = GameManager.Instance.Bank.GetDepositData("dep_quick");
                if (depData.isActive)
                {
                    refresh = true;
                }
            }

            if (!refresh && GameManager.Instance != null && GameManager.Instance.Business != null)
            {
                var coffee = GameManager.Instance.Business.GetBusinessData("biz_coffee");
                refresh = coffee.isPurchased && coffee.accumulatedIncome < BusinessService.MaxAccumulatedIncome;
            }

            if (refresh) UpdateUI();
        }

        private void OnWorkButtonClicked()
        {
            GameManager game = GameManager.Instance;
            if (game == null || game.Job == null) return;

            if (game.Job.IsSalaryReadyToClaim)
            {
                game.Job.ClaimSalary();
            }
            else if (!game.Job.IsWorking && game.Needs.CanWork())
            {
                game.Job.StartWork();
            }

            UpdateUI();
        }

        private void OnStudyButtonClicked()
        {
            GameManager.Instance.Education.StartStudy("edu_courses");
            UpdateUI();
        }

        private void OnPromoteWaiterClicked()
        {
            GameManager.Instance.Job.PromoteToJob("job_waiter", GameManager.Instance.Education, GameManager.Instance.Assets);
            UpdateUI();
        }

        private void OnPromoteManagerClicked()
        {
            GameManager.Instance.Job.PromoteToJob(
                "job_manager", 
                GameManager.Instance.Education, 
                GameManager.Instance.Assets
            );
            UpdateUI();
        }

        private void BuyClothing(string clothingId)
        {
            GameManager.Instance.Assets.BuyAsset(clothingId);
            UpdateUI();
        }

        private void OnHealClicked()
        {
            GameManager.Instance.Needs.Heal(20, 50);
            UpdateUI();
        }

        private void OnRestClicked()
        {
            GameManager.Instance.Needs.Rest(20, 50);
            UpdateUI();
        }

        private void OnBuyBusinessClicked()
        {
            GameManager.Instance.Business.BuyBusiness("biz_coffee");
            UpdateUI();
        }

        private void OnCollectIncomeClicked()
        {
            GameManager.Instance.Business.CollectIncome("biz_coffee");
            UpdateUI();
        }

        private void OnOpenDepositClicked()
        {
            GameManager.Instance.Bank.OpenDeposit("dep_quick", 300);
            UpdateUI();
        }

        private void OnClaimDepositClicked()
        {
            GameManager.Instance.Bank.ClaimDeposit("dep_quick");
            UpdateUI();
        }

        private void OnBuyHouseClicked()
        {
            GameManager.Instance.Assets.BuyAsset("house_hostel");
            UpdateUI();
        }

        private void OnBuyCarClicked()
        {
            GameManager.Instance.Assets.BuyAsset("car_bicycle");
            UpdateUI();
        }

        private void OnWorkStart()
        {
            UpdateWorkButtonState();
        }

        private void OnWorkProgress(float progress)
        {
            if (workProgressBar != null) workProgressBar.fillAmount = progress;
            UpdateWorkButtonState();
        }

        private void OnWorkDone()
        {
            if (workProgressBar != null) workProgressBar.fillAmount = 0f;
            UpdateUI();
        }

        private void OnStudyStart()
        {
            if (studyButton != null) studyButton.interactable = false;
        }

        private void OnStudyProgress(float progress)
        {
            if (studyProgressBar != null) studyProgressBar.fillAmount = progress;
        }

        private void OnStudyDone(string eduId)
        {
            if (studyButton != null) studyButton.interactable = true;
            if (studyProgressBar != null) studyProgressBar.fillAmount = 0f;
            UpdateUI();
        }

        public void UpdateUI()
        {
            if (GameManager.Instance == null || GameManager.Instance.Player == null) return;

            var player = GameManager.Instance.Player;
            var currentJob = GameManager.Instance.Job.GetCurrentJob();
            var edu = GameManager.Instance.Education;
            var biz = GameManager.Instance.Business;
            var bank = GameManager.Instance.Bank;
            var assets = GameManager.Instance.Assets;

            UpdateWorkButtonState();

            if (moneyText != null) moneyText.text = $"{player.money} KZT";
            if (healthText != null) healthText.text = $"Здоровье: {player.health}%";
            if (moodText != null) moodText.text = $"Настроение: {player.mood}%";

            if (dateText != null) dateText.text = $"Месяц {player.month}, День {player.day}";

            if (jobTitleText != null) jobTitleText.text = $"Работа: {currentJob.jobTitle}";
            if (salaryText != null) salaryText.text = $"Зарплата: {currentJob.salary} KZT";

            if (studyTitleText != null)
            {
                bool hasCourses = edu.HasCompleted("edu_courses");
                studyTitleText.text = hasCourses ? "Курсы: Пройдено (500 KZT)" : "Курсы: Не пройдены (500 KZT)";
            }

            if (biz != null)
            {
                var coffeeData = biz.GetBusinessData("biz_coffee");
                if (businessStatusText != null)
                {
                    if (!coffeeData.isPurchased)
                    {
                        businessStatusText.text = "Кофейня: Не куплена (1000 KZT)";
                    }
                    else if (coffeeData.accumulatedIncome >= BusinessService.MaxAccumulatedIncome)
                    {
                        businessStatusText.text = $"Кофемашина | Накоплено: {coffeeData.accumulatedIncome}/1500 KZT | Лимит достигнут";
                    }
                    else
                    {
                        int secondsLeft = Mathf.CeilToInt(biz.GetRemainingCycleSeconds("biz_coffee"));
                        businessStatusText.text = $"Кофемашина | Накоплено: {coffeeData.accumulatedIncome}/1500 KZT | Следующий цикл: {secondsLeft} сек. | Звёзды: {coffeeData.stars}/3";
                    }
                }
                if (buyBusinessButton != null)
                {
                    bool canAfford = biz.Configs.TryGetValue("biz_coffee", out var coffeeConfig) &&
                        player.money >= coffeeConfig.buyCost;
                    buyBusinessButton.interactable = !coffeeData.isPurchased && canAfford;
                }
                if (collectIncomeButton != null)
                    collectIncomeButton.interactable = coffeeData.isPurchased && coffeeData.accumulatedIncome > 0;
            }

            if (bank != null)
            {
                var depData = bank.GetDepositData("dep_quick");
                if (bankStatusText != null)
                {
                    if (depData.isReadyToClaim)
                    {
                        bankStatusText.text = "Вклад готов! Можно забрать с процентами (+10%)";
                    }
                    else if (depData.isActive)
                    {
                        int secondsLeft = Mathf.CeilToInt(depData.timer);
                        bankStatusText.text = $"Вклад активен: осталось {secondsLeft} сек.";
                    }
                    else
                    {
                        bankStatusText.text = "Быстрый вклад: 300 KZT ➔ +10% за 2 мин.";
                    }
                }

                if (openDepositButton != null) openDepositButton.interactable = !depData.isActive && !depData.isReadyToClaim;
                if (claimDepositButton != null) claimDepositButton.interactable = depData.isReadyToClaim;
            }

            // Обновление UI Имущества
            if (assets != null)
            {
                bool hasHostel = assets.HasAsset("house_hostel");
                if (houseStatusText != null) houseStatusText.text = hasHostel ? "Жильё: Койко-место" : "Жильё: Нет";
                if (buyHouseButton != null) buyHouseButton.interactable = !hasHostel;

                bool hasBicycle = assets.HasAsset("car_bicycle");
                if (carStatusText != null) carStatusText.text = hasBicycle ? "Транспорт: Велосипед" : "Транспорт: Нет";
                if (buyCarButton != null) buyCarButton.interactable = !hasBicycle;

                // Статус одежды
                bool hasCasual = assets.HasAsset("wear_casual");
                bool hasSuit = assets.HasAsset("wear_suit");

                if (clothingStatusText != null)
                {
                    if (hasSuit) clothingStatusText.text = "Одежда: Деловой костюм";
                    else if (hasCasual) clothingStatusText.text = "Одежда: Повседневная";
                    else clothingStatusText.text = "Одежда: Старая футболка";
                }

                if (buyCasualClothingButton != null) buyCasualClothingButton.interactable = !hasCasual;
                if (buySuitClothingButton != null) buySuitClothingButton.interactable = !hasSuit;
            }
        }

        private void UpdateWorkButtonState()
        {
            GameManager game = GameManager.Instance;
            if (game == null || game.Job == null) return;

            var job = game.Job;
            if (workButton != null)
            {
                workButton.interactable = !job.IsWorking &&
                    (job.IsSalaryReadyToClaim || (game.Needs != null && game.Needs.CanWork()));
            }

            if (_workButtonLabel == null) return;

            if (job.IsWorking)
            {
                _workButtonLabel.text = $"Работа: осталось {Mathf.CeilToInt(job.RemainingWorkSeconds)} сек.";
            }
            else if (job.IsSalaryReadyToClaim)
            {
                _workButtonLabel.text = $"Получить зарплату ({game.Player.workTimer.salary} KZT)";
            }
            else
            {
                _workButtonLabel.text = "Начать работу";
            }
        }
    }
}
