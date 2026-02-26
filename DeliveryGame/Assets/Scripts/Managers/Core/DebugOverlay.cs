using DeliveryRun;
using DeliveryRun.Managers.Subs;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliveryRun.Managers.Core
{
    public sealed class DebugOverlay : MonoBehaviour
    {
        private const float PanelWidth = 380f;
        private const float PanelHeight = 500f;
        private const float Margin = 12f;

        private void OnGUI()
        {
#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
            return;
#endif
            if (!Application.isPlaying)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(Margin, Margin, PanelWidth, PanelHeight), GUI.skin.box);

            string activeScene = SceneManager.GetActiveScene().name;
            GUILayout.Label("Scene: " + activeScene);

            CoreRoot root = CoreRoot.Instance;
            if (root == null || root.Events == null || root.Services == null)
            {
                GUILayout.Label("CoreRoot not ready.");
                GUILayout.EndArea();
                return;
            }

            EventBus events = root.Events;
            if (activeScene == SceneNames.LobbyScene)
            {
                if (GUILayout.Button("Start Run"))
                {
                    events.Publish(new StartRunRequested());
                }
            }
            else if (activeScene == SceneNames.RunScene)
            {
                if (GUILayout.Button("End Run -> Lobby"))
                {
                    events.Publish(new ReturnToLobbyRequested());
                }

                RunSessionManager runSessionDebugManager;
                if (root.Services.TryGet(out runSessionDebugManager) && runSessionDebugManager != null)
                {
                    if (GUILayout.Button("Force End Run"))
                    {
                        runSessionDebugManager.ForceEndRun();
                    }

                    if (GUILayout.Button("+60s"))
                    {
                        runSessionDebugManager.DebugAddElapsed(60f);
                    }

                    if (GUILayout.Button("PauseForChoice"))
                    {
                        runSessionDebugManager.DebugEnterChoicePause();
                    }

                    if (GUILayout.Button("Resume"))
                    {
                        runSessionDebugManager.DebugResumeFromChoice();
                    }
                }
            }

            if (activeScene == SceneNames.LoadingScene)
            {
                LoadingState loadingState;
                if (root.Services.TryGet(out loadingState) && loadingState != null)
                {
                    GUILayout.Space(6f);
                    GUILayout.Label("From: " + SafeText(loadingState.FromScene));
                    GUILayout.Label("To: " + SafeText(loadingState.ToScene));
                    GUILayout.Label("Phase: " + SafeText(loadingState.Phase));
                    GUILayout.Label("Progress: " + Mathf.RoundToInt(loadingState.Progress01 * 100f) + "%");
                }
                else
                {
                    GUILayout.Label("LoadingState unavailable.");
                }
            }

            GUILayout.Space(8f);

            SoundManager soundManager;
            if (root.Services.TryGet(out soundManager) && soundManager != null)
            {
                GUILayout.Label("BGM: " + SafeText(soundManager.CurrentBgmKey));
            }

            UIManager uiManager;
            if (root.Services.TryGet(out uiManager) && uiManager != null)
            {
                GUILayout.Label("UI Screen: " + SafeText(uiManager.CurrentScreen));
                GUILayout.Label("UI Toast: " + SafeText(uiManager.LastToast));
                GUILayout.Label("Run Remaining: " + Mathf.CeilToInt(uiManager.RunRemainingSeconds) + "s");
                GUILayout.Label("Rating: " + uiManager.RatingValue.ToString("F2"));
                GUILayout.Label("Coins (Run/Total): " + uiManager.SessionCoins + "/" + uiManager.TotalCoins);
                GUILayout.Label(
                    "Orders A/C/F: " +
                    uiManager.ActiveOrderCount + "/" +
                    uiManager.CompletedOrderCount + "/" +
                    uiManager.FailedOrderCount);
            }

            RunSessionManager runSessionManager;
            if (root.Services.TryGet(out runSessionManager) && runSessionManager != null)
            {
                GUILayout.Label("RunSession Active: " + (runSessionManager.HasActiveRun ? "Y" : "N"));
                GUILayout.Label("Run State: " + runSessionManager.CurrentState);
                GUILayout.Label(
                    "Run Time: " + Mathf.FloorToInt(runSessionManager.ElapsedSeconds) +
                    " / " + Mathf.FloorToInt(runSessionManager.RunDurationSeconds));
                GUILayout.Label("Run Remaining: " + Mathf.CeilToInt(runSessionManager.RemainingSeconds) + "s");
            }

            RatingManager ratingManager;
            if (root.Services.TryGet(out ratingManager) && ratingManager != null)
            {
                GUILayout.Label("Rating(Current): " + ratingManager.CurrentRating.ToString("F2"));
            }

            EconomyManager economyManager;
            if (root.Services.TryGet(out economyManager) && economyManager != null)
            {
                GUILayout.Label(
                    "Economy(Current): " + economyManager.SessionCoins +
                    " / Total " + economyManager.TotalCoins);
            }

            MusicChoiceManager musicChoiceManager;
            if (root.Services.TryGet(out musicChoiceManager) && musicChoiceManager != null)
            {
                GUILayout.Label(
                    "Music Choice Pending: " +
                    (musicChoiceManager.HasPendingChoice
                        ? "#" + (musicChoiceManager.PendingChoiceIndex + 1)
                        : "-"));

                if (musicChoiceManager.HasPendingChoice)
                {
                    GUILayout.Label(
                        "Choice Timeout: " +
                        Mathf.CeilToInt(musicChoiceManager.PendingChoiceRemainingSeconds) + "s");

                    if (GUILayout.Button("Apply Music Choice"))
                    {
                        musicChoiceManager.ApplyChoice("mod/manual_debug");
                    }
                }
            }

            if (activeScene == SceneNames.RunScene)
            {
                DeliveryManager deliveryManager;
                if (root.Services.TryGet(out deliveryManager) && deliveryManager != null)
                {
                    if (deliveryManager.HasActiveOrder)
                    {
                        GUILayout.Label(
                            "Active Order #" + deliveryManager.ActiveOrderSequence +
                            " (" + Mathf.CeilToInt(deliveryManager.ActiveOrderRemainingSeconds) + "s)");

                        if (GUILayout.Button("Complete Active Order"))
                        {
                            deliveryManager.CompleteActiveOrder();
                        }

                        if (GUILayout.Button("Fail Active Order"))
                        {
                            deliveryManager.FailActiveOrder(DeliveryFailReason.ManualAbort);
                        }
                    }
                    else
                    {
                        GUILayout.Label("Active Order: -");
                    }
                }
            }

            GUILayout.EndArea();
        }

        private static string SafeText(string value)
        {
            return string.IsNullOrEmpty(value) ? "-" : value;
        }
    }
}
