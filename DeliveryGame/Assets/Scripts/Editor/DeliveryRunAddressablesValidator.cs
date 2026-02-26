using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#if DELIVERYRUN_ADDRESSABLES
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
#endif

namespace DeliveryRun.Editor
{
    public static class DeliveryRunAddressablesValidator
    {
        private const string GroupUiCommon = "UI_Common";
        private const string GroupUiLobby = "UI_Lobby";
        private const string GroupUiRun = "UI_Run";
        private const string GroupAudioBgm = "Audio_BGM";
        private const string GroupAudioSfx = "Audio_SFX";
        private const string GroupAudioUi = "Audio_UI";
        private const string GroupSharedFallback = "Shared_Fallback";
        private const string BuiltInDataGroupName = "Built In Data";

#if DELIVERYRUN_ADDRESSABLES
        [MenuItem("Tools/DeliveryRun/Addressables/Validate Standard")]
        public static void ValidateStandard()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (settings == null)
            {
                throw new Exception("[AddressablesValidator] AddressableAssetSettings not found.");
            }

            ValidateStandardInternal(settings, true);
            Debug.Log("[AddressablesValidator] Validation passed.");
        }

        internal static void ValidateStandardInternal(AddressableAssetSettings settings, bool throwOnFail)
        {
            var errors = new List<string>(32);

            var allowedGroups = new HashSet<string>(StringComparer.Ordinal)
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

            for (int i = 0; i < settings.groups.Count; i++)
            {
                AddressableAssetGroup group = settings.groups[i];
                if (group == null)
                {
                    continue;
                }

                if (!allowedGroups.Contains(group.Name))
                {
                    errors.Add("Non-standard group detected: " + group.Name);
                }
            }

            ValidateGroupEntries(errors, settings, GroupUiRun, "ui/run/", "ui", "ui:run");
            ValidateGroupEntries(errors, settings, GroupAudioBgm, "audio/bgm/", "audio", "audio:bgm");
            ValidateGroupEntries(errors, settings, GroupAudioUi, "audio/ui/", "audio", "audio:ui");

            if (errors.Count == 0)
            {
                return;
            }

            for (int i = 0; i < errors.Count; i++)
            {
                Debug.LogError("[AddressablesValidator] " + errors[i]);
            }

            if (throwOnFail)
            {
                throw new Exception("[AddressablesValidator] Validation failed. Error count: " + errors.Count);
            }
        }

        private static void ValidateGroupEntries(
            List<string> errors,
            AddressableAssetSettings settings,
            string groupName,
            string addressPrefix,
            string requiredLabelA,
            string requiredLabelB)
        {
            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group == null)
            {
                errors.Add("Missing required group: " + groupName);
                return;
            }

            foreach (AddressableAssetEntry entry in group.entries)
            {
                if (entry == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(entry.address) ||
                    !entry.address.StartsWith(addressPrefix, StringComparison.Ordinal))
                {
                    errors.Add(
                        "Group " + groupName + " has invalid address '" + entry.address +
                        "' for entry guid=" + entry.guid + ". Expected prefix: " + addressPrefix);
                }

                if (entry.labels == null || !entry.labels.Contains(requiredLabelA))
                {
                    errors.Add("Entry " + entry.address + " in group " + groupName + " missing label " + requiredLabelA);
                }

                if (entry.labels == null || !entry.labels.Contains(requiredLabelB))
                {
                    errors.Add("Entry " + entry.address + " in group " + groupName + " missing label " + requiredLabelB);
                }
            }
        }
#else
        [MenuItem("Tools/DeliveryRun/Addressables/Validate Standard")]
        public static void ValidateStandard()
        {
            Debug.LogWarning("[AddressablesValidator] DELIVERYRUN_ADDRESSABLES is not enabled. Install Addressables package first.");
        }
#endif
    }
}
