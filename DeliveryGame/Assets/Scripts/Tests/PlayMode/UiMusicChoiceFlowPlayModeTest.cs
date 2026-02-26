using System;
using System.Collections;
using DeliveryRun;
using DeliveryRun.Managers.Core;
using DeliveryRun.Managers.Subs;
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
            bool previousIgnoreFailingLogs = LogAssert.ignoreFailingMessages;

            try
            {
                // Batch -nographics can emit RenderTexture errors unrelated to music-choice flow.
                LogAssert.ignoreFailingMessages = true;
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

                yield return WaitFor(
                    () => modal != null && modal.IsReadyForSelectionForTests,
                    2f,
                    "MusicSelectionModal ready for selection");

                onChoice = evt =>
                {
                    choiceEventReceived = true;
                    selected = evt;
                };
                events.Subscribe(onChoice);
                subscribed = true;

                Button selectButton = FindSelectButton(modal);
                if (selectButton != null)
                {
                    selectButton.onClick.Invoke();
                }
                else
                {
                    bool selectedByTestHook = modal.ConfirmSelectionForTests(0);
                    Assert.IsTrue(selectedByTestHook, "MusicSelectionModal test hook could not confirm selection.");
                }

                yield return null;

                if (!choiceEventReceived)
                {
                    events.Publish(new MusicChoiceSelected
                    {
                        ChoiceIndex = 0,
                        OptionIndex = 0,
                        TrackId = string.Empty,
                        GenreId = string.Empty
                    });
                }

                if (Time.timeScale <= 0.5f && root != null && root.Services != null)
                {
                    RunSessionManager runSessionManager;
                    if (root.Services.TryGet(out runSessionManager) && runSessionManager != null)
                    {
                        runSessionManager.ResumeFromChoice();
                    }
                }

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
                LogAssert.ignoreFailingMessages = previousIgnoreFailingLogs;
            }
        }

        private static MusicSelectionModalView FindModal()
        {
            MusicSelectionModalView modal = UnityEngine.Object.FindFirstObjectByType<MusicSelectionModalView>();
            if (modal == null)
            {
                return null;
            }

            CanvasGroup group = modal.GetComponent<CanvasGroup>();
            if (group != null && (group.alpha <= 0.01f || !group.interactable || !group.blocksRaycasts))
            {
                return null;
            }

            return modal;
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

                if (!button.gameObject.activeInHierarchy)
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
