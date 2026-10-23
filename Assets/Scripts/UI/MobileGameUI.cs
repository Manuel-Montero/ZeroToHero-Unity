using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZeroToHero.Assets;
using ZeroToHero.Bank;
using ZeroToHero.Business;
using ZeroToHero.Core;
using ZeroToHero.Education;
using ZeroToHero.Jobs;
using ZeroToHero.Player;

namespace ZeroToHero.UI
{
    /// <summary>Runtime-built, portrait-first UI bound directly to the game's services.</summary>
    public sealed class MobileGameUI : MonoBehaviour
    {
        private enum Page { Home, Work, Business, Assets, Shop, Wallet, Education, Bank, Needs }

        private static readonly Color Background = new Color32(13, 20, 34, 255);
        private static readonly Color Surface = new Color32(25, 35, 53, 255);
        private static readonly Color SurfaceAlt = new Color32(34, 47, 68, 255);
        private static readonly Color Accent = new Color32(78, 211, 157, 255);
        private static readonly Color Muted = new Color32(162, 178, 198, 255);
        private static readonly Color Warn = new Color32(245, 184, 91, 255);

        private readonly List<Action> _liveBindings = new List<Action>();
        private GameManager _game;
        private RectTransform _safeRoot;
        private RectTransform _content;
        private TMP_Text _balanceLabel;
        private TMP_Text _dayLabel;
        private TMP_InputField _depositAmountInput;
        private Page _page = Page.Home;
        private float _nextRefresh;
        private int _safeAreaWidth;
        private int _safeAreaHeight;
        private Rect _lastSafeArea;
        private bool _initialized;

        private void Start() => Initialize();

        public void Initialize()
        {
            if (_initialized) return;
            _game = GameManager.Instance;
            if (_game == null || _game.Player == null)
            {
                Debug.LogError("[MobileGameUI] Игровые сервисы не готовы; мобильный интерфейс не создан.");
                enabled = false;
                return;
            }

            BuildShell();
            if (!enabled) return;
            Subscribe();
            RenderPage();
            _initialized = true;
        }

        private void OnDestroy()
        {
            if (_game == null) return;
            if (_game.Economy != null) _game.Economy.OnBalanceChanged -= OnBalanceChanged;
            if (_game.Job != null)
            {
                _game.Job.OnWorkStarted -= OnDataChanged;
                _game.Job.OnWorkCompleted -= OnDataChanged;
                _game.Job.OnSalaryClaimed -= OnDataChanged;
            }
            if (_game.Education != null)
            {
                _game.Education.OnStudyStarted -= OnDataChanged;
                _game.Education.OnStudyCompleted -= OnStudyCompleted;
            }
            if (_game.Business != null) _game.Business.OnBusinessUpdated -= OnDataChanged;
            if (_game.Bank != null) _game.Bank.OnBankUpdated -= OnDataChanged;
            if (_game.Assets != null) _game.Assets.OnAssetsUpdated -= OnDataChanged;
            if (_game.Needs != null) _game.Needs.OnNeedsChanged -= OnDataChanged;
            if (_game.TimeSystem != null) _game.TimeSystem.OnDayPassed -= OnDataChanged;
        }

        private void Update()
        {
            if (_safeRoot != null && (Screen.width != _safeAreaWidth || Screen.height != _safeAreaHeight || Screen.safeArea != _lastSafeArea))
                ApplySafeArea();

            for (int i = 0; i < _liveBindings.Count; i++) _liveBindings[i]?.Invoke();

            // Keep the header responsive if another system changes the player model.
            if (Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + 1f;
                UpdateHeader();
            }
        }

        private void Subscribe()
        {
            _game.Economy.OnBalanceChanged += OnBalanceChanged;
            _game.Job.OnWorkStarted += OnDataChanged;
            _game.Job.OnWorkCompleted += OnDataChanged;
            _game.Job.OnSalaryClaimed += OnDataChanged;
            _game.Education.OnStudyStarted += OnDataChanged;
            _game.Education.OnStudyCompleted += OnStudyCompleted;
            _game.Business.OnBusinessUpdated += OnDataChanged;
            _game.Bank.OnBankUpdated += OnDataChanged;
            _game.Assets.OnAssetsUpdated += OnDataChanged;
            _game.Needs.OnNeedsChanged += OnDataChanged;
            _game.TimeSystem.OnDayPassed += OnDataChanged;
        }

        private void OnDataChanged() => RenderPage();
        private void OnBalanceChanged(int _, string __) => RenderPage();
        private void OnStudyCompleted(string _) => RenderPage();

        private void BuildShell()
        {
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[MobileGameUI] На MainCanvas не найден компонент Canvas.");
                enabled = false;
                return;
            }

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 1920f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            GameObject root = new GameObject("MobileGameUI", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canvas.transform, false);
            root.transform.SetAsLastSibling();
            _safeRoot = root.GetComponent<RectTransform>();
            ApplySafeArea();
            root.GetComponent<Image>().color = Background;

            RectTransform header = CreateRect("Header", _safeRoot);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, 180f);
            header.anchoredPosition = Vector2.zero;

            Text(header, "ZERO TO HERO", 18, Accent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(36f, -22f), new Vector2(-36f, -54f), FontStyles.Bold, TextAlignmentOptions.Left);
            _balanceLabel = Text(header, "0 KZT", 36, Color.white, new Vector2(0f, 1f), new Vector2(0.68f, 1f), new Vector2(36f, -58f), new Vector2(-12f, -111f), FontStyles.Bold, TextAlignmentOptions.Left);
            _balanceLabel.raycastTarget = true;
            Button walletButton = _balanceLabel.gameObject.AddComponent<Button>();
            walletButton.targetGraphic = _balanceLabel;
            walletButton.onClick.AddListener(() => Show(Page.Wallet));
            _dayLabel = Text(header, "", 15, Muted, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(36f, -120f), new Vector2(-36f, -157f), FontStyles.Normal, TextAlignmentOptions.Left);

            RectTransform body = CreateRect("Body", _safeRoot);
            body.anchorMin = Vector2.zero;
            body.anchorMax = Vector2.one;
            body.offsetMin = new Vector2(20f, 104f);
            body.offsetMax = new Vector2(-20f, -184f);
            body.gameObject.AddComponent<RectMask2D>();

            ScrollRect scroll = body.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 70f;
            _content = CreateRect("Content", body);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.padding = new RectOffset(0, 0, 8, 28);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = _content;
            scroll.viewport = body;
            scroll.vertical = true;

            RectTransform nav = CreateRect("BottomNavigation", _safeRoot);
            nav.anchorMin = new Vector2(0f, 0f);
            nav.anchorMax = new Vector2(1f, 0f);
            nav.pivot = new Vector2(0.5f, 0f);
            nav.sizeDelta = new Vector2(0f, 100f);
            nav.anchoredPosition = Vector2.zero;
            Image navImage = nav.gameObject.AddComponent<Image>();
            navImage.color = Surface;
            HorizontalLayoutGroup navLayout = nav.gameObject.AddComponent<HorizontalLayoutGroup>();
            navLayout.padding = new RectOffset(8, 8, 10, 12);
            navLayout.spacing = 2f;
            navLayout.childControlWidth = true;
            navLayout.childControlHeight = true;
            navLayout.childForceExpandWidth = true;
            navLayout.childForceExpandHeight = true;
            AddNav(nav, "⌂\nГлавная", Page.Home);
            AddNav(nav, "◷\nРабота", Page.Work);
            AddNav(nav, "▣\nБизнес", Page.Business);
            AddNav(nav, "◆\nАктивы", Page.Assets);
            AddNav(nav, "＋\nМагазин", Page.Shop);
        }

        private void ApplySafeArea()
        {
            if (_safeRoot == null) return;
            Rect area = Screen.safeArea;
            _safeAreaWidth = Screen.width;
            _safeAreaHeight = Screen.height;
            _lastSafeArea = area;
            _safeRoot.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            _safeRoot.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            _safeRoot.offsetMin = Vector2.zero;
            _safeRoot.offsetMax = Vector2.zero;
        }

        private void AddNav(Transform parent, string label, Page page)
        {
            Button button = MakeButton(parent, label, Surface, 56f, () => Show(page), 14);
            button.GetComponent<LayoutElement>().flexibleWidth = 1f;
        }

        private void Show(Page page)
        {
            _page = page;
            RenderPage();
        }

        private void RenderPage()
        {
            if (_content == null || _game == null || _game.Player == null) return;
            _liveBindings.Clear();
            _depositAmountInput = null;
            for (int i = _content.childCount - 1; i >= 0; i--) DestroyGenerated(_content.GetChild(i).gameObject);
            UpdateHeader();

            switch (_page)
            {
                case Page.Home: RenderHome(); break;
                case Page.Work: RenderWork(); break;
                case Page.Business: RenderBusinesses(); break;
                case Page.Assets: RenderAssets(); break;
                case Page.Shop: RenderShop(); break;
                case Page.Wallet: RenderWallet(); break;
                case Page.Education: RenderEducation(); break;
                case Page.Bank: RenderBank(); break;
                case Page.Needs: RenderNeeds(); break;
            }
        }

        private void UpdateHeader()
        {
            if (_game == null || _game.Player == null) return;
            if (_balanceLabel != null) _balanceLabel.text = $"{_game.Player.money:N0} KZT";
            if (_dayLabel != null) _dayLabel.text = $"Месяц {_game.Player.month}  ·  День {_game.Player.day}     Здоровье {_game.Player.health}%  ·  Настроение {_game.Player.mood}%";
        }

        private void RenderHome()
        {
            PlayerData player = _game.Player;
            Card("ТВОЙ ПУТЬ", "Начни со смены курьера, получи зарплату и открой новые возможности.");
            ButtonCard("Кошелёк", WalletSummary(), () => Show(Page.Wallet), Accent);

            if (_game.Job.IsWorking)
            {
                TMP_Text timer = Card("Смена в процессе", $"{_game.Job.GetCurrentJob().jobTitle} · {_game.Job.RemainingWorkSeconds:0} сек.");
                _liveBindings.Add(() => { if (timer != null) timer.text = $"{_game.Job.GetCurrentJob().jobTitle} · {Mathf.CeilToInt(_game.Job.RemainingWorkSeconds)} сек."; });
                ActionButton("Открыть работу", () => Show(Page.Work));
            }
            else if (_game.Job.IsSalaryReadyToClaim)
            {
                Card("Смена завершена", $"К получению: {_game.Player.workTimer.salary:N0} KZT");
                ActionButton("Получить зарплату", () => { _game.Job.ClaimSalary(); RenderPage(); });
            }
            else
            {
                ButtonCard("Работа", $"{_game.Job.GetCurrentJob().jobTitle} · {_game.Job.GetCurrentJob().salary:N0} KZT за смену", () => Show(Page.Work));
            }

            BusinessData business = _game.Business.GetBusinessData("biz_coffee");
            ButtonCard("Бизнес", business.isPurchased
                ? $"Накоплено {_game.Business.GetBusinessData("biz_coffee").accumulatedIncome:N0} / {BusinessService.MaxAccumulatedIncome:N0} KZT"
                : "Кофейный автомат · покупка по игровому конфигу", () => Show(Page.Business));

            Card("Состояние", $"Здоровье  {player.health}%     Настроение  {player.mood}%");
            TMP_Text timerSummary = Card("Активные действия", GetActiveTimersText());
            _liveBindings.Add(() => { if (timerSummary != null) timerSummary.text = GetActiveTimersText(); });
            ButtonCard("Образование", GetEducationSummary(), () => Show(Page.Education));
            ButtonCard("Банк", GetBankSummary(), () => Show(Page.Bank));
            ButtonCard("Имущество", GetOwnedAssetsSummary(), () => Show(Page.Assets));
            ActionButton("Забота о себе", () => Show(Page.Needs), SurfaceAlt);
        }

        private void RenderWork()
        {
            JobConfig current = _game.Job.GetCurrentJob();
            Card("Работа", "Выполни смену и забери зарплату вручную.");
            TMP_Text status = Card(current.jobTitle, $"Зарплата  {current.salary:N0} KZT");
            if (_game.Job.IsWorking)
            {
                status.text = $"Осталось {Mathf.CeilToInt(_game.Job.RemainingWorkSeconds)} сек. · зарплата {_game.Player.workTimer.salary:N0} KZT";
                _liveBindings.Add(() => { if (status != null) status.text = $"Осталось {Mathf.CeilToInt(_game.Job.RemainingWorkSeconds)} сек. · зарплата {_game.Player.workTimer.salary:N0} KZT"; });
                ActionButton("Смена выполняется", null, SurfaceAlt, false);
            }
            else if (_game.Job.IsSalaryReadyToClaim)
            {
                Card("Зарплата ожидает получения", $"{_game.Player.workTimer.salary:N0} KZT");
                ActionButton("Получить зарплату", () => _game.Job.ClaimSalary());
            }
            else
            {
                ActionButton(_game.Needs.CanWork() ? "Начать смену" : "Сейчас нельзя начать смену", () => _game.Job.StartWork(), Accent, _game.Needs.CanWork());
            }

            SectionTitle("Следующие должности");
            foreach (JobConfig job in _game.Job.JobsConfig.Values)
            {
                if (job.id == current.id) continue;
                bool canTake = _game.Job.CanPromoteTo(job.id, _game.Education, _game.Assets);
                Card(job.jobTitle, $"Зарплата  {job.salary:N0} KZT за смену\n{DescribeJobRequirements(job)}");
                ActionButton(canTake ? "Перейти на должность" : "Требования не выполнены",
                    canTake ? () =>
                    {
                        if (_game.Job.PromoteToJob(job.id, _game.Education, _game.Assets)) _game.SaveGame();
                        RenderPage();
                    } : null,
                    canTake ? Accent : SurfaceAlt, canTake && !_game.Job.IsWorking && !_game.Job.IsSalaryReadyToClaim);
            }
            ButtonCard("Образование", GetEducationSummary(), () => Show(Page.Education));
            ButtonCard("Магазин имущества", "Одежда, жильё и транспорт из игровых конфигов", () => Show(Page.Shop));
        }

        private void RenderBusinesses()
        {
            SectionTitle("Мой бизнес");
            foreach (BusinessConfig config in _game.Business.Configs.Values)
            {
                BusinessData data = _game.Business.GetBusinessData(config.id);
                if (!data.isPurchased)
                {
                    Card(config.title, $"Покупка  {config.buyCost:N0} KZT\nДоход за цикл  {config.baseIncome:N0} KZT");
                    ActionButton("Купить бизнес", () => _game.Business.BuyBusiness(config.id), Accent,
                        _game.Player.money >= config.buyCost);
                    continue;
                }

                TMP_Text details = Card(config.title,
                    $"Накоплено  {data.accumulatedIncome:N0} / {BusinessService.MaxAccumulatedIncome:N0} KZT\n" +
                    $"Доход за цикл  {config.baseIncome:N0} KZT\nСборов  {data.collectCount}  ·  Звёзды  {data.stars}/3");
                _liveBindings.Add(() =>
                {
                    BusinessData live = _game.Business.GetBusinessData(config.id);
                    if (details != null) details.text = $"Накоплено  {live.accumulatedIncome:N0} / {BusinessService.MaxAccumulatedIncome:N0} KZT\n" +
                        $"Доход за цикл  {config.baseIncome:N0} KZT\nСборов  {live.collectCount}  ·  Звёзды  {live.stars}/3\n" +
                        (live.accumulatedIncome >= BusinessService.MaxAccumulatedIncome ? "Лимит достигнут" : $"До следующего цикла  {Mathf.CeilToInt(_game.Business.GetRemainingCycleSeconds(config.id))} сек.");
                });
                ActionButton(data.accumulatedIncome > 0 ? $"Собрать {data.accumulatedIncome:N0} KZT" : "Доход накапливается",
                    () => _game.Business.CollectIncome(config.id), Accent, data.accumulatedIncome > 0);
                Card("Менеджер", data.hasManager ? "Менеджер нанят" : "Найм менеджера пока не подключён к игровому сервису.", Warn);
            }
        }

        private void RenderAssets()
        {
            SectionTitle("Моё имущество");
            List<AssetConfig> owned = _game.Assets.Configs.Values.Where(item => _game.Assets.HasAsset(item.id)).ToList();
            if (owned.Count == 0) Card("Пока пусто", "Приобретённое имущество появится здесь.", Muted);
            foreach (AssetConfig item in owned) Card(item.title, AssetTypeName(item.type) + " · приобретено");
            Card("Текущая работа", _game.Job.GetCurrentJob().jobTitle);
            ActionButton("Перейти в магазин", () => Show(Page.Shop));
        }

        private void RenderShop()
        {
            SectionTitle("Магазин имущества");
            Card("Предложения", "Покупки выполняются через AssetService по текущим игровым конфигам.");
            List<AssetConfig> available = _game.Assets.Configs.Values.Where(item => !_game.Assets.HasAsset(item.id)).ToList();
            if (available.Count == 0) Card("Всё приобретено", "Все доступные предметы из конфигурации уже есть у игрока.", Accent);
            foreach (AssetConfig item in available)
            {
                Card(item.title, $"{AssetTypeName(item.type)}  ·  {item.price:N0} KZT");
                ActionButton(_game.Player.money >= item.price ? $"Купить за {item.price:N0} KZT" : $"Недостаточно средств · {item.price:N0} KZT",
                    () => _game.Assets.BuyAsset(item.id), Accent, _game.Player.money >= item.price);
            }
            ActionButton("Купить бизнес", () => Show(Page.Business), SurfaceAlt);
            Card("Билеты и бусты", "В текущей модели игры нет доступных предложений для этих категорий.", Muted);
        }

        private void RenderWallet()
        {
            SectionTitle("Кошелёк");
            List<MoneyTransactionData> tx = (_game.Player.transactions ?? new List<MoneyTransactionData>()).Where(item => item != null).ToList();
            long income = tx.Where(item => item.amount > 0).Sum(item => (long)item.amount);
            long expense = tx.Where(item => item.amount < 0).Sum(item => (long)item.amount);
            Card("Текущий баланс", $"{_game.Player.money:N0} KZT", Accent);
            Card("По сохранённой истории операций", $"Доходы  {income:N0} KZT\nРасходы  {Math.Abs(expense):N0} KZT\nСальдо истории  {(income + expense):N0} KZT\nОпераций  {tx.Count}");
            SectionTitle("История операций");
            foreach (MoneyTransactionData item in tx.AsEnumerable().Reverse().Take(100))
            {
                if (item == null) continue;
                string date = item.timestampUtcTicks > 0
                    ? new DateTime(item.timestampUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("dd.MM HH:mm", CultureInfo.InvariantCulture)
                    : "без времени";
                Card(item.source ?? item.type ?? "Операция", $"{date}  ·  {item.amount:+#,0;-#,0;0} KZT\nБаланс: {item.balanceBefore:N0} → {item.balanceAfter:N0} KZT",
                    item.amount >= 0 ? Accent : Color.white);
            }
            if (tx.Count == 0) Card("Операций пока нет", "Покупки и выплаты появятся здесь после их проведения.", Muted);
        }

        private void RenderEducation()
        {
            SectionTitle("Образование");
            if (_game.Education.IsStudying)
            {
                string id = _game.Education.CurrentStudyingId;
                EducationConfig active = _game.Education.AvailableEducations.TryGetValue(id, out var found) ? found : null;
                TMP_Text status = Card(active != null ? active.title : "Обучение", $"Осталось {Mathf.CeilToInt(_game.TimeSystem.GetRemainingSeconds(_game.Player.studyTimer.endUtcTicks))} сек.");
                _liveBindings.Add(() => { if (status != null) status.text = $"Осталось {Mathf.CeilToInt(_game.TimeSystem.GetRemainingSeconds(_game.Player.studyTimer.endUtcTicks))} сек."; });
            }
            foreach (EducationConfig course in _game.Education.AvailableEducations.Values)
            {
                bool completed = _game.Education.HasCompleted(course.id);
                Card(course.title, $"Стоимость  {course.cost:N0} KZT\nДлительность  {course.durationInSeconds:0} сек.\nСтатус  {(completed ? "пройдено" : "не пройдено")}");
                if (!completed)
                    ActionButton(_game.Player.money >= course.cost ? "Начать обучение" : $"Недостаточно средств · {course.cost:N0} KZT",
                        () => _game.Education.StartStudy(course.id), Accent,
                        !_game.Education.IsStudying && _game.Player.money >= course.cost);
            }
            Card("Требования должностей", string.Join("\n", _game.Job.JobsConfig.Values.Select(job => $"{job.jobTitle}: {DescribeJobRequirements(job)}")), Muted);
        }

        private void RenderBank()
        {
            SectionTitle("Банк");
            foreach (DepositConfig config in _game.Bank.Configs.Values)
            {
                DepositData data = _game.Bank.GetDepositData(config.id);
                Card(config.title, $"Ставка по конфигу  {config.profitPercent:P0}\nДлительность  {config.durationInSeconds:0} сек.");
                if (data.isActive)
                {
                    TMP_Text remaining = Card("Вклад активен", $"Осталось {Mathf.CeilToInt(_game.TimeSystem.GetRemainingSeconds(data.endUtcTicks))} сек. · вложено {data.investedAmount:N0} KZT");
                    _liveBindings.Add(() => { if (remaining != null) remaining.text = $"Осталось {Mathf.CeilToInt(_game.TimeSystem.GetRemainingSeconds(data.endUtcTicks))} сек. · вложено {data.investedAmount:N0} KZT"; });
                }
                else if (data.isReadyToClaim)
                {
                    int expected = data.investedAmount + Mathf.RoundToInt(data.investedAmount * config.profitPercent);
                    Card("Вклад завершён", $"К выплате {expected:N0} KZT");
                    ActionButton("Забрать вклад", () => _game.Bank.ClaimDeposit(config.id));
                }
                else
                {
                    InputFieldRow("Сумма в KZT", out _depositAmountInput);
                    TMP_InputField captured = _depositAmountInput;
                    ActionButton("Открыть вклад", () =>
                    {
                        if (captured != null && int.TryParse(captured.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount))
                            _game.Bank.OpenDeposit(config.id, amount);
                        RenderPage();
                    }, Accent, false);
                    Button openButton = _content.GetChild(_content.childCount - 1).GetComponent<Button>();
                    if (openButton != null)
                    {
                        _liveBindings.Add(() =>
                        {
                            bool valid = captured != null && int.TryParse(captured.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount) && amount > 0 && amount <= _game.Player.money;
                            openButton.interactable = valid && !data.isActive && !data.isReadyToClaim;
                        });
                    }
                }
            }
            Card("Срок", "Банковские вклады используют реальные UTC-сроки сервиса и продолжаются при закрытом приложении.", Muted);
        }

        private void RenderNeeds()
        {
            SectionTitle("Самочувствие");
            Card("Здоровье", $"{_game.Player.health}%");
            ActionButton("Восстановить 20 · 50 KZT", () => _game.Needs.Heal(20, 50), Accent, _game.Player.money >= 50 && _game.Player.health < 100);
            Card("Настроение", $"{_game.Player.mood}%");
            ActionButton("Отдохнуть · 50 KZT", () => _game.Needs.Rest(20, 50), Accent, _game.Player.money >= 50 && _game.Player.mood < 100);
        }

        private string WalletSummary()
        {
            List<MoneyTransactionData> transactions = (_game.Player.transactions ?? new List<MoneyTransactionData>()).Where(item => item != null).ToList();
            long net = transactions.Sum(item => (long)item.amount);
            return $"Баланс { _game.Player.money:N0} KZT  ·  сальдо истории {net:N0} KZT";
        }

        private string GetActiveTimersText()
        {
            List<string> active = new List<string>();
            if (_game.Job.IsWorking) active.Add($"Работа · {Mathf.CeilToInt(_game.Job.RemainingWorkSeconds)} сек.");
            if (_game.Job.IsSalaryReadyToClaim) active.Add("Зарплата ожидает получения");
            if (_game.Education.IsStudying) active.Add($"Учёба · {Mathf.CeilToInt(_game.TimeSystem.GetRemainingSeconds(_game.Player.studyTimer.endUtcTicks))} сек.");
            foreach (DepositData deposit in _game.Player.activeDeposits ?? new List<DepositData>())
                if (deposit.isActive) active.Add($"Вклад · {_game.TimeSystem.GetRemainingSeconds(deposit.endUtcTicks):0} сек.");
            return active.Count == 0 ? "Активных таймеров нет" : string.Join("\n", active);
        }

        private string GetEducationSummary()
        {
            if (_game.Education.IsStudying) return "Идёт обучение · нажми, чтобы открыть";
            int complete = _game.Player.completedEducation == null ? 0 : _game.Player.completedEducation.Count;
            return $"Пройдено курсов: {complete} · открой список программ";
        }

        private string GetBankSummary()
        {
            DepositData deposit = _game.Bank.GetDepositData("dep_quick");
            if (deposit.isReadyToClaim) return "Вклад готов к получению";
            if (deposit.isActive) return $"Активный вклад · осталось {Mathf.CeilToInt(_game.TimeSystem.GetRemainingSeconds(deposit.endUtcTicks))} сек.";
            return "Вклады по доступным банковским конфигам";
        }

        private string GetOwnedAssetsSummary()
        {
            int count = _game.Assets.Configs.Values.Count(item => _game.Assets.HasAsset(item.id));
            return count == 0 ? "Пока нет приобретённого имущества" : $"Предметов: {count}";
        }

        private string DescribeJobRequirements(JobConfig job)
        {
            List<string> requirements = new List<string>();
            if (!string.IsNullOrEmpty(job.requiredEducationId))
                requirements.Add(_game.Education.AvailableEducations.TryGetValue(job.requiredEducationId, out var edu) ? edu.title : job.requiredEducationId);
            if (!string.IsNullOrEmpty(job.requiredHouseId)) requirements.Add(AssetTitle(job.requiredHouseId));
            if (!string.IsNullOrEmpty(job.requiredCarId)) requirements.Add(AssetTitle(job.requiredCarId));
            if (!string.IsNullOrEmpty(job.requiredClothingId)) requirements.Add(AssetTitle(job.requiredClothingId));
            return requirements.Count == 0 ? "Требований нет" : "Нужно: " + string.Join(", ", requirements);
        }

        private string AssetTitle(string id) => _game.Assets.Configs.TryGetValue(id, out var config) ? config.title : id;
        private static string AssetTypeName(AssetType type) => type == AssetType.House ? "Жильё" : type == AssetType.Car ? "Транспорт" : "Одежда";

        private TMP_Text SectionTitle(string title)
        {
            TMP_Text label = AddText(_content, title, 26, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
            LayoutElement layout = label.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 44f;
            return label;
        }

        private TMP_Text Card(string title, string body, Color? titleColor = null)
        {
            GameObject card = new GameObject("Card - " + title, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            card.transform.SetParent(_content, false);
            card.GetComponent<Image>().color = Surface;
            VerticalLayoutGroup layout = card.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 16, 16);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            card.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            TMP_Text header = AddText(card.transform, title, 19, titleColor ?? Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            TMP_Text detail = AddText(card.transform, body, 16, Muted, FontStyles.Normal, TextAlignmentOptions.Left);
            detail.textWrappingMode = TextWrappingModes.Normal;
            detail.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            detail.gameObject.AddComponent<LayoutElement>().minHeight = 30f;
            return detail;
        }

        private void ButtonCard(string title, string body, Action action, Color? titleColor = null)
        {
            GameObject card = new GameObject("Link - " + title, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            card.transform.SetParent(_content, false);
            Image image = card.GetComponent<Image>();
            image.color = SurfaceAlt;
            Button button = card.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action?.Invoke());
            VerticalLayoutGroup layout = card.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 15, 15);
            layout.spacing = 5f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            TMP_Text heading = AddText(card.transform, title + "  ›", 19, titleColor ?? Accent, FontStyles.Bold, TextAlignmentOptions.Left);
            heading.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            TMP_Text detail = AddText(card.transform, body, 15, Muted, FontStyles.Normal, TextAlignmentOptions.Left);
            detail.textWrappingMode = TextWrappingModes.Normal;
            detail.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            detail.gameObject.AddComponent<LayoutElement>().minHeight = 30f;
            card.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void ActionButton(string label, Action action, Color? color = null, bool interactable = true)
        {
            Button button = MakeButton(_content, label, color ?? Accent, 56f, action, 16);
            button.interactable = interactable && action != null;
        }

        private Button MakeButton(Transform parent, string label, Color color, float height, Action action, int fontSize)
        {
            GameObject obj = new GameObject("Button - " + label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            obj.transform.SetParent(parent, false);
            Image image = obj.GetComponent<Image>();
            image.color = color;
            Button button = obj.GetComponent<Button>();
            button.targetGraphic = image;
            if (action != null) button.onClick.AddListener(() => action());
            obj.GetComponent<LayoutElement>().preferredHeight = height;
            TMP_Text text = AddText(obj.transform, label, fontSize, color == Accent ? Background : Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(12f, 4f);
            rect.offsetMax = new Vector2(-12f, -4f);
            text.textWrappingMode = TextWrappingModes.Normal;
            button.interactable = action != null;
            return button;
        }

        private void InputFieldRow(string placeholder, out TMP_InputField input)
        {
            GameObject obj = new GameObject("Deposit amount", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            obj.transform.SetParent(_content, false);
            obj.GetComponent<Image>().color = SurfaceAlt;
            obj.GetComponent<LayoutElement>().preferredHeight = 58f;
            obj.SetActive(false);
            input = obj.AddComponent<TMP_InputField>();
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
            input.lineType = TMP_InputField.LineType.SingleLine;
            RectTransform viewport = CreateRect("TextViewport", obj.transform);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(12f, 2f);
            viewport.offsetMax = new Vector2(-12f, -2f);
            viewport.gameObject.AddComponent<RectMask2D>();
            TMP_Text valueText = AddText(viewport, "", 17, Color.white, FontStyles.Normal, TextAlignmentOptions.Left);
            valueText.rectTransform.anchorMin = Vector2.zero;
            valueText.rectTransform.anchorMax = Vector2.one;
            valueText.rectTransform.offsetMin = new Vector2(6f, 6f);
            valueText.rectTransform.offsetMax = new Vector2(-6f, -6f);
            input.textComponent = valueText;
            TMP_Text placeholderText = AddText(viewport, placeholder, 16, Muted, FontStyles.Normal, TextAlignmentOptions.Left);
            placeholderText.rectTransform.anchorMin = Vector2.zero;
            placeholderText.rectTransform.anchorMax = Vector2.one;
            placeholderText.rectTransform.offsetMin = new Vector2(6f, 6f);
            placeholderText.rectTransform.offsetMax = new Vector2(-6f, -6f);
            input.placeholder = placeholderText;
            input.textViewport = viewport;
            input.targetGraphic = obj.GetComponent<Image>();
            obj.SetActive(true);
        }

        private TMP_Text Text(Transform parent, string value, int size, Color color, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, FontStyles style, TextAlignmentOptions alignment)
        {
            TMP_Text result = AddText(parent, value, size, color, style, alignment);
            RectTransform rect = result.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return result;
        }

        private static TMP_Text AddText(Transform parent, string value, int size, Color color, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject obj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            TMP_Text text = obj.GetComponent<TMP_Text>();
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.alignment = alignment;
            text.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj.GetComponent<RectTransform>();
        }

        private static void DestroyGenerated(GameObject generated)
        {
            if (Application.isPlaying) Destroy(generated);
            else DestroyImmediate(generated);
        }

    }
}
