using UnityEngine;
using Genevore.Core;
using Genevore.Combat;
using Genevore.Systems;

namespace Genevore.Player
{
    /// <summary>
    /// Mobile movement via Virtual Joystick (UI) + CharacterController.
    /// Stage 4: implements IMetabolismSpeedReceiver for BiomassMetabolism penalties.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class MobilePlayerController : MonoBehaviour, IMetabolismSpeedReceiver
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float rotationSpeed = 12f;
        [SerializeField] private float gravity = -18f;

        [Header("References")]
        [SerializeField] private DevourController devourController;
        [SerializeField] private GenomeManager genomeManager;
        [SerializeField] private DamageableEntity damageable;
        [SerializeField] private Transform cameraTransform;

        private Vector2 _moveInput;
        private CharacterController _cc;
        private float _verticalVelocity;
        private float _metabolismSpeedOverride = -1f;

        public Vector2 MoveInput
        {
            get => _moveInput;
            set => _moveInput = value;
        }

        public void SetMetabolismMoveSpeed(float speed)
        {
            _metabolismSpeedOverride = speed;
        }

        public float GetEffectiveMoveSpeed()
        {
            return _metabolismSpeedOverride > 0f ? _metabolismSpeedOverride : moveSpeed;
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
        }

        private void ApplyMovement()
        {
            Vector3 inputDir = new Vector3(_moveInput.x, 0f, _moveInput.y);
            if (inputDir.sqrMagnitude > 1f) inputDir.Normalize();

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

            if (worldDir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(worldDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }

            if (_cc.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }
            else
            {
                _verticalVelocity += gravity * Time.deltaTime;
            }

            float speed = GetEffectiveMoveSpeed();
            Vector3 motion = worldDir * speed + Vector3.up * _verticalVelocity;
            _cc.Move(motion * Time.deltaTime);
        }

        public void SetJoystickInput(Vector2 input)
        {
            _moveInput = input;
        }

        public void RequestDevour()
        {
            if (devourController != null)
            {
                devourController.ForceDevourCycle();
            }
        }
    }
}
