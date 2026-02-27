using UnityEngine;

namespace DeliveryRun.Delivery.Lobby
{
    [DisallowMultipleComponent]
    public sealed class LobbyInteractZone : MonoBehaviour
    {
        [SerializeField] private LobbyInteractType interactType = LobbyInteractType.OpenGaragePanel;
        [SerializeField] private string promptText = "Press F";
        [SerializeField] private float interactRadius = 2.4f;

        public LobbyInteractType InteractType => interactType;
        public string PromptText => string.IsNullOrEmpty(promptText) ? "Press F" : promptText;
        public float InteractRadius => Mathf.Max(0.25f, interactRadius);

        public bool IsInRange(Vector3 worldPosition)
        {
            Vector3 delta = transform.position - worldPosition;
            delta.y = 0f;
            return delta.sqrMagnitude <= (InteractRadius * InteractRadius);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.98f, 0.84f, 0.22f, 0.95f);
            Vector3 center = transform.position;
            center.y += 0.1f;
            Gizmos.DrawWireSphere(center, InteractRadius);
        }
#endif
    }
}

