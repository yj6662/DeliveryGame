using System;
using System.Collections;
using System.Globalization;
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
    public sealed class OrderOfferCountdownPlayModeTest
    {
        private const float SceneTimeout = 20f;

        [UnityTest]
        public IEnumerator OfferCountdown_Decreases_AndPausesWhileMusicChoiceOpen()
        {
            CoreRoot root = null;
            EventBus events = null;

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
                    "CoreRoot initialized");

                events = root.Events;
                Assert.IsNotNull(events, "EventBus missing.");

                if (SceneManager.GetActiveScene().name != SceneNames.LobbyScene)
                {
                    events.Publish(new BootToLobbyRequested());
                }

                yield return WaitFor(
                    () => SceneManager.GetActiveScene().name == SceneNames.LobbyScene,
                    SceneTimeout,
                    "Lobby scene loaded");

                events.Publish(new StartRunRequested());
                yield return WaitFor(
                    () => SceneManager.GetActiveScene().name == SceneNames.RunScene,
                    SceneTimeout,
                    "Run scene loaded");

                yield return DismissMusicModalIfNeeded(root, events);
                yield return WaitFor(() => Time.timeScale > 0.5f, 4f, "Timescale resumed");

                events.Publish(new OfferSpawned
                {
                    OfferId = "TEST_OFFER_UI",
                    TtlSeconds = 5f,
                    PickupName = "PICKUP",
                    DeliveryName = "DELIVERY",
                    Reward = 250,
                    FoodId = "food_test",
                    FoodName = "Test Food"
                });

                float firstRemaining = 0f;
                yield return WaitFor(
                    () => TryGetOfferRemainingSeconds(out firstRemaining) && firstRemaining > 0f,
                    5f,
                    "Offer TTL visible");

                float baseline = firstRemaining;
                float waitStart = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - waitStart <= 1.2f)
                {
                    yield return null;
                }

                float secondRemaining;
                Assert.IsTrue(TryGetOfferRemainingSeconds(out secondRemaining), "Offer TTL text not found after delay.");
                Assert.Less(
                    secondRemaining,
                    baseline - 0.5f,
                    "Offer TTL should decrease over realtime while modal is closed.");

                events.Publish(new MusicChoiceModalStateChanged { IsOpen = true });

                float pausedStart = secondRemaining;
                waitStart = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - waitStart <= 1f)
                {
                    yield return null;
                }

                float pausedEnd;
                Assert.IsTrue(TryGetOfferRemainingSeconds(out pausedEnd), "Offer TTL text not found during pause window.");
                Assert.LessOrEqual(
                    Mathf.Abs(pausedEnd - pausedStart),
                    0.25f,
                    "Offer TTL should not meaningfully change while music choice modal state is open.");

                events.Publish(new MusicChoiceModalStateChanged { IsOpen = false });

                waitStart = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - waitStart <= 1f)
                {
                    yield return null;
                }

                float resumed;
                Assert.IsTrue(TryGetOfferRemainingSeconds(out resumed), "Offer TTL text not found after resume.");
                Assert.Less(
                    resumed,
                    pausedEnd - 0.35f,
                    "Offer TTL should resume decreasing after modal closes.");

                events.Publish(new OfferExpired { OfferId = "TEST_OFFER_UI" });
                events.Publish(new ReturnToLobbyRequested());
                yield return WaitFor(
                    () => SceneManager.GetActiveScene().name == SceneNames.LobbyScene,
                    SceneTimeout,
                    "Return to lobby");
            }
            finally
            {
                Time.timeScale = 1f;
            }
        }

        private static bool TryGetOfferRemainingSeconds(out float seconds)
        {
            seconds = 0f;
            Text[] texts = UnityEngine.Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null || string.IsNullOrEmpty(text.text))
                {
                    continue;
                }

                int marker = text.text.IndexOf("Accept TTL", StringComparison.OrdinalIgnoreCase);
                if (marker < 0)
                {
                    continue;
                }

                if (TryParseTtlSeconds(text.text, marker, out seconds))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseTtlSeconds(string content, int startMarkerIndex, out float seconds)
        {
            seconds = 0f;
            if (string.IsNullOrEmpty(content) || startMarkerIndex < 0 || startMarkerIndex >= content.Length)
            {
                return false;
            }

            int cursor = startMarkerIndex;
            while (cursor < content.Length)
            {
                char c = content[cursor];
                if ((c >= '0' && c <= '9') || c == '.')
                {
                    break;
                }

                cursor++;
            }

            if (cursor >= content.Length)
            {
                return false;
            }

            int end = cursor;
            while (end < content.Length)
            {
                char c = content[end];
                if (!((c >= '0' && c <= '9') || c == '.'))
                {
                    break;
                }

                end++;
            }

            if (end <= cursor)
            {
                return false;
            }

            string token = content.Substring(cursor, end - cursor);
            return float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds);
        }

        private static IEnumerator DismissMusicModalIfNeeded(CoreRoot root, EventBus events)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start <= 6f)
            {
                MusicSelectionModalView modal =
                    UnityEngine.Object.FindFirstObjectByType<MusicSelectionModalView>(FindObjectsInactive.Include);
                if (modal != null)
                {
                    if (modal.IsReadyForSelectionForTests && modal.ConfirmSelectionForTests(0))
                    {
                        yield return null;
                        yield break;
                    }

                    if (root != null && root.Services != null)
                    {
                        RunSessionManager runSessionManager;
                        if (root.Services.TryGet(out runSessionManager) && runSessionManager != null)
                        {
                            runSessionManager.ResumeFromChoice();
                        }
                    }

                    if (events != null)
                    {
                        events.Publish(new MusicChoiceModalStateChanged { IsOpen = false });
                        events.Publish(new MusicChoiceSelected
                        {
                            ChoiceIndex = 0,
                            OptionIndex = 0,
                            TrackId = string.Empty,
                            GenreId = string.Empty
                        });
                    }
                }

                if (Time.timeScale > 0.5f)
                {
                    yield break;
                }

                yield return null;
            }
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
                " (" + timeoutSecondsRealtime.ToString("F1", CultureInfo.InvariantCulture) + "s).");
        }
    }
}
