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
        [SerializeField] private bool _alignToShipRotation = false;

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
            Interact();
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

        private Vector3 lastAppliedBoatDelta;
        [SerializeField] private bool moveInputEnabled = true;
        
        private Ship _previousShip;
        private Vector3 _previousShipPosition;
        private Quaternion _previousShipRotation;

        private void Move()
        {
            if (!IsOwner) return;

            // 1. CALCULATE WALKING
            float targetSpeed = _input.move == Vector2.zero ? 0.0f : (_input.sprint ? SprintSpeed : MoveSpeed);
            _speed = Mathf.Lerp(_speed, targetSpeed, Time.deltaTime * SpeedChangeRate);
            Vector3 inputDirection = (transform.right * _input.move.x + transform.forward * _input.move.y).normalized;
            Vector3 playerMotion = inputDirection * (_speed * Time.deltaTime);

            if(!moveInputEnabled)
            {
                playerMotion = Vector3.zero;
            }
            
            Vector3 verticalMotion = transform.up * (_verticalVelocity * Time.deltaTime);
            
            // 3. APPLY BOAT SYNC
            if (ship != null)
            {
                if (_previousShip != ship)
                {
                    _previousShip = ship;
                    _previousShipPosition = ship.transform.position;
                    _previousShipRotation = ship.transform.rotation;
                }

                Vector3 currentShipPos = ship.transform.position;
                Quaternion currentShipRot = ship.transform.rotation;

                // Calculate displacement caused by ship rotation
                Vector3 relativePos = transform.position - _previousShipPosition;
                Quaternion shipRotationDelta = currentShipRot * Quaternion.Inverse(_previousShipRotation);
                Vector3 rotatedPos = shipRotationDelta * relativePos;
                Vector3 rotationDisplacement = rotatedPos - relativePos;
                
                Vector3 shipTranslation = currentShipPos - _previousShipPosition;

                if (!Grounded) 
                {
                    shipTranslation.y = 0;
                }
                
                // Final Move: Walk + Gravity + Ship Move + Ship Rotate
                _controller.Move(playerMotion + verticalMotion + shipTranslation + rotationDisplacement);

                _previousShipPosition = ship.transform.position;
                _previousShipRotation = ship.transform.rotation;
            }
            else
            {
                _previousShip = null;
                _controller.Move(playerMotion + verticalMotion);
            }
        }

        private void JumpAndGravity()
        {
            if (Grounded)
            {
                _fallTimeoutDelta = FallTimeout;
                if (_verticalVelocity < 0.0f) _verticalVelocity = -0.5f;

                if (_input.jump && _jumpTimeoutDelta <= 0.0f && moveInputEnabled)
                {
                    if(ship!=null)
                        _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity) + ship.VerticalVelocity;
                    else
                        _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity) ;
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

        private void Interact()
        {
            if(_input.interact)
            {
                RaycastHit hit;
                Physics.Raycast(CinemachineCameraTarget.transform.position, CinemachineCameraTarget.transform.forward,out hit, 2f);
                if(hit.collider != null && hit.collider.TryGetComponent<IInteractable>(out var interactable))
                {
                    interactable.OnInteract();
                     
                }
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