using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace DeliveryRun.UI
{
    public static class UiEventSystemBootstrap
    {
        private const string InputSystemTypeName = "UnityEngine.InputSystem.InputSystem, Unity.InputSystem";
        private const string InputSystemUiModuleTypeName = "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem";

        private static EventSystem _persistentEventSystem;
        private static bool _sceneHooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RuntimeInitialize()
        {
            EnsureNow();
            HookSceneLoadedIfNeeded();
        }

        public static void EnsureNow()
        {
            EventSystem eventSystem = FindOrCreatePersistentEventSystem();
            if (eventSystem == null)
            {
                return;
            }

            EnsureInputModule(eventSystem.gameObject);
            EnsureInputUpdateMode();
            RemoveDuplicateEventSystems(eventSystem);
        }

        private static void HookSceneLoadedIfNeeded()
        {
            if (_sceneHooked)
            {
                return;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            _sceneHooked = true;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureNow();
        }

        private static EventSystem FindOrCreatePersistentEventSystem()
        {
            if (_persistentEventSystem != null)
            {
                if (!_persistentEventSystem.Equals(null))
                {
                    return _persistentEventSystem;
                }

                _persistentEventSystem = null;
            }

            EventSystem existing = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (existing != null)
            {
                _persistentEventSystem = existing;
                if (!_persistentEventSystem.enabled)
                {
                    _persistentEventSystem.enabled = true;
                }

                UnityEngine.Object.DontDestroyOnLoad(_persistentEventSystem.gameObject);
                return _persistentEventSystem;
            }

            GameObject eventSystemObject = new GameObject("EventSystem");
            _persistentEventSystem = eventSystemObject.AddComponent<EventSystem>();
            UnityEngine.Object.DontDestroyOnLoad(eventSystemObject);
            return _persistentEventSystem;
        }

        private static void RemoveDuplicateEventSystems(EventSystem keep)
        {
            EventSystem[] all = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                EventSystem candidate = all[i];
                if (candidate == null || candidate == keep)
                {
                    continue;
                }

                UnityEngine.Object.Destroy(candidate.gameObject);
            }
        }

        private static void EnsureInputModule(GameObject host)
        {
            if (host == null)
            {
                return;
            }

            Type inputSystemModuleType = Type.GetType(InputSystemUiModuleTypeName);
            BaseInputModule[] modules = host.GetComponents<BaseInputModule>();

            if (inputSystemModuleType != null)
            {
                bool hasInputSystemModule = false;
                for (int i = 0; i < modules.Length; i++)
                {
                    BaseInputModule module = modules[i];
                    if (module == null)
                    {
                        continue;
                    }

                    if (inputSystemModuleType.IsInstanceOfType(module))
                    {
                        module.enabled = true;
                        hasInputSystemModule = true;
                    }
                    else
                    {
                        module.enabled = false;
                        UnityEngine.Object.Destroy(module);
                    }
                }

                if (!hasInputSystemModule)
                {
                    host.AddComponent(inputSystemModuleType);
                }

                return;
            }

            bool hasStandalone = false;
            for (int i = 0; i < modules.Length; i++)
            {
                BaseInputModule module = modules[i];
                if (module == null)
                {
                    continue;
                }

                StandaloneInputModule standalone = module as StandaloneInputModule;
                if (standalone != null)
                {
                    standalone.enabled = true;
                    hasStandalone = true;
                    continue;
                }

                module.enabled = false;
                UnityEngine.Object.Destroy(module);
            }

            if (!hasStandalone)
            {
                host.AddComponent<StandaloneInputModule>();
            }
        }

        private static void EnsureInputUpdateMode()
        {
#if ENABLE_INPUT_SYSTEM
            try
            {
                // Reflection path keeps this assembly independent from InputSystem references.
                Type inputSystemType = Type.GetType(InputSystemTypeName);
                if (inputSystemType == null)
                {
                    return;
                }

                var settingsProperty = inputSystemType.GetProperty("settings");
                object settings = settingsProperty != null ? settingsProperty.GetValue(null, null) : null;
                if (settings == null)
                {
                    return;
                }

                Type settingsType = settings.GetType();
                var updateModeProperty = settingsType.GetProperty("updateMode");
                if (updateModeProperty == null || !updateModeProperty.CanRead || !updateModeProperty.CanWrite)
                {
                    return;
                }

                object current = updateModeProperty.GetValue(settings, null);
                Type enumType = updateModeProperty.PropertyType;

                object fixedMode;
                bool hasFixedMode = TryParseEnum(enumType, "ProcessEventsInFixedUpdate", out fixedMode);
                object manualMode;
                bool hasManualMode = TryParseEnum(enumType, "ProcessEventsManually", out manualMode);
                if (!hasFixedMode && !hasManualMode)
                {
                    return;
                }

                bool needsDynamic = false;
                if (current != null)
                {
                    if (hasFixedMode && current.Equals(fixedMode))
                    {
                        needsDynamic = true;
                    }
                    else if (hasManualMode && current.Equals(manualMode))
                    {
                        needsDynamic = true;
                    }
                }

                if (!needsDynamic)
                {
                    return;
                }

                object dynamicMode;
                if (!TryParseEnum(enumType, "ProcessEventsInDynamicUpdate", out dynamicMode))
                {
                    return;
                }

                updateModeProperty.SetValue(settings, dynamicMode, null);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[UiEventSystemBootstrap] Failed to force InputSystem dynamic update mode: " + ex.Message);
            }
#endif
        }

        private static bool TryParseEnum(Type enumType, string name, out object value)
        {
            value = null;
            if (enumType == null || string.IsNullOrEmpty(name))
            {
                return false;
            }

            try
            {
                value = Enum.Parse(enumType, name);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
