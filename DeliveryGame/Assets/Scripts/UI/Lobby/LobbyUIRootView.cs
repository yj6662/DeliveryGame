using UnityEngine;
using UnityEngine.UI;

namespace DeliveryRun.UI.Lobby
{
    public enum LobbyPanel
    {
        MainMenu = 0,
        RegionUnlock = 1,
        Upgrade = 2
    }

    [DisallowMultipleComponent]
    public sealed class LobbyUIRootView : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject regionUnlockPanel;
        [SerializeField] private GameObject upgradePanel;

        [Header("Main Buttons")]
        [SerializeField] private Button deliveryStartButton;
        [SerializeField] private Button regionUnlockButton;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Button exitButton;

        [Header("Back Buttons")]
        [SerializeField] private Button regionUnlockBackButton;
        [SerializeField] private Button upgradeBackButton;

        public Button DeliveryStartButton => deliveryStartButton;
        public Button RegionUnlockButton => regionUnlockButton;
        public Button UpgradeButton => upgradeButton;
        public Button ExitButton => exitButton;
        public Button RegionUnlockBackButton => regionUnlockBackButton;
        public Button UpgradeBackButton => upgradeBackButton;

        private void Awake()
        {
            EnsureCanvasGroup(mainMenuPanel);
            EnsureCanvasGroup(regionUnlockPanel);
            EnsureCanvasGroup(upgradePanel);
            ShowPanel(LobbyPanel.MainMenu);
        }

        public void ShowPanel(LobbyPanel panel)
        {
            SetPanelState(mainMenuPanel, panel == LobbyPanel.MainMenu);
            SetPanelState(regionUnlockPanel, panel == LobbyPanel.RegionUnlock);
            SetPanelState(upgradePanel, panel == LobbyPanel.Upgrade);

            bool mainActive = panel == LobbyPanel.MainMenu;
            SetButtonInteractable(deliveryStartButton, mainActive);
            SetButtonInteractable(regionUnlockButton, mainActive);
            SetButtonInteractable(upgradeButton, mainActive);
            SetButtonInteractable(exitButton, mainActive);

            bool subActive = !mainActive;
            SetButtonInteractable(regionUnlockBackButton, subActive);
            SetButtonInteractable(upgradeBackButton, subActive);
        }

        public void BindForBuild(
            GameObject mainMenu,
            GameObject regionUnlock,
            GameObject upgrade,
            Button startButton,
            Button regionButton,
            Button upgradeButtonRef,
            Button exitButtonRef,
            Button regionBack,
            Button upgradeBack)
        {
            mainMenuPanel = mainMenu;
            regionUnlockPanel = regionUnlock;
            upgradePanel = upgrade;
            deliveryStartButton = startButton;
            regionUnlockButton = regionButton;
            upgradeButton = upgradeButtonRef;
            exitButton = exitButtonRef;
            regionUnlockBackButton = regionBack;
            upgradeBackButton = upgradeBack;
        }

        private static void SetButtonInteractable(Button button, bool interactable)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = interactable;
        }

        private static void SetPanelState(GameObject panel, bool active)
        {
            if (panel == null)
            {
                return;
            }

            if (panel.activeSelf != active)
            {
                panel.SetActive(active);
            }

            CanvasGroup canvasGroup = EnsureCanvasGroup(panel);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = active ? 1f : 0f;
                canvasGroup.interactable = active;
                canvasGroup.blocksRaycasts = active;
            }
        }

        private static CanvasGroup EnsureCanvasGroup(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = target.AddComponent<CanvasGroup>();
            }

            return canvasGroup;
        }
    }
}
