using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using DeliveryRun.UI;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

#if DELIVERYRUN_ADDRESSABLES
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

namespace DeliveryRun.Editor
{
    public static class DeliveryRunAddressablesSetup
    {
        private const string GroupUiCommon = "UI_Common";
        private const string GroupUiLobby = "UI_Lobby";
        private const string GroupUiRun = "UI_Run";
        private const string GroupAudioBgm = "Audio_BGM";
        private const string GroupAudioSfx = "Audio_SFX";
        private const string GroupAudioUi = "Audio_UI";
        private const string GroupSharedFallback = "Shared_Fallback";
        private const string BuiltInDataGroupName = "Built In Data";

        private const string RunHudPath = "Assets/Prefabs/UI/Run/RunHUD.prefab";
        private const string MusicModalPath = "Assets/Prefabs/UI/Run/MusicSelectionModal.prefab";
        private const string RunResultPath = "Assets/Prefabs/UI/Run/RunResultModal.prefab";

        private const string RunHudAddress = "ui/run/hud";
        private const string MusicModalAddress = "ui/run/music_selection_modal";
        private const string RunResultAddress = "ui/run/run_result_modal";

        private const string TestBgmAssetPath = "Assets/Audio/Generated/TestBgm.wav";
        private const string UiClickAssetPath = "Assets/Audio/Generated/UiClick.wav";
        private const string TestBgmAddress = "audio/bgm/test_bgm";
        private const string UiClickAddress = "audio/ui/click";

        [MenuItem("Tools/DeliveryRun/Addressables/Setup Standard Groups + Register UI/Audio")]
        public static void GenerateAll()
        {
#if !DELIVERYRUN_ADDRESSABLES
            InstallAddressablesPackage();
            Debug.Log("[AddressablesSetup] Package add attempted. Rerun GenerateAll after domain reload.");
            return;
#else
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
            {
                throw new Exception("[AddressablesSetup] Unable to get AddressableAssetSettings.");
            }

            EnsureLabels(settings);
            AddressableAssetGroup sharedFallbackGroup = EnsureStandardGroups(settings);
            NormalizeGroups(settings, sharedFallbackGroup);

            AddressableAssetGroup uiRunGroup = settings.FindGroup(GroupUiRun);
            AddressableAssetGroup audioBgmGroup = settings.FindGroup(GroupAudioBgm);
            AddressableAssetGroup audioUiGroup = settings.FindGroup(GroupAudioUi);
            if (uiRunGroup == null || audioBgmGroup == null || audioUiGroup == null)
            {
                throw new Exception("[AddressablesSetup] One or more required standard groups are missing.");
            }

            RegisterEntry(settings, uiRunGroup, RunHudPath, RunHudAddress, "ui", "ui:run");
            RegisterEntry(settings, uiRunGroup, MusicModalPath, MusicModalAddress, "ui", "ui:run");
            RegisterEntry(settings, uiRunGroup, RunResultPath, RunResultAddress, "ui", "ui:run");

            EnsureGeneratedAudioAssets();
            RegisterEntry(settings, audioBgmGroup, TestBgmAssetPath, TestBgmAddress, "audio", "audio:bgm");
            RegisterEntry(settings, audioUiGroup, UiClickAssetPath, UiClickAddress, "audio", "audio:ui");

            UpdateUiCatalogKeys();

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            DeliveryRunAddressablesValidator.ValidateStandardInternal(settings, true);
            ValidateUiRunLabelLoad();
            BuildAddressablesContent();

            Debug.Log(
                "[AddressablesSetup] Completed.\n" +
                "Groups: UI_Common, UI_Lobby, UI_Run, Audio_BGM, Audio_SFX, Audio_UI, Shared_Fallback\n" +
                "UI entries: " + RunHudAddress + ", " + MusicModalAddress + ", " + RunResultAddress + "\n" +
                "Audio entries: " + TestBgmAddress + ", " + UiClickAddress);
#endif
        }

#if !DELIVERYRUN_ADDRESSABLES
        private static void InstallAddressablesPackage()
        {
            Debug.Log("[AddressablesSetup] DELIVERYRUN_ADDRESSABLES is not enabled. Installing com.unity.addressables...");
            AddRequest request = Client.Add("com.unity.addressables");
            DateTime timeoutAt = DateTime.UtcNow.AddSeconds(180);

            while (!request.IsCompleted && DateTime.UtcNow < timeoutAt)
            {
                Thread.Sleep(250);
            }

            if (!request.IsCompleted)
            {
                Debug.LogError("[AddressablesSetup] Package install timed out.");
                return;
            }

            if (request.Status == StatusCode.Success)
            {
                Debug.Log("[AddressablesSetup] Installed package: " + request.Result.name + "@" + request.Result.version);
                return;
            }

            string error = request.Error != null ? request.Error.message : "unknown";
            Debug.LogError("[AddressablesSetup] Package install failed: " + error);
        }
#else
        private static AddressableAssetGroup EnsureStandardGroups(AddressableAssetSettings settings)
        {
            AddressableAssetGroup sharedFallback = GetOrCreateGroup(settings, GroupSharedFallback);
            GetOrCreateGroup(settings, GroupUiCommon);
            GetOrCreateGroup(settings, GroupUiLobby);
            GetOrCreateGroup(settings, GroupUiRun);
            GetOrCreateGroup(settings, GroupAudioBgm);
            GetOrCreateGroup(settings, GroupAudioSfx);
            GetOrCreateGroup(settings, GroupAudioUi);

            if (settings.DefaultGroup == null || settings.DefaultGroup.Name != GroupSharedFallback)
            {
                settings.DefaultGroup = sharedFallback;
            }

            return sharedFallback;
        }

        private static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings settings, string groupName)
        {
            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group != null)
            {
                return group;
            }

            return settings.CreateGroup(
                groupName,
                false,
                false,
                false,
                null,
                typeof(BundledAssetGroupSchema),
                typeof(ContentUpdateGroupSchema));
        }

        private static void NormalizeGroups(AddressableAssetSettings settings, AddressableAssetGroup sharedFallbackGroup)
        {
            var allowed = new HashSet<string>(StringComparer.Ordinal)
            {
                GroupUiCommon,
                GroupUiLobby,
                GroupUiRun,
                GroupAudioBgm,
                GroupAudioSfx,
                GroupAudioUi,
                GroupSharedFallback,
                BuiltInDataGroupName
            };

            for (int i = settings.groups.Count - 1; i >= 0; i--)
            {
                AddressableAssetGroup group = settings.groups[i];
                if (group == null)
                {
                    continue;
                }

                if (allowed.Contains(group.Name))
                {
                    continue;
                }

                var entriesToMove = new List<AddressableAssetEntry>();
                foreach (AddressableAssetEntry entry in group.entries)
                {
                    if (entry == null)
                    {
                        continue;
                    }

                    entriesToMove.Add(entry);
                }

                for (int e = 0; e < entriesToMove.Count; e++)
                {
                    settings.CreateOrMoveEntry(entriesToMove[e].guid, sharedFallbackGroup, false, false);
                }

                if (settings.DefaultGroup == group)
                {
                    settings.DefaultGroup = sharedFallbackGroup;
                }

                Debug.LogWarning("[AddressablesSetup] Removed non-standard group: " + group.Name);
                settings.RemoveGroup(group);
            }
        }

        private static void EnsureLabels(AddressableAssetSettings settings)
        {
            EnsureLabel(settings, "ui");
            EnsureLabel(settings, "ui:run");
            EnsureLabel(settings, "audio");
            EnsureLabel(settings, "audio:bgm");
            EnsureLabel(settings, "audio:ui");
        }

        private static void EnsureLabel(AddressableAssetSettings settings, string label)
        {
            List<string> labels = settings.GetLabels();
            if (labels != null && labels.Contains(label))
            {
                return;
            }

            settings.AddLabel(label, false);
        }

        private static void RegisterEntry(
            AddressableAssetSettings settings,
            AddressableAssetGroup group,
            string assetPath,
            string address,
            string labelA,
            string labelB)
        {
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                throw new Exception("[AddressablesSetup] Asset not found: " + assetPath);
            }

            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, false);
            if (entry == null)
            {
                throw new Exception("[AddressablesSetup] Failed to create/move entry: " + assetPath);
            }

            entry.address = address;
            entry.SetLabel(labelA, true, true, false);
            entry.SetLabel(labelB, true, true, false);
        }

        private static void EnsureGeneratedAudioAssets()
        {
            EnsureFolder("Assets/Audio");
            EnsureFolder("Assets/Audio/Generated");

            WriteSineWaveIfMissing(TestBgmAssetPath, 8.0f, 220f, 0.12f, false);
            WriteSineWaveIfMissing(UiClickAssetPath, 0.08f, 1200f, 0.35f, true);

            AssetDatabase.ImportAsset(TestBgmAssetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(UiClickAssetPath, ImportAssetOptions.ForceUpdate);
        }

        private static void WriteSineWaveIfMissing(
            string assetPath,
            float durationSeconds,
            float frequency,
            float amplitude,
            bool clickEnvelope)
        {
            if (File.Exists(assetPath))
            {
                return;
            }

            int sampleRate = 44100;
            int sampleCount = Mathf.Max(1, Mathf.RoundToInt(durationSeconds * sampleRate));
            var samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float sample = Mathf.Sin(2f * Mathf.PI * frequency * t) * amplitude;
                if (clickEnvelope)
                {
                    float envelope = Mathf.Exp(-38f * t);
                    sample *= envelope;
                }

                samples[i] = Mathf.Clamp(sample, -1f, 1f);
            }

            WriteWav(assetPath, samples, sampleRate);
        }

        private static void WriteWav(string assetPath, float[] samples, int sampleRate)
        {
            if (samples == null)
            {
                throw new ArgumentNullException(nameof(samples));
            }

            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath.Replace('/', Path.DirectorySeparatorChar));
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            const short channels = 1;
            const short bitsPerSample = 16;
            int byteRate = sampleRate * channels * (bitsPerSample / 8);
            short blockAlign = (short)(channels * (bitsPerSample / 8));
            int dataSize = samples.Length * blockAlign;

            using (var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream, Encoding.ASCII))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataSize);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write(channels);
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write(blockAlign);
                writer.Write(bitsPerSample);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(dataSize);

                for (int i = 0; i < samples.Length; i++)
                {
                    short sample16 = (short)Mathf.Clamp(samples[i] * 32767f, short.MinValue, short.MaxValue);
                    writer.Write(sample16);
                }
            }
        }

        private static void ValidateUiRunLabelLoad()
        {
            AsyncOperationHandle<IList<GameObject>> handle =
                Addressables.LoadAssetsAsync<GameObject>("ui:run", null, false);

            IList<GameObject> loaded = handle.WaitForCompletion();
            bool success = handle.Status == AsyncOperationStatus.Succeeded && loaded != null && loaded.Count > 0;
            Addressables.Release(handle);

            if (!success)
            {
                throw new Exception("[AddressablesSetup] Label load smoke test failed for label: ui:run");
            }
        }

        private static void BuildAddressablesContent()
        {
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (result == null)
            {
                throw new Exception("[AddressablesSetup] BuildPlayerContent returned null result.");
            }

            if (!string.IsNullOrEmpty(result.Error))
            {
                throw new Exception("[AddressablesSetup] BuildPlayerContent failed: " + result.Error);
            }
        }

        private static void UpdateUiCatalogKeys()
        {
            const string catalogPath = "Assets/Resources/Bootstrap/UiPrefabCatalog.asset";
            UiPrefabCatalogSO catalog = AssetDatabase.LoadAssetAtPath<UiPrefabCatalogSO>(catalogPath);
            if (catalog == null)
            {
                return;
            }

            catalog.RunHudPrefab = null;
            catalog.MusicSelectionModalPrefab = null;
            catalog.RunResultModalPrefab = null;
            catalog.RunHudKey = RunHudAddress;
            catalog.MusicSelectionModalKey = MusicModalAddress;
            catalog.RunResultModalKey = RunResultAddress;
            catalog.UiRunLabel = "ui:run";
            catalog.TestBgmKey = TestBgmAddress;
            catalog.UiClickKey = UiClickAddress;
            EditorUtility.SetDirty(catalog);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string[] parts = path.Split('/');
            if (parts.Length == 0)
            {
                return;
            }

            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
#endif
    }
}
