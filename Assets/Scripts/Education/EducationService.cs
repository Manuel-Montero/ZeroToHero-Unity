using System;
using System.Collections.Generic;
using UnityEngine;
using ZeroToHero.Core;
using ZeroToHero.Player;

namespace ZeroToHero.Education
{
    public class EducationService
    {
        private readonly PlayerData _player;
        private readonly EconomyService _economy;
        private readonly TimeManager _time;

        public Dictionary<string, EducationConfig> AvailableEducations { get; private set; } 
            = new Dictionary<string, EducationConfig>();

        public bool IsStudying => _player.studyTimer != null && _player.studyTimer.isActive;
        public string CurrentStudyingId => IsStudying ? _player.studyTimer.educationId : null;
        public float CurrentTimer { get; private set; }
        public float CurrentDuration { get; private set; }

        public event Action OnStudyStarted;
        public event Action<float> OnStudyProgress;
        public event Action<string> OnStudyCompleted;

        public EducationService(PlayerData player, EconomyService economy, TimeManager time = null)
        {
            _player = player;
            _economy = economy;
            _time = time;

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
            if (IsStudying || _time == null) return false;
            if (HasCompleted(eduId)) return false;
            if (!AvailableEducations.TryGetValue(eduId, out var edu)) return false;

            // Проверяем, хватает ли денег
            if (!_economy.SpendMoney(edu.cost, $"Education: {edu.title}"))
            {
                Debug.Log("[EducationService] Недостаточно средств для обучения!");
                return false;
            }

            _player.studyTimer.isActive = true;
            _player.studyTimer.educationId = eduId;
            _player.studyTimer.durationSeconds = edu.durationInSeconds;
            _player.studyTimer.endUtcTicks = _time.CreateDeadlineTicks(edu.durationInSeconds);
            CurrentTimer = 0f;
            CurrentDuration = edu.durationInSeconds;

            OnStudyStarted?.Invoke();
            return true;
        }

        public void UpdateTimer(float deltaTime)
        {
            if (!IsStudying || _time == null) return;

            var timer = _player.studyTimer;
            float remaining = _time.GetRemainingSeconds(timer.endUtcTicks);
            CurrentDuration = timer.durationSeconds;
            CurrentTimer = Mathf.Max(0f, CurrentDuration - remaining);
            float progress = Mathf.Clamp01(CurrentTimer / CurrentDuration);
            OnStudyProgress?.Invoke(progress);

            if (remaining <= 0f)
            {
                CompleteStudy();
            }
        }

        private void CompleteStudy()
        {
            string completedId = _player.studyTimer.educationId;
            _player.studyTimer.isActive = false;
            _player.studyTimer.educationId = null;
            _player.studyTimer.endUtcTicks = 0;

            if (_player.completedEducation == null)
            {
                _player.completedEducation = new List<string>();
            }

            if (!_player.completedEducation.Contains(completedId))
            {
                _player.completedEducation.Add(completedId);
            }

            Debug.Log($"[EducationService] Обучение completed: {completedId}");
            OnStudyCompleted?.Invoke(completedId);
        }
    }
}
