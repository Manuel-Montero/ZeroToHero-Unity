using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZeroToHero.Core;

namespace ZeroToHero.UI
{
    public class MainUI : MonoBehaviour
    {
        [Header("Status Bars")]
        public TMP_Text moneyText;
        public TMP_Text healthText;
        public TMP_Text moodText;

        [Header("Job Panel")]
        public TMP_Text jobTitleText;
        public TMP_Text salaryText;
        public Button workButton;
        public Image workProgressBar;
        public Button promoteWaiterButton;

        [Header("Education Panel")]
        public TMP_Text studyTitleText;
        public Button studyButton;
        public Image studyProgressBar;

        [Header("Needs Panel")]
        public Button healButton; // Кнопка "Лечиться" (+20 HP за 50 KZT)
        public Button restButton; // Кнопка "Отдохнуть" (+20 Mood за 50 KZT)

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                if (GameManager.Instance.Job != null)
                {
                    GameManager.Instance.Job.OnTimerTick += OnWorkProgress;
                    GameManager.Instance.Job.OnWorkCompleted += OnWorkDone;
                    GameManager.Instance.Job.OnWorkStarted += OnWorkStart;
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
            }

            if (workButton != null) workButton.onClick.AddListener(OnWorkButtonClicked);
            if (studyButton != null) studyButton.onClick.AddListener(OnStudyButtonClicked);
            if (promoteWaiterButton != null) promoteWaiterButton.onClick.AddListener(OnPromoteWaiterClicked);
            
            if (healButton != null) healButton.onClick.AddListener(OnHealClicked);
            if (restButton != null) restButton.onClick.AddListener(OnRestClicked);

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
            }
        }

        private void OnWorkButtonClicked()
        {
            if (!GameManager.Instance.Needs.CanWork())
            {
                Debug.Log("[MainUI] Слишком низкое здоровье или настроение! Полечитесь или отдохните.");
                return;
            }
            GameManager.Instance.Job.StartWork();
        }

        private void OnStudyButtonClicked()
        {
            GameManager.Instance.Education.StartStudy("edu_courses");
            UpdateUI();
        }

        private void OnPromoteWaiterClicked()
        {
            GameManager.Instance.Job.PromoteToJob("job_waiter", GameManager.Instance.Education);
            UpdateUI();
        }

        private void OnHealClicked()
        {
            GameManager.Instance.Needs.Heal(20, 50); // +20 HP за 50 KZT
            UpdateUI();
        }

        private void OnRestClicked()
        {
            GameManager.Instance.Needs.Rest(20, 50); // +20 Mood за 50 KZT
            UpdateUI();
        }

        private void OnWorkStart()
        {
            if (workButton != null) workButton.interactable = false;
        }

        private void OnWorkProgress(float progress)
        {
            if (workProgressBar != null) workProgressBar.fillAmount = progress;
        }

        private void OnWorkDone()
        {
            if (workButton != null) workButton.interactable = true;
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

            if (moneyText != null) moneyText.text = $"{player.money} KZT";
            if (healthText != null) healthText.text = $"Здоровье: {player.health}%";
            if (moodText != null) moodText.text = $"Настроение: {player.mood}%";

            if (jobTitleText != null) jobTitleText.text = $"Работа: {currentJob.jobTitle}";
            if (salaryText != null) salaryText.text = $"Зарплата: {currentJob.salary} KZT";

            if (studyTitleText != null)
            {
                bool hasCourses = edu.HasCompleted("edu_courses");
                studyTitleText.text = hasCourses 
                    ? "Курсы: Пройдено (500 KZT)" 
                    : "Курсы: Не пройдены (500 KZT)";
            }
        }
    }
}