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

        private void Start()
        {
            if (GameManager.Instance != null && GameManager.Instance.Job != null)
            {
                GameManager.Instance.Job.OnTimerTick += OnWorkProgress;
                GameManager.Instance.Job.OnWorkCompleted += OnWorkDone;
                GameManager.Instance.Job.OnWorkStarted += OnWorkStart;
            }

            if (workButton != null)
            {
                workButton.onClick.AddListener(OnWorkButtonClicked);
            }

            UpdateUI();
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null && GameManager.Instance.Job != null)
            {
                GameManager.Instance.Job.OnTimerTick -= OnWorkProgress;
                GameManager.Instance.Job.OnWorkCompleted -= OnWorkDone;
                GameManager.Instance.Job.OnWorkStarted -= OnWorkStart;
            }
        }

        private void OnWorkButtonClicked()
        {
            GameManager.Instance.Job.StartWork();
        }

        private void OnWorkStart()
        {
            if (workButton != null) workButton.interactable = false;
        }

        private void OnWorkProgress(float progress)
        {
            if (workProgressBar != null)
            {
                workProgressBar.fillAmount = progress;
            }
        }

        private void OnWorkDone()
        {
            if (workButton != null) workButton.interactable = true;
            if (workProgressBar != null) workProgressBar.fillAmount = 0f;
            
            UpdateUI();
        }

        public void UpdateUI()
        {
            if (GameManager.Instance == null || GameManager.Instance.Player == null) return;

            var player = GameManager.Instance.Player;
            var currentJob = GameManager.Instance.Job.GetCurrentJob();

            if (moneyText != null) moneyText.text = $"{player.money} KZT";
            if (healthText != null) healthText.text = $"Здоровье: {player.health}%";
            if (moodText != null) moodText.text = $"Настроение: {player.mood}%";

            if (jobTitleText != null) jobTitleText.text = $"Работа: {currentJob.jobTitle}";
            if (salaryText != null) salaryText.text = $"Зарплата: {currentJob.salary} KZT";
        }
    }
}