using System;
using System.Collections.Generic;
using UnityEngine;
using ZeroToHero.Core;
using ZeroToHero.Player;

namespace ZeroToHero.Education
{
    public class EducationService
    {
        private PlayerData _player;
        private EconomyService _economy;

        public Dictionary<string, EducationConfig> AvailableEducations { get; private set; } 
            = new Dictionary<string, EducationConfig>();

        public bool IsStudying { get; private set; }
        public string CurrentStudyingId { get; private set; }
        public float CurrentTimer { get; private set; }
        public float CurrentDuration { get; private set; }

        public event Action OnStudyStarted;
        public event Action<float> OnStudyProgress;
        public event Action<string> OnStudyCompleted;

        public EducationService(PlayerData player, EconomyService economy)
        {
            _player = player;
            _economy = economy;

            InitDefaultEducations();
        }

        private void InitDefaultEducations()
        {
            var courses = new EducationConfig
            {
                id = "edu_courses",
                title = "Курсы курьера / сервис",
                cost = 500,
                durationInSeconds = 5.0f
            };

            var college = new EducationConfig
            {
                id = "edu_college",
                title = "Колледж (Сервис и общепит)",
                cost = 2000,
                durationInSeconds = 10.0f
            };

            AvailableEducations.Add(courses.id, courses);
            AvailableEducations.Add(college.id, college);
        }

        public bool HasCompleted(string eduId)
        {
            return _player.completedEducation != null && _player.completedEducation.Contains(eduId);
        }

        public bool StartStudy(string eduId)
        {
            if (IsStudying) return false;
            if (HasCompleted(eduId)) return false;
            if (!AvailableEducations.TryGetValue(eduId, out var edu)) return false;

            // Проверяем, хватает ли денег
            if (!_economy.SpendMoney(edu.cost, $"Education: {edu.title}"))
            {
                Debug.Log("[EducationService] Недостаточно средств для обучения!");
                return false;
            }

            IsStudying = true;
            CurrentStudyingId = eduId;
            CurrentTimer = 0f;
            CurrentDuration = edu.durationInSeconds;

            OnStudyStarted?.Invoke();
            return true;
        }

        public void UpdateTimer(float deltaTime)
        {
            if (!IsStudying) return;

            CurrentTimer += deltaTime;
            float progress = Mathf.Clamp01(CurrentTimer / CurrentDuration);
            OnStudyProgress?.Invoke(progress);

            if (CurrentTimer >= CurrentDuration)
            {
                CompleteStudy();
            }
        }

        private void CompleteStudy()
        {
            IsStudying = false;

            if (_player.completedEducation == null)
            {
                _player.completedEducation = new List<string>();
            }

            if (!_player.completedEducation.Contains(CurrentStudyingId))
            {
                _player.completedEducation.Add(CurrentStudyingId);
            }

            string completedId = CurrentStudyingId;
            CurrentStudyingId = null;

            Debug.Log($"[EducationService] Обучение completed: {completedId}");
            OnStudyCompleted?.Invoke(completedId);
        }
    }
}