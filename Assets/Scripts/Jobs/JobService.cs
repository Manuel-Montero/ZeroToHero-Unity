using System;
using System.Collections.Generic;
using UnityEngine;
using ZeroToHero.Assets;
using ZeroToHero.Education;
using ZeroToHero.Player;
using ZeroToHero.Core;

namespace ZeroToHero.Jobs
{
    public class JobService
    {
        private PlayerData _player;
        private EconomyService _economy;

        public Dictionary<string, JobConfig> JobsConfig { get; private set; } 
            = new Dictionary<string, JobConfig>();

        private float _currentWorkTimer = 0f;
        private bool _isWorking = false;

        public event Action OnWorkStarted;
        public event Action<float> OnTimerTick;
        public event Action OnWorkCompleted;

        public JobService(PlayerData player, EconomyService economy)
        {
            _player = player;
            _economy = economy;

            InitDefaultJobs();
        }

        private void InitDefaultJobs()
        {
            var courier = new JobConfig
            {
                id = "job_courier",
                jobTitle = "Курьер",
                salary = 100,
                workDurationInSeconds = 3f,
                requiredEducationId = null,
                requiredHouseId = null,
                requiredCarId = null,
                requiredClothingId = null
            };

            var waiter = new JobConfig
            {
                id = "job_waiter",
                jobTitle = "Официант",
                salary = 250,
                workDurationInSeconds = 5f,
                requiredEducationId = "edu_courses",
                requiredHouseId = null,
                requiredCarId = null,
                requiredClothingId = "wear_casual" // Нужно купить «Повседневную одежду»
            };

            var manager = new JobConfig
            {
                id = "job_manager",
                jobTitle = "Младший Менеджер",
                salary = 600,
                workDurationInSeconds = 7f,
                requiredEducationId = "edu_courses",
                requiredHouseId = "house_hostel",
                requiredCarId = "car_bicycle",
                requiredClothingId = "wear_suit"   // Нужен «Деловой костюм»
            };

            JobsConfig.Add(courier.id, courier);
            JobsConfig.Add(waiter.id, waiter);
            JobsConfig.Add(manager.id, manager);
        }

        public JobConfig GetCurrentJob()
        {
            if (JobsConfig.TryGetValue(_player.currentJobId, out var job))
            {
                return job;
            }
            return JobsConfig["job_courier"];
        }

        // Проверка выполнения ВСЕХ условий для найма
        public bool CanPromoteTo(string jobId, EducationService eduService, AssetService assetService)
        {
            if (!JobsConfig.TryGetValue(jobId, out var targetJob)) return false;

            // 1. Образование
            if (!string.IsNullOrEmpty(targetJob.requiredEducationId))
            {
                if (eduService == null || !eduService.HasCompleted(targetJob.requiredEducationId)) 
                    return false;
            }

            // 2. Жилье
            if (!string.IsNullOrEmpty(targetJob.requiredHouseId))
            {
                if (assetService == null || !assetService.HasAsset(targetJob.requiredHouseId)) 
                    return false;
            }

            // 3. Транспорт
            if (!string.IsNullOrEmpty(targetJob.requiredCarId))
            {
                if (assetService == null || !assetService.HasAsset(targetJob.requiredCarId)) 
                    return false;
            }

            // 4. Одежда
            if (!string.IsNullOrEmpty(targetJob.requiredClothingId))
            {
                if (assetService == null || !assetService.HasAsset(targetJob.requiredClothingId)) 
                    return false;
            }

            return true;
        }

        public bool PromoteToJob(string jobId, EducationService eduService, AssetService assetService)
        {
            if (CanPromoteTo(jobId, eduService, assetService))
            {
                _player.currentJobId = jobId;
                Debug.Log($"[JobService] Поздравляем! Вы устроились на должность: {JobsConfig[jobId].jobTitle}");
                return true;
            }

            Debug.Log("[JobService] Не выполнены все условия для повышения!");
            return false;
        }

        public void StartWork()
        {
            if (_isWorking) return;

            var job = GetCurrentJob();
            _isWorking = true;
            _currentWorkTimer = job.workDurationInSeconds;

            OnWorkStarted?.Invoke();
        }

        public void UpdateTimer(float deltaTime)
        {
            if (!_isWorking) return;

            _currentWorkTimer -= deltaTime;
            var job = GetCurrentJob();

            float progress = 1f - (_currentWorkTimer / job.workDurationInSeconds);
            OnTimerTick?.Invoke(progress);

            if (_currentWorkTimer <= 0f)
            {
                _isWorking = false;
                _economy.AddMoney(job.salary, $"Зарплата: {job.jobTitle}");
                OnWorkCompleted?.Invoke();
            }
        }
    }
}