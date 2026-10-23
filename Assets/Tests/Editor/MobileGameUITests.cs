using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace ZeroToHero.SaveTests
{
    public class MobileGameUITests
    {
        private string _directory;
        private GameObject _managerObject;
        private GameObject _canvasObject;
        private Component _manager;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "ZeroToHero_MobileUI_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);

            Assembly game = AppDomain.CurrentDomain.GetAssemblies()
                .First(assembly => assembly.GetName().Name == "Assembly-CSharp");
            Type managerType = game.GetType("ZeroToHero.Core.GameManager", true);
            Type saveType = game.GetType("ZeroToHero.Core.SaveService", true);
            Type mainUiType = game.GetType("ZeroToHero.UI.MainUI", true);
            Type tabsType = game.GetType("ZeroToHero.UI.TabManager", true);

            _managerObject = new GameObject("Isolated Mobile UI Test GameManager");
            _managerObject.SetActive(false);
            _manager = _managerObject.AddComponent(managerType);
            object save = saveType.GetMethod("CreateTestProfile", BindingFlags.Public | BindingFlags.Static)
                .Invoke(null, new object[] { Path.Combine(_directory, "test-profile.json") });
            managerType.GetMethod("ConfigureSaveServiceForTesting").Invoke(_manager, new[] { save });
            _managerObject.SetActive(true);
            managerType.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, _manager);
            managerType.GetMethod("InitServices", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_manager, null);

            _canvasObject = new GameObject("Isolated Mobile UI Test Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Component mainUi = _canvasObject.AddComponent(mainUiType);
            Component legacyTabs = _canvasObject.AddComponent(tabsType);
            mainUiType.GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(mainUi, null);
            Component mobileUi = _canvasObject.GetComponent(game.GetType("ZeroToHero.UI.MobileGameUI", true));
            Assert.That(mobileUi, Is.Not.Null);
            Assert.That(((Behaviour)mobileUi).enabled, Is.True);
            Assert.That(((Behaviour)mainUi).enabled, Is.False);
            Assert.That(((Behaviour)legacyTabs).enabled, Is.False);
        }

        [TearDown]
        public void TearDown()
        {
            if (_canvasObject != null) UnityEngine.Object.DestroyImmediate(_canvasObject);
            if (_managerObject != null) UnityEngine.Object.DestroyImmediate(_managerObject);
            Type managerType = AppDomain.CurrentDomain.GetAssemblies()
                .First(assembly => assembly.GetName().Name == "Assembly-CSharp")
                .GetType("ZeroToHero.Core.GameManager", true);
            managerType.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
            if (!string.IsNullOrEmpty(_directory) && Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        [Test]
        public void MobileNavigationAndSecondaryScreens_AreBuiltForAllRequestedAreas()
        {
            Transform root = _canvasObject.transform.Find("MobileGameUI");
            Assert.That(root, Is.Not.Null);
            Transform nav = root.Find("BottomNavigation");
            Assert.That(nav, Is.Not.Null);
            string[] navLabels = Enumerable.Range(0, nav.childCount)
                .Select(index => nav.GetChild(index).name.Replace("Button - ", ""))
                .ToArray();
            CollectionAssert.AreEqual(new[] { "⌂\nГлавная", "◷\nРабота", "▣\nБизнес", "◆\nАктивы", "＋\nМагазин" }, navLabels);

            string[] pageMarkers = { "ТВОЙ ПУТЬ", "Следующие должности", "Мой бизнес", "Моё имущество", "Магазин имущества" };
            for (int i = 0; i < nav.childCount; i++)
            {
                nav.GetChild(i).GetComponent<Button>().onClick.Invoke();
                Assert.That(TextContains(root, pageMarkers[i]), Is.True, $"Не открылся экран {navLabels[i]}");
            }

            nav.GetChild(0).GetComponent<Button>().onClick.Invoke();
            AssertHomeShortcutOpens(root, "Образование", "Длительность");
            nav.GetChild(0).GetComponent<Button>().onClick.Invoke();
            AssertHomeShortcutOpens(root, "Банк", "Ставка по конфигу");
            nav.GetChild(0).GetComponent<Button>().onClick.Invoke();
            AssertHomeShortcutOpens(root, "Кошелёк", "История операций");
        }

        private static void AssertHomeShortcutOpens(Transform root, string linkName, string expectedText)
        {
            Button shortcut = root.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => button.name == "Link - " + linkName);
            Assert.That(shortcut, Is.Not.Null, "Главная должна вести в " + linkName);
            shortcut.onClick.Invoke();
            Assert.That(TextContains(root, expectedText), Is.True, "Не открылся экран " + linkName);
        }

        [Test]
        public void WorkButtons_CallRealJobAndEconomyServicesExactlyOnce()
        {
            Type managerType = _manager.GetType();
            object job = managerType.GetProperty("Job").GetValue(_manager);
            object player = managerType.GetProperty("Player").GetValue(_manager);
            Transform root = _canvasObject.transform.Find("MobileGameUI");
            Transform nav = root.Find("BottomNavigation");
            Button workTab = nav.GetChild(1).GetComponent<Button>();
            workTab.onClick.Invoke();

            Button start = root.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "Button - Начать смену");
            start.onClick.Invoke();
            Assert.That((bool)job.GetType().GetProperty("IsWorking").GetValue(job), Is.True);

            object timer = player.GetType().GetField("workTimer").GetValue(player);
            timer.GetType().GetField("endUtcTicks").SetValue(timer, DateTime.UtcNow.Ticks - 1);
            job.GetType().GetMethod("UpdateTimer").Invoke(job, new object[] { 0f });
            Button claim = root.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "Button - Получить зарплату");
            claim.onClick.Invoke();

            Assert.That((int)player.GetType().GetField("money").GetValue(player), Is.EqualTo(100));
            System.Collections.IList transactions = (System.Collections.IList)player.GetType().GetField("transactions").GetValue(player);
            Assert.That(transactions.Count, Is.EqualTo(1));
            Assert.That((int)transactions[0].GetType().GetField("amount").GetValue(transactions[0]), Is.EqualTo(100));
            Assert.That((bool)job.GetType().GetProperty("IsSalaryReadyToClaim").GetValue(job), Is.False);
            Assert.That(Directory.GetFiles(_directory, "*", SearchOption.AllDirectories).Length, Is.GreaterThan(0));
        }

        [Test]
        public void ShopPurchase_UsesConfiguredAssetPriceAndUpdatesWallet()
        {
            object player = _manager.GetType().GetProperty("Player").GetValue(_manager);
            player.GetType().GetField("money").SetValue(player, 200);
            Component gameManager = _manager;
            gameManager.GetType().GetMethod("SaveGame").Invoke(gameManager, null);

            Transform root = _canvasObject.transform.Find("MobileGameUI");
            Button shopTab = root.Find("BottomNavigation").GetChild(4).GetComponent<Button>();
            shopTab.onClick.Invoke();
            Button purchase = root.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "Button - Купить за 200 KZT");
            purchase.onClick.Invoke();

            object assets = gameManager.GetType().GetProperty("Assets").GetValue(gameManager);
            Assert.That((int)player.GetType().GetField("money").GetValue(player), Is.Zero);
            Assert.That((bool)assets.GetType().GetMethod("HasAsset").Invoke(assets, new object[] { "house_hostel" }), Is.True);
            System.Collections.IList transactions = (System.Collections.IList)player.GetType().GetField("transactions").GetValue(player);
            Assert.That(transactions.Count, Is.EqualTo(1));
            Assert.That((int)transactions[0].GetType().GetField("amount").GetValue(transactions[0]), Is.EqualTo(-200));
        }

        [Test]
        public void EducationScreen_UsesConfiguredCourseAndRecordsSpending()
        {
            object player = _manager.GetType().GetProperty("Player").GetValue(_manager);
            player.GetType().GetField("money").SetValue(player, 500);
            _manager.GetType().GetMethod("SaveGame").Invoke(_manager, null);
            Transform root = _canvasObject.transform.Find("MobileGameUI");
            Transform nav = root.Find("BottomNavigation");
            nav.GetChild(0).GetComponent<Button>().onClick.Invoke();
            AssertHomeShortcutOpens(root, "Образование", "Длительность");
            root.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "Button - Начать обучение").onClick.Invoke();

            object education = _manager.GetType().GetProperty("Education").GetValue(_manager);
            Assert.That((bool)education.GetType().GetProperty("IsStudying").GetValue(education), Is.True);
            Assert.That((int)player.GetType().GetField("money").GetValue(player), Is.Zero);
            System.Collections.IList transactions = (System.Collections.IList)player.GetType().GetField("transactions").GetValue(player);
            Assert.That(transactions.Count, Is.EqualTo(1));
            Assert.That((int)transactions[0].GetType().GetField("amount").GetValue(transactions[0]), Is.EqualTo(-500));

        }

        [Test]
        public void BankScreen_UsesEnteredAmountAndRecordsDepositExactlyOnce()
        {
            object player = _manager.GetType().GetProperty("Player").GetValue(_manager);
            player.GetType().GetField("money").SetValue(player, 500);
            _manager.GetType().GetMethod("SaveGame").Invoke(_manager, null);
            Transform root = _canvasObject.transform.Find("MobileGameUI");
            Transform nav = root.Find("BottomNavigation");
            nav.GetChild(0).GetComponent<Button>().onClick.Invoke();
            AssertHomeShortcutOpens(root, "Банк", "Ставка по конфигу");
            Transform inputObject = root.Find("Body/Content/Deposit amount");
            Assert.That(inputObject, Is.Not.Null);
            Component input = inputObject.GetComponents<Component>()
                .Single(component => component.GetType().FullName == "TMPro.TMP_InputField");
            input.GetType().GetProperty("text").SetValue(input, "250");
            Button openDeposit = root.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "Button - Открыть вклад");
            _canvasObject.GetComponent("MobileGameUI").GetType()
                .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_canvasObject.GetComponent("MobileGameUI"), null);
            Assert.That(openDeposit.interactable, Is.True);
            openDeposit.onClick.Invoke();

            object bank = _manager.GetType().GetProperty("Bank").GetValue(_manager);
            object deposit = bank.GetType().GetMethod("GetDepositData").Invoke(bank, new object[] { "dep_quick" });
            Assert.That((bool)deposit.GetType().GetField("isActive").GetValue(deposit), Is.True);
            Assert.That((int)deposit.GetType().GetField("investedAmount").GetValue(deposit), Is.EqualTo(250));
            Assert.That((int)player.GetType().GetField("money").GetValue(player), Is.EqualTo(250));
            System.Collections.IList transactions = (System.Collections.IList)player.GetType().GetField("transactions").GetValue(player);
            Assert.That(transactions.Count, Is.EqualTo(1));
            Assert.That((int)transactions[0].GetType().GetField("amount").GetValue(transactions[0]), Is.EqualTo(-250));
            Assert.That((int)transactions[0].GetType().GetField("balanceBefore").GetValue(transactions[0]), Is.EqualTo(500));
            Assert.That((int)transactions[0].GetType().GetField("balanceAfter").GetValue(transactions[0]), Is.EqualTo(250));
        }

        private static bool TextContains(Transform root, string value)
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component.GetType().FullName != "TMPro.TextMeshProUGUI") continue;
                PropertyInfo text = component.GetType().GetProperty("text");
                if (text != null && ((string)text.GetValue(component)).Contains(value)) return true;
            }
            return false;
        }
    }
}
