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
        [SerializeField] public Ship ship;
        [Tooltip("If enabled, the player will tilt to match the ship's orientation (X and Z axis).")]
        [SerializeField] private bool _alignToShipRotationPitchAndRoll = false;
        [SerializeField] private bool _alignToShipRotationYaw = false;


        [Header("Cinemachine")]
        public GameObject CinemachineCameraTarget;
        public float TopClamp = 90.0f;
        public float BottomClamp = -90.0f;

        private float _cinemachineTargetPitch;
        private float _speed;
        private float _rotationVelocity;
        public float _verticalVelocity;
        private float _terminalVelocity = 53.0f;

        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        private CharacterController _controller;
        private PlayerInputs _input;
        private GameObject _mainCamera;
        private GameObject followCam;
        private Player PlayerScript;

#if ENABLE_INPUT_SYSTEM
        private PlayerInput _playerInput;
#endif

        private const float _threshold = 0.01f;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // DO NOT return on !IsOwner here! Observers need their components enabled to run LateUpdate overrides!
            PlayerScript = GetComponent<Player>();
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputs>();
#if ENABLE_INPUT_SYSTEM
            _playerInput = GetComponent<PlayerInput>();
#endif
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;

            if (IsOwner)
            {
                followCam = GameObject.FindGameObjectWithTag("PlayerFollowCamera");
                if (followCam != null && followCam.TryGetComponent<CinemachineCamera>(out var vCam))
                {
                    vCam.Target.TrackingTarget = CinemachineCameraTarget.transform;
                }
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            }
        }

        // --- CUSTOM MOVING PLATFORM SYNC ---
        private NetworkVariable<bool> _netIsOnShip = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<Vector3> _netLocalPos = new NetworkVariable<Vector3>(Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private NetworkVariable<ulong> _netShipId = new NetworkVariable<ulong>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private Vector3 _observerVisualLocalPos;
        // -----------------------------------

        private void Update()
        {
            if (IsOwner)
            {
                if (_mainCamera == null) _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
                if (followCam == null)
                {
                    followCam = GameObject.FindGameObjectWithTag("PlayerFollowCamera");
                    if (followCam != null && followCam.TryGetComponent<CinemachineCamera>(out var vCam))
                    {
                        if(vCam.Target.TrackingTarget == null)
                            vCam.Target.TrackingTarget = CinemachineCameraTarget.transform;
                        
                    }
                }

                GroundedCheck();
                JumpAndGravity();
                Move();
                Interact();
                Menus();
                Items();
                // SYNC LOCAL POSITION TO OBSERVERS
                if (ship != null && ship.NetworkObject != null)
                {
                    if (!_netIsOnShip.Value) _netIsOnShip.Value = true;
                    _netShipId.Value = ship.NetworkObjectId;
                    _netLocalPos.Value = ship.transform.InverseTransformPoint(transform.position);
                }
                else
                {
                    if (_netIsOnShip.Value) _netIsOnShip.Value = false;
                }
            }
        }
        bool usingItem = false;

        int itemButtonPressed = -1;
        int itemUsed = -1;
        private void Items()
        {
            
            //eşyaya göre değişken değiştiriliyor
            if (_input.button1)
            {
                itemButtonPressed = 0;
                _input.button1 = false;
            }
            else if (_input.button2)
            {
                itemButtonPressed = 1;
                _input.button2 = false;
            }
            else if (_input.button3)
            {
                itemButtonPressed = 2;
                _input.button3 = false;
            }
            else if (_input.button4)
            {
                itemButtonPressed = 3;
                _input.button4 = false;
            }
            else
                itemButtonPressed = -1;
            if (PauseMenuController.Instance.isMenuOpen || PlayerScript.diary.open) 
                itemButtonPressed = -1;
            //basılan tuşa göre eşya takıp çıkarma işlemleri
            if (itemButtonPressed != -1)
            {
                Debug.LogError(PlayerScript.Items + itemButtonPressed.ToString());
                if (itemUsed != -1) //eğer aktif eşya varsa çıkarılıyor
                {
                    PlayerScript.Items[itemUsed].UnEquip();
                    usingItem = false;
                }
                if (itemUsed != itemButtonPressed) // eğer aktif eşya seçilen eşyadan farklı ise ; eşya takılıyor
                {
                    PlayerScript.Items[itemButtonPressed].Equip(this);
                    itemUsed = itemButtonPressed;
                    usingItem = true;
                }
                else
                    itemUsed = -1; // eğer seçilen eşya zaten bulunan eşya ise eşya yok diye işleniyor

            }
            //eşya kullanımı ile alakalı şeyler
            if (itemUsed != -1)
            {
                if (_input.interact)
                {
                    PlayerScript.Items[itemUsed].Interact();
                }
                else
                {
                    PlayerScript.Items[itemUsed].UnInteract();
                }
                if (_input.leftMouseButton && PlayerScript.Items[itemUsed].Interacting)
                {
                    PlayerScript.Items[itemUsed].Use1();
                }
                if (_input.rightMouseButton && PlayerScript.Items[itemUsed].Interacting)
                {
                    PlayerScript.Items[itemUsed].Use2();
                }


            }



        }
        private void Menus()
        {
            if (_input.menu == true)
            {
                if(PlayerScript.diary.open)
                {
                    PlayerScript.diary.CloseDiary();
                }
                else if (!PauseMenuController.Instance.isMenuOpen)
                {
                    Cursor.SetCursor(cursorOpen, Vector2.zero, CursorMode.Auto);
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;

                    PauseMenuController.Instance.OpenPauseMenu();

                }
                else
                {
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;

                    PauseMenuController.Instance.ClosePauseMenu();

                }

                _input.menu = false;
            }
        }
        private void FixedUpdate()
        {
        }
        private void LateUpdate()
        {
            if (IsOwner)
            {
                CameraRotation();
                AlignOrientation();

            }
            else
            {
                // OBSERVER: FLAWLESS JITTER-FREE VISUAL GLUE
                // Ignore NetworkTransform's jittery world space and use the owner's exact local position
                if (_netIsOnShip.Value && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(_netShipId.Value, out NetworkObject shipObj))
                {
                    // Prevent snapping if we just got on the ship
                    if (Vector3.Distance(_observerVisualLocalPos, _netLocalPos.Value) > 5f)
                    {
                        _observerVisualLocalPos = _netLocalPos.Value;
                    }

                    // Smoothly interpolate the 30-tick network updates into a silky smooth visual position
                    _observerVisualLocalPos = Vector3.Lerp(_observerVisualLocalPos, _netLocalPos.Value, Time.deltaTime * 15f);

                    // Override the final visual position relative to the ship!
                    transform.position = shipObj.transform.TransformPoint(_observerVisualLocalPos);
                }
                else
                {
                    // Not on a ship, let standard NetworkTransform handle world space
                    _observerVisualLocalPos = Vector3.zero;
                }
            }
        }

        private void GroundedCheck()
        {
            Vector3 spherePosition = transform.position - (transform.up * GroundedOffset);
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);

            if (Grounded)
            {
                /*
                Collider[] colliders = Physics.OverlapSphere(spherePosition, GroundedRadius, GroundLayers);
                bool foundShip = false;
                foreach (var col in colliders)
                {
                    if (col.CompareTag("Ship"))
                    {
                        ship = col.GetComponentInParent<Ship>();
                        foundShip = true;
                        break;
                    }
                }
                if (!foundShip && !_handMode) ship = null;
                */
            }

        }
        private float lastShipYaw;
        private float yawOffset;
        private float yawOffsetVelocity;

        [SerializeField] private float yawSmoothTime = 0.08f;
        [SerializeField] private float tiltLerpSpeed = 12f;

        private void AlignOrientation()
        {
            if (ship == null) return;

            if (_alignToShipRotationYaw)
            {
                float shipYaw = ship.transform.eulerAngles.y;

                float deltaYaw = Mathf.DeltaAngle(lastShipYaw, shipYaw);

                transform.Rotate(0f, deltaYaw, 0f, Space.World);

                lastShipYaw = shipYaw;
            }



            if (_alignToShipRotationPitchAndRoll)
            {
                Vector3 targetUp = ship.transform.up;

                Quaternion targetTilt =
                    Quaternion.FromToRotation(transform.up, targetUp) * transform.rotation;

                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetTilt,
                    Time.deltaTime * tiltLerpSpeed
                );

            }
        }
        bool cameraRotationAllowed = true;
        private void CameraRotation()
        {
            if (PauseMenuController.Instance.isMenuOpen || PlayerScript.diary.open) 
                    return;
            if (_input.look.sqrMagnitude >= _threshold && cameraRotationAllowed)
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
            

            float targetSpeed = _input.move == Vector2.zero ? 0.0f : (_input.sprint ? SprintSpeed : MoveSpeed);
            _speed = Mathf.Lerp(_speed, targetSpeed, Time.deltaTime * SpeedChangeRate);
            Vector3 inputDirection = (transform.right * _input.move.x + transform.forward * _input.move.y).normalized;
            Vector3 playerMotion = inputDirection * (_speed * Time.deltaTime);

            if (!moveInputEnabled)
            {
                playerMotion = Vector3.zero;
            }
            if (PauseMenuController.Instance.isMenuOpen || PlayerScript.diary.open) 
                playerMotion = Vector3.zero;


            Vector3 verticalMotion = Vector3.zero;
            if (!Grounded || _verticalVelocity > 0f)
            {
                verticalMotion = transform.up * (_verticalVelocity * Time.deltaTime);
            }


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


                Vector3 relativePos = transform.position - _previousShipPosition;
                Quaternion shipRotationDelta = currentShipRot * Quaternion.Inverse(_previousShipRotation);
                Vector3 rotatedPos = shipRotationDelta * relativePos;
                Vector3 rotationDisplacement = rotatedPos - relativePos;

                Vector3 shipTranslation = currentShipPos - _previousShipPosition;

                if (!Grounded)
                {
                    shipTranslation.y = 0;
                }


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

                // ANTI-SLIDE FIX: Only apply the stick-to-ground downward force if NOT on a ship
                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = (ship != null) ? 0.0f : -0.5f;
                }

                
                if (_input.jump && _jumpTimeoutDelta <= 0.0f && moveInputEnabled)
                {
                    if (PauseMenuController.Instance.isMenuOpen || PlayerScript.diary.open) 
                        return;
                    if (ship != null)
                        _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity) + ship.VerticalVelocity;
                    else
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
        bool _isMouseClosed = false;
        bool _isRightMouseClosed = false;

        IHandInput CurrentHandInput;
        IInteractable CurrentInteract;
        Vector2 startPos = Vector2.zero;

        private float handModeTimeoutCounter = 0.0f;
        private void Interact()
        {
            if (PauseMenuController.Instance.isMenuOpen || usingItem || PlayerScript.diary.open) // menü açıksa veya eşya kullanımdaysa etkleşim kapanıyor
                return;
            if (_isMouseClosed && CurrentHandInput != null)
            {
                Vector2 currentPos = Mouse.current.position.value;
                Vector2 value = (startPos - currentPos) / 1000;
                CurrentHandInput.OnHandInput(value.x, value.y);
                startPos = currentPos;
            }

            if (_isRightMouseClosed && CurrentHandInput != null)
            {
                Vector2 currentPos = Mouse.current.position.value;
                Vector2 value = (startPos - currentPos) / 1000;
                CurrentHandInput.OnRightHandInput(value.x, value.y);
                startPos = currentPos;
            }
            if (_handMode)
            {
                if (_input.leftMouseButton && !_isMouseClosed)
                {
                    Cursor.SetCursor(cursorClosed, Vector2.zero, CursorMode.Auto);
                    _isMouseClosed = true;
                    startPos = Mouse.current.position.value;
                }
                else if (!_input.leftMouseButton && _isMouseClosed)
                {

                    Cursor.SetCursor(cursorOpen, Vector2.zero, CursorMode.Auto);
                    _isMouseClosed = false;
                }

                if (_input.rightMouseButton && !_isRightMouseClosed)
                {
                    Cursor.SetCursor(cursorClosed, Vector2.zero, CursorMode.Auto);
                    _isRightMouseClosed = true;
                    startPos = Mouse.current.position.value;
                }
                else if (!_input.rightMouseButton && _isRightMouseClosed)
                {

                    Cursor.SetCursor(cursorOpen, Vector2.zero, CursorMode.Auto);
                    _isRightMouseClosed = false;
                }

                if (_input.jump)
                {
                    CurrentHandInput.OnButtonInput();
                    _input.jump = false;
                }
            }
            if (_input.interact)
            {

                RaycastHit hit;
                Physics.Raycast(CinemachineCameraTarget.transform.position, CinemachineCameraTarget.transform.forward, out hit, 2f);
                if (hit.collider != null && hit.collider.TryGetComponent<IInteractable>(out var interactable) && hit.collider.TryGetComponent<IHandInput>(out var handInput))
                {
                    if (!_handMode)
                    {
                        ship = hit.collider.GetComponentInParent<Ship>();
                        interactable.OnInteract(PlayerScript);
                        CurrentHandInput = handInput;
                        CurrentInteract = interactable;
                        EnterHandMode();
                    }

                }
                else if (_handMode)
                {
                    handModeTimeoutCounter += Time.deltaTime;
                    if (handModeTimeoutCounter > 3)
                    {
                        ExitHandMode();
                        CurrentInteract.OnUnInteract(PlayerScript);
                        CurrentInteract = null;
                        CurrentHandInput = null;
                        handModeTimeoutCounter = 0;
                    }

                }

            }
            else if (_handMode)
            {
                ExitHandMode();
                CurrentInteract.OnUnInteract(PlayerScript);
                CurrentInteract = null;
                CurrentHandInput = null;
            }
        }
        [SerializeField] private bool _handMode = false;
        [SerializeField] Texture2D cursorClosed;
        [SerializeField] Texture2D cursorOpen;
        public void EnterHandMode()
        {
            _handMode = true;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.Confined;
            moveInputEnabled = false;
            _alignToShipRotationPitchAndRoll = true;
            _alignToShipRotationYaw = true;

            cameraRotationAllowed = false;

        }
        public void ExitHandMode()
        {
            _handMode = false;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            moveInputEnabled = true;
            _alignToShipRotationPitchAndRoll = false;
            _alignToShipRotationYaw = false;
            cameraRotationAllowed = true;
            ship = null;
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Ship"))
            {
                ship = other.GetComponentInParent<Ship>();

            }
        }

        void OnTriggerStay(Collider other)
        {
            if (ship != null)
                return;
            if (other.CompareTag("Ship"))
            {
                ship = other.GetComponentInParent<Ship>();

            }
        }

        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Ship"))
            {
                if (!_handMode)
                    ship = null;

            }
        }
    }
}