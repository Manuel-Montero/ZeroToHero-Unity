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
        private readonly PlayerData _player;
        private readonly EconomyService _economy;
        private readonly TimeManager _time;

        public Dictionary<string, JobConfig> JobsConfig { get; private set; } 
            = new Dictionary<string, JobConfig>();

        public bool IsWorking => _player.workTimer != null && _player.workTimer.isActive;

        public event Action OnWorkStarted;
        public event Action<float> OnTimerTick;
        public event Action OnWorkCompleted;

        public JobService(PlayerData player, EconomyService economy, TimeManager time = null)
        {
            _player = player;
            _economy = economy;
            _time = time;

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
            if (IsWorking || _time == null) return;

            var job = GetCurrentJob();
            _player.workTimer.isActive = true;
            _player.workTimer.jobId = job.id;
            _player.workTimer.salary = job.salary;
            _player.workTimer.durationSeconds = job.workDurationInSeconds;
            _player.workTimer.endUtcTicks = _time.CreateDeadlineTicks(job.workDurationInSeconds);

            OnWorkStarted?.Invoke();
        }

        public void UpdateTimer(float deltaTime)
        {
            if (!IsWorking || _time == null) return;

            var timer = _player.workTimer;
            float remaining = _time.GetRemainingSeconds(timer.endUtcTicks);
            float progress = timer.durationSeconds <= 0f ? 1f : 1f - (remaining / timer.durationSeconds);
            OnTimerTick?.Invoke(progress);

            if (remaining <= 0f)
            {
                string jobTitle = JobsConfig.TryGetValue(timer.jobId, out var completedJob)
                    ? completedJob.jobTitle : timer.jobId;
                int salary = timer.salary;
                if (!_economy.AddMoney(salary, $"Зарплата: {jobTitle}"))
                    return;

                timer.isActive = false;
                OnWorkCompleted?.Invoke();
            }
        }
    }
}
