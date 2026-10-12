using UnityEngine;
using UnityEngine.UI;

namespace ZeroToHero.UI
{
    public class TabManager : MonoBehaviour
    {
        [Header("Panels")]
        public GameObject jobPanel;
        public GameObject educationPanel;
        public GameObject businessPanel;
        public GameObject needsPanel;
        public GameObject bankPanel; // <--- Добавили панель банка

        [Header("Navigation Buttons")]
        public Button jobTabButton;
        public Button educationTabButton;
        public Button businessTabButton;
        public Button needsTabButton;
        public Button bankTabButton; // <--- Добавили кнопку вкладки банка

        private void Start()
        {
            if (jobTabButton != null) jobTabButton.onClick.AddListener(() => ShowTab(jobPanel));
            if (educationTabButton != null) educationTabButton.onClick.AddListener(() => ShowTab(educationPanel));
            if (businessTabButton != null) businessTabButton.onClick.AddListener(() => ShowTab(businessPanel));
            if (needsTabButton != null) needsTabButton.onClick.AddListener(() => ShowTab(needsPanel));
            if (bankTabButton != null) bankTabButton.onClick.AddListener(() => ShowTab(bankPanel)); // <--- Листенер

            ShowTab(jobPanel);
        }

        public void ShowTab(GameObject panelToShow)
        {
            if (jobPanel != null) jobPanel.SetActive(false);
            if (educationPanel != null) educationPanel.SetActive(false);
            if (businessPanel != null) businessPanel.SetActive(false);
            if (needsPanel != null) needsPanel.SetActive(false);
            if (bankPanel != null) bankPanel.SetActive(false); // <--- Выключаем банк

            if (panelToShow != null)
            {
                panelToShow.SetActive(true);
            }
        }
    }
}