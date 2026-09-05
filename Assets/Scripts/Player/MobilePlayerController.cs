using UnityEngine;
using Genevore.Core;
using Genevore.Combat;

namespace Genevore.Player
{
    /// <summary>
    /// Mobile movement via Virtual Joystick (UI) + CharacterController.
    /// Avoids Rigidbody to minimise physics CPU overhead on mid-range Android.
    /// Integrates contextual Devour trigger with DevourController from Stage 1.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class MobilePlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float rotationSpeed = 12f;
        [SerializeField] private float gravity = -18f;

        [Header("References")]
        [SerializeField] private DevourController devourController;
        [SerializeField] private GenomeManager genomeManager;
        [SerializeField] private DamageableEntity damageable;
        [SerializeField] private Transform cameraTransform; // optional; for relative movement

        // Virtual joystick input (set by UI Joystick script or external)
        private Vector2 _moveInput;
        private CharacterController _cc;
        private float _verticalVelocity;

        public Vector2 MoveInput
        {
            get => _moveInput;
            set => _moveInput = value;
        }

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (devourController == null) devourController = GetComponent<DevourController>();
            if (genomeManager == null) genomeManager = GetComponent<GenomeManager>();
            if (damageable == null) damageable = GetComponent<DamageableEntity>();

            if (damageable != null && genomeManager != null)
            {
                damageable.BindGenome(genomeManager);
            }
        }

        private void Update()
        {
            if (damageable != null && !damageable.IsAlive) return;

            ApplyMovement();
            // DevourController runs its own Update FSM; we only feed position.
            // Contextual devour is already handled inside DevourController via OverlapSphereNonAlloc.
        }

        private void ApplyMovement()
        {
            Vector3 inputDir = new Vector3(_moveInput.x, 0f, _moveInput.y);
            if (inputDir.sqrMagnitude > 1f) inputDir.Normalize();

            // Camera-relative if available, else world
            Vector3 worldDir;
            if (cameraTransform != null)
            {
                Vector3 camForward = cameraTransform.forward;
                camForward.y = 0f;
                camForward.Normalize();
                Vector3 camRight = cameraTransform.right;
                camRight.y = 0f;
                camRight.Normalize();
                worldDir = camForward * inputDir.z + camRight * inputDir.x;
            }
            else
            {
                worldDir = inputDir;
            }

            // Rotate toward movement
            if (worldDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(worldDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }

            // Gravity
            if (_cc.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }
            else
            {
                _verticalVelocity += gravity * Time.deltaTime;
            }

            Vector3 motion = worldDir * moveSpeed + Vector3.up * _verticalVelocity;
            _cc.Move(motion * Time.deltaTime);
        }

        /// <summary>
        /// Called by VirtualJoystick UI component every frame (or on value change).
        /// </summary>
        public void SetJoystickInput(Vector2 input)
        {
            _moveInput = input;
        }

        /// <summary>
        /// Optional manual devour button (UI). Forces the Stage-1 FSM into Executing.
        /// </summary>
        public void RequestDevour()
        {
            if (devourController != null)
            {
                devourController.ForceDevourCycle();
            }
        }
    }
}
