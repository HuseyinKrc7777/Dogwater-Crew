using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DogWater
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
    [RequireComponent(typeof(PlayerInput))]
#endif
    public class FirstPersonController : NetworkBehaviour
    {
        [Header("Player")]
        public float MoveSpeed = 4.0f;
        public float SprintSpeed = 6.0f;
        public float RotationSpeed = 1.0f;
        public float SpeedChangeRate = 10.0f;

        [Space(10)]
        public float JumpHeight = 1.2f;
        public float Gravity = -15.0f;

        [Space(10)]
        public float JumpTimeout = 0.1f;
        public float FallTimeout = 0.15f;

        [Header("Player Grounded")]
        public bool Grounded = true;
        public float GroundedOffset = -0.14f;
        public float GroundedRadius = 0.5f;
        public LayerMask GroundLayers;

        [Header("Ship Settings")]
        [SerializeField] private Ship ship;
        [Tooltip("If enabled, the player will tilt to match the ship's orientation (X and Z axis).")]
        [SerializeField] private bool _alignToShipRotation = true; 

        [Header("Cinemachine")]
        public GameObject CinemachineCameraTarget;
        public float TopClamp = 90.0f;
        public float BottomClamp = -90.0f;

        private float _cinemachineTargetPitch;
        private float _speed;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;

        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        private CharacterController _controller;
        private PlayerInputs _input;
        private GameObject _mainCamera;
        private GameObject followCam;

#if ENABLE_INPUT_SYSTEM
        private PlayerInput _playerInput;
#endif

        private const float _threshold = 0.01f;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (!IsOwner) return;

            _controller = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputs>();
#if ENABLE_INPUT_SYSTEM
            _playerInput = GetComponent<PlayerInput>();
#endif
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;

            followCam = GameObject.FindGameObjectWithTag("PlayerFollowCamera");
            if (followCam != null && followCam.TryGetComponent<CinemachineCamera>(out var vCam))
            {
                vCam.Target.TrackingTarget = CinemachineCameraTarget.transform;
            }
            _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
        }

        private void Update()
        {
            if (!IsOwner) return;

            if (_mainCamera == null) _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            if (followCam == null)
            {
                followCam = GameObject.FindGameObjectWithTag("PlayerFollowCamera");
                if (followCam != null && followCam.TryGetComponent<CinemachineCamera>(out var vCam))
                    vCam.Target.TrackingTarget = CinemachineCameraTarget.transform;
            }

            GroundedCheck();
            AlignOrientation(); 
            JumpAndGravity();
            Move(); 
        }

        private void LateUpdate()
        {
            if (!IsOwner) return;
            CameraRotation(); 
        }

        private void GroundedCheck()
        {
            Vector3 spherePosition = transform.position - (transform.up * GroundedOffset);
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
            
            if (Grounded)
            {
                Collider[] colliders = Physics.OverlapSphere(spherePosition, GroundedRadius, GroundLayers);
                foreach (var col in colliders)
                {
                    if (col.CompareTag("Ship"))
                    {
                        ship = col.GetComponentInParent<Ship>();
                        break;
                    }
                }
            }
            else ship = null;
        }

        private void AlignOrientation()
        {
     
            Vector3 targetUp = (_alignToShipRotation && ship != null) ? ship.transform.up : Vector3.up;
			
            if (Vector3.Angle(transform.up, targetUp) > 0.01f)
            {
                Quaternion targetRotation = Quaternion.FromToRotation(transform.up, targetUp) * transform.rotation;
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 15f);
            }
        }

        private void CameraRotation()
        {
            if (_input.look.sqrMagnitude >= _threshold)
            {
                float deltaTimeMultiplier = _playerInput.currentControlScheme == "KeyboardMouse" ? 1.0f : Time.deltaTime;

                _cinemachineTargetPitch += _input.look.y * RotationSpeed * deltaTimeMultiplier;
                _rotationVelocity = _input.look.x * RotationSpeed * deltaTimeMultiplier;

                _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);
                CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, 0.0f, 0.0f);

                transform.Rotate(Vector3.up * _rotationVelocity);
            }
        }

        private void Move()
        {
            float targetSpeed = _input.move == Vector2.zero ? 0.0f : (_input.sprint ? SprintSpeed : MoveSpeed);
            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

            if (Mathf.Abs(currentHorizontalSpeed - targetSpeed) > 0.1f)
            {
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed, Time.deltaTime * SpeedChangeRate);
            }
            else _speed = targetSpeed;

            Vector3 inputDirection = (transform.right * _input.move.x + transform.forward * _input.move.y).normalized;

            Vector3 playerMotion = inputDirection * (_speed * Time.deltaTime) + (transform.up * _verticalVelocity * Time.deltaTime);

            if (ship != null)
            {
                Vector3 shipTranslation = ship.velocityDelta;
                Vector3 relativePos = transform.position - ship.transform.position;
                Vector3 rotatedPos = ship.deltaRot * relativePos;
                Vector3 shipRotationDisplacement = (rotatedPos - relativePos);

                _controller.Move(playerMotion + shipTranslation + shipRotationDisplacement);
            }
            else
            {
                _controller.Move(playerMotion);
            }
        }

        private void JumpAndGravity()
        {
            if (Grounded)
            {
                _fallTimeoutDelta = FallTimeout;
                if (_verticalVelocity < 0.0f) _verticalVelocity = -0.5f; 

                if (_input.jump && _jumpTimeoutDelta <= 0.0f)
                {
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                }
                if (_jumpTimeoutDelta >= 0.0f) _jumpTimeoutDelta -= Time.deltaTime;
            }
            else
            {
                _jumpTimeoutDelta = JumpTimeout;
                if (_fallTimeoutDelta >= 0.0f) _fallTimeoutDelta -= Time.deltaTime;
                _input.jump = false;
                
                if (_verticalVelocity < _terminalVelocity) _verticalVelocity += Gravity * Time.deltaTime;
            }
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }
    }
}