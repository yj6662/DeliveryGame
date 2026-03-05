#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DeliveryRun.EditorTools
{
    public static class LobbyUIAddressablesConfigurator
    {
        public static bool TryConfigureLobbyUiAddressable(string assetPath, string addressKey)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogWarning("[LobbyUIAddressablesConfigurator] Asset path is empty.");
                return false;
            }

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogWarning("[LobbyUIAddressablesConfigurator] Asset not found: " + assetPath);
                return false;
            }

            Type settingsDefaultType = Type.GetType(
                "UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject, Unity.Addressables.Editor");
            if (settingsDefaultType == null)
            {
                Debug.LogWarning("[LobbyUIAddressablesConfigurator] Addressables package not found. Skip setup.");
                return false;
            }

            PropertyInfo settingsProperty = settingsDefaultType.GetProperty(
                "Settings",
                BindingFlags.Public | BindingFlags.Static);
            object settings = settingsProperty != null ? settingsProperty.GetValue(null, null) : null;
            if (settings == null)
            {
                Debug.LogWarning("[LobbyUIAddressablesConfigurator] Addressables settings not found. Skip setup.");
                return false;
            }

            object group = InvokeMethod(settings, "FindGroup", "UI_Lobby");
            if (group == null)
            {
                Debug.LogWarning("[LobbyUIAddressablesConfigurator] Group 'UI_Lobby' not found. Skip setup.");
                return false;
            }

            object entry = CreateOrMoveEntry(settings, guid, group);
            if (entry == null)
            {
                Debug.LogWarning("[LobbyUIAddressablesConfigurator] Failed to create/move addressable entry.");
                return false;
            }

            SetAddress(entry, string.IsNullOrEmpty(addressKey) ? LobbySceneBuilder.LobbyAddressKey : addressKey);
            SetLabel(entry, "ui");
            SetLabel(entry, "ui:lobby");

            UnityEngine.Object settingsObject = settings as UnityEngine.Object;
            if (settingsObject != null)
            {
                EditorUtility.SetDirty(settingsObject);
            }

            UnityEngine.Object entryObject = entry as UnityEngine.Object;
            if (entryObject != null)
            {
                EditorUtility.SetDirty(entryObject);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[LobbyUIAddressablesConfigurator] Addressable configured: " + assetPath);
            return true;
        }

        private static object CreateOrMoveEntry(object settings, string guid, object group)
        {
            if (settings == null || string.IsNullOrEmpty(guid) || group == null)
            {
                return null;
            }

            MethodInfo method = settings.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "CreateOrMoveEntry" && m.GetParameters().Length >= 2);

            if (method == null)
            {
                return null;
            }

            ParameterInfo[] parameters = method.GetParameters();
            object[] args = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo parameter = parameters[i];
                if (i == 0)
                {
                    args[i] = guid;
                    continue;
                }

                if (i == 1)
                {
                    args[i] = group;
                    continue;
                }

                if (parameter.ParameterType == typeof(bool))
                {
                    args[i] = false;
                    continue;
                }

                args[i] = parameter.HasDefaultValue ? parameter.DefaultValue : null;
            }

            try
            {
                return method.Invoke(settings, args);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[LobbyUIAddressablesConfigurator] CreateOrMoveEntry failed: " + ex.Message);
                return null;
            }
        }

        private static void SetAddress(object entry, string addressKey)
        {
            if (entry == null || string.IsNullOrEmpty(addressKey))
            {
                return;
            }

            Type entryType = entry.GetType();
            PropertyInfo addressProperty = entryType.GetProperty(
                "address",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (addressProperty != null && addressProperty.CanWrite)
            {
                addressProperty.SetValue(entry, addressKey, null);
                return;
            }

            FieldInfo addressField = entryType.GetField(
                "address",
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (addressField != null)
            {
                addressField.SetValue(entry, addressKey);
            }
        }

        private static void SetLabel(object entry, string label)
        {
            if (entry == null || string.IsNullOrEmpty(label))
            {
                return;
            }

            MethodInfo[] methods = entry.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == "SetLabel")
                .OrderBy(m => m.GetParameters().Length)
                .ToArray();

            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 0 || parameters[0].ParameterType != typeof(string))
                {
                    continue;
                }

                object[] args = new object[parameters.Length];
                args[0] = label;

                bool firstBoolAssigned = false;
                for (int p = 1; p < parameters.Length; p++)
                {
                    ParameterInfo parameter = parameters[p];
                    if (parameter.ParameterType == typeof(bool))
                    {
                        args[p] = !firstBoolAssigned;
                        firstBoolAssigned = true;
                        continue;
                    }

                    if (parameter.ParameterType.IsEnum)
                    {
                        args[p] = Activator.CreateInstance(parameter.ParameterType);
                        continue;
                    }

                    args[p] = parameter.HasDefaultValue ? parameter.DefaultValue : null;
                }

                try
                {
                    method.Invoke(entry, args);
                    return;
                }
                catch
                {
                    // try next overload
                }
            }
        }

        private static object InvokeMethod(object target, string methodName, params object[] args)
        {
            if (target == null || string.IsNullOrEmpty(methodName))
            {
                return null;
            }

            MethodInfo[] methods = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                {
                    continue;
                }

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != (args == null ? 0 : args.Length))
                {
                    continue;
                }

                try
                {
                    return method.Invoke(target, args);
                }
                catch
                {
                    // try next overload
                }
            }

            return null;
        }
    }
}
#endif
