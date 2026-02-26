using System;
using System.Collections;
using DeliveryRun;
using DeliveryRun.Managers.Core;
using DeliveryRun.UI.Run;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DeliveryRun.PlayModeTests
{
    public sealed class UiMusicChoiceFlowPlayModeTest
    {
        [UnityTest]
        public IEnumerator MusicChoice_AtZero_ShowsModal_Pauses_And_Resumes()
        {
            CoreRoot root = null;
            EventBus events = null;
            Action<MusicChoiceSelected> onChoice = null;
            bool choiceEventReceived = false;
            MusicChoiceSelected selected = default;
            bool subscribed = false;

            try
            {
                Time.timeScale = 1f;

                yield return SceneManager.LoadSceneAsync(SceneNames.CoreScene, LoadSceneMode.Single);

                yield return WaitFor(
                    () =>
                    {
                        root = CoreRoot.Instance;
                        return root != null && root.Events != null;
                    },
                    10f,
                    "CoreRoot and EventBus ready");

                events = root.Events;
                Assert.IsNotNull(events, "EventBus was not initialized.");

                if (SceneManager.GetActiveScene().name != SceneNames.LobbyScene)
                {
                    events.Publish(new BootToLobbyRequested());
                }

                yield return WaitFor(
                    () => SceneManager.GetActiveScene().name == SceneNames.LobbyScene,
                    15f,
                    "Transition to LobbyScene");
                Assert.AreEqual(SceneNames.LobbyScene, SceneManager.GetActiveScene().name);

                events.Publish(new StartRunRequested());
                yield return WaitFor(
                    () => SceneManager.GetActiveScene().name == SceneNames.RunScene,
                    15f,
                    "Transition to RunScene");
                Assert.AreEqual(SceneNames.RunScene, SceneManager.GetActiveScene().name);

                MusicSelectionModalView modal = null;
                yield return WaitFor(
                    () =>
                    {
                        modal = FindModal();
                        return modal != null;
                    },
                    8f,
                    "MusicSelectionModal appears");

                Assert.IsNotNull(modal, "MusicSelectionModalView was not found.");
                Assert.That(Time.timeScale, Is.EqualTo(0f).Within(0.001f), "PauseForChoice did not set Time.timeScale=0.");

                onChoice = evt =>
                {
                    choiceEventReceived = true;
                    selected = evt;
                };
                events.Subscribe(onChoice);
                subscribed = true;

                Button selectButton = FindSelectButton(modal);
                Assert.IsNotNull(selectButton, "Could not find a Select button in modal.");
                selectButton.onClick.Invoke();

                yield return WaitFor(() => choiceEventReceived, 2f, "MusicChoiceSelected published");
                Assert.IsTrue(choiceEventReceived, "MusicChoiceSelected event was not published.");

                yield return WaitFor(() => Time.timeScale > 0.5f, 2f, "Time.timeScale restored");
                Assert.Greater(Time.timeScale, 0.5f, "Time.timeScale was not restored.");

                RunHudView hud = null;
                yield return WaitFor(
                    () =>
                    {
                        hud = UnityEngine.Object.FindFirstObjectByType<RunHudView>(FindObjectsInactive.Include);
                        return hud != null;
                    },
                    5f,
                    "RunHudView exists");

                Assert.IsNotNull(hud, "RunHudView was not found in Run scene.");

                bool nowPlayingUpdated = false;
                yield return WaitFor(
                    () =>
                    {
                        if (hud == null)
                        {
                            hud = UnityEngine.Object.FindFirstObjectByType<RunHudView>(FindObjectsInactive.Include);
                            if (hud == null)
                            {
                                return false;
                            }
                        }

                        nowPlayingUpdated = HasNowPlayingText(hud);
                        return nowPlayingUpdated;
                    },
                    5f,
                    "RunHUD NOW PLAYING updated");

                Assert.IsTrue(nowPlayingUpdated, "RunHUD NOW PLAYING text was not updated.");

                events.Publish(new ReturnToLobbyRequested());
                yield return WaitFor(
                    () => SceneManager.GetActiveScene().name == SceneNames.LobbyScene,
                    15f,
                    "Return to LobbyScene");
                Assert.AreEqual(SceneNames.LobbyScene, SceneManager.GetActiveScene().name);
            }
            finally
            {
                if (subscribed && events != null && onChoice != null)
                {
                    events.Unsubscribe(onChoice);
                }

                Time.timeScale = 1f;
            }
        }

        private static MusicSelectionModalView FindModal()
        {
            return UnityEngine.Object.FindFirstObjectByType<MusicSelectionModalView>(FindObjectsInactive.Include);
        }

        private static Button FindSelectButton(MusicSelectionModalView modal)
        {
            Button[] buttons = modal.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null)
                {
                    continue;
                }

                Text label = button.GetComponentInChildren<Text>(true);
                if (label == null || string.IsNullOrEmpty(label.text))
                {
                    continue;
                }

                if (label.text.IndexOf("Select", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return button;
                }
            }

            return buttons.Length > 0 ? buttons[0] : null;
        }

        private static bool HasNowPlayingText(RunHudView hud)
        {
            Text[] texts = hud.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null || string.IsNullOrEmpty(text.text))
                {
                    continue;
                }

                if (text.text.IndexOf("NOW PLAYING", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (text.text.IndexOf("NOW PLAYING: -", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                int colonIndex = text.text.IndexOf(':');
                if (colonIndex >= 0 && colonIndex + 1 < text.text.Length)
                {
                    string right = text.text.Substring(colonIndex + 1).Trim();
                    if (!string.IsNullOrEmpty(right) && right != "-")
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static IEnumerator WaitFor(Func<bool> predicate, float timeoutSecondsRealtime, string conditionName)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start <= timeoutSecondsRealtime)
            {
                bool satisfied;
                try
                {
                    satisfied = predicate();
                }
                catch (Exception ex)
                {
                    Assert.Fail("Wait predicate threw: " + ex);
                    yield break;
                }

                if (satisfied)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail(
                "Timeout waiting for condition: " + conditionName +
                " (" + timeoutSecondsRealtime.ToString("F1") + "s).");
        }
    }
}
