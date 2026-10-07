using System;
using System.Collections.Generic;
using UnityEngine;
using ZeroToHero.Core;
using ZeroToHero.Player;

namespace ZeroToHero.Jobs
{
    public class JobService
    {
        private EconomyService _economy;
        private PlayerData _player;

        public Dictionary<string, JobConfig> AvailableJobs { get; private set; } = new Dictionary<string, JobConfig>();
        
        public bool IsWorking { get; private set; }
        public float CurrentTimer { get; private set; }
        public float CurrentJobDuration { get; private set; }

        public event Action OnWorkStarted;
        public event Action OnWorkCompleted;
        public event Action<float> OnTimerTick;

        public JobService(PlayerData player, EconomyService economy)
        {
            _player = player;
            _economy = economy;

            InitDefaultJobs();
        }
        
        private void InitDefaultJobs()
        {
                // Для Курьера образование не требуется
            var courier = new JobConfig 
            { 
                id = "job_courier", 
                jobTitle = "Курьер", 
                salary = 100, 
                durationInSeconds = 2.5f,
                requiredEducationId = ""
            };

            // Для Официанта требуются "Курсы" (edu_courses)
            var waiter = new JobConfig 
            { 
                id = "job_waiter", 
                jobTitle = "Официант", 
                salary = 350, 
                durationInSeconds = 4.0f,
                requiredEducationId = "edu_courses"
            };

            AvailableJobs.Add(courier.id, courier);
            AvailableJobs.Add(waiter.id, waiter);
        }

        // Повышение с проверкой образования
        public bool PromoteToJob(string newJobId, Education.EducationService educationService)
        {
            if (!AvailableJobs.TryGetValue(newJobId, out var job)) return false;

            // Если для работы требуется образование, проверяем, пройдено ли оно
            if (!string.IsNullOrEmpty(job.requiredEducationId))
            {
                if (educationService == null || !educationService.HasCompleted(job.requiredEducationId))
                {
                    Debug.Log($"[JobService] Для работы '{job.jobTitle}' необходимо сначала пройти обучение!");
                    return false;
                }
            }

            _player.currentJobId = newJobId;
            Debug.Log($"[JobService] Успешное повышение! Новая должность: {job.jobTitle}");
            return true;
        }

        public JobConfig GetCurrentJob()
        {
            if (AvailableJobs.TryGetValue(_player.currentJobId, out var job))
                return job;

            return AvailableJobs["job_courier"];
        }

        public void StartWork()
        {
            if (IsWorking) return;

            JobConfig current = GetCurrentJob();
            IsWorking = true;
            CurrentTimer = 0f;
            CurrentJobDuration = current.durationInSeconds;

            OnWorkStarted?.Invoke();
        }

        public void UpdateTimer(float deltaTime)
        {
            if (!IsWorking) return;

            CurrentTimer += deltaTime;
            float progress = Mathf.Clamp01(CurrentTimer / CurrentJobDuration);
            OnTimerTick?.Invoke(progress);

            if (CurrentTimer >= CurrentJobDuration)
            {
                IsWorking = false;
                CompleteWork();
            }
        }

        private void CompleteWork()
        {
            JobConfig current = GetCurrentJob();
            _economy.AddMoney(current.salary, $"Job: {current.jobTitle}");
            OnWorkCompleted?.Invoke();
        }

        public bool PromoteToJob(string newJobId)
        {
            if (!AvailableJobs.ContainsKey(newJobId)) return false;
            
            _player.currentJobId = newJobId;
            return true;
        }
    }
}