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

        private void Awake()
        {
            // Debug overlay is currently disabled by request.
            Destroy(gameObject);
        }

        private void OnGUI()
        {
            // Disabled.
        }

        private static string SafeText(string value)
        {
            return string.IsNullOrEmpty(value) ? "-" : value;
        }
    }
}
