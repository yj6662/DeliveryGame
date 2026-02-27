using DeliveryRun.Delivery.Input;
using UnityEngine;

namespace DeliveryRun.Delivery.Lobby
{
    [DisallowMultipleComponent]
    public sealed class LobbyAvatarController : MonoBehaviour
    {
        [SerializeField] private CharacterController characterController;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private float moveSpeed = 5.8f;
        [SerializeField] private float rotationSpeed = 540f;
        [SerializeField] private float gravity = -22f;
        [SerializeField] private float terminalVelocity = -30f;

        private float _verticalVelocity;

        public Vector3 Velocity { get; private set; }

        private void Awake()
        {
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }

            if (characterController == null)
            {
                characterController = gameObject.AddComponent<CharacterController>();
                characterController.height = 1.72f;
                characterController.radius = 0.34f;
                characterController.center = new Vector3(0f, 0.86f, 0f);
                characterController.stepOffset = 0.36f;
                characterController.slopeLimit = 48f;
            }

            if (visualRoot == null && transform.childCount > 0)
            {
                visualRoot = transform.GetChild(0);
            }
        }

        private void Update()
        {
            Vector2 input = RuntimeInput.ReadMove();
            Vector3 input3 = new Vector3(input.x, 0f, input.y);
            if (input3.sqrMagnitude > 1f)
            {
                input3.Normalize();
            }

            Camera cam = Camera.main;
            Vector3 camForward = Vector3.forward;
            Vector3 camRight = Vector3.right;
            if (cam != null)
            {
                camForward = cam.transform.forward;
                camRight = cam.transform.right;
                camForward.y = 0f;
                camRight.y = 0f;
                if (camForward.sqrMagnitude > 0.0001f)
                {
                    camForward.Normalize();
                }
                else
                {
                    camForward = Vector3.forward;
                }

                if (camRight.sqrMagnitude > 0.0001f)
                {
                    camRight.Normalize();
                }
                else
                {
                    camRight = Vector3.right;
                }
            }

            Vector3 moveDir = (camRight * input3.x) + (camForward * input3.z);
            if (moveDir.sqrMagnitude > 1f)
            {
                moveDir.Normalize();
            }

            if (moveDir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRot,
                    rotationSpeed * Time.deltaTime);
            }

            if (characterController.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -1f;
            }

            _verticalVelocity += gravity * Time.deltaTime;
            if (_verticalVelocity < terminalVelocity)
            {
                _verticalVelocity = terminalVelocity;
            }

            Vector3 velocity = moveDir * moveSpeed;
            velocity.y = _verticalVelocity;
            CollisionFlags flags = characterController.Move(velocity * Time.deltaTime);
            if ((flags & CollisionFlags.Below) != 0 && _verticalVelocity < 0f)
            {
                _verticalVelocity = -1f;
            }

            Velocity = new Vector3(moveDir.x * moveSpeed, 0f, moveDir.z * moveSpeed);

            if (visualRoot != null)
            {
                visualRoot.localPosition = Vector3.zero;
                visualRoot.localRotation = Quaternion.identity;
            }
        }
    }
}

