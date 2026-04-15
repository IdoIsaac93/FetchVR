using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace FetchVR.Dog
{
    /// <summary>
    /// Keyboard-driven game controller for testing the fetch loop.
    /// Manages the full cycle: ball in hand → throw → dog fetches → dog returns → pet → repeat.
    ///
    /// Keys:
    ///   T = Throw ball
    ///   F = Command dog to fetch
    ///   P = Pet dog (completes round, ball returns to hand)
    /// </summary>
    public class FetchGameController : MonoBehaviour
    {
        private enum XRButton
        {
            TriggerButton,
            GripButton,
            PrimaryButton,
            SecondaryButton,
            MenuButton,
            Primary2DAxisClick
        }

        public enum GameState
        {
            BallInHand,     // Ball at player hand, waiting for throw
            BallThrown,     // Ball landed, waiting for fetch command
            DogFetching     // Dog is going to get the ball / returning
        }

        [Header("References")]
        [SerializeField] private FetchAgent fetchAgent;
        [SerializeField] private FetchBall fetchBall;
        [SerializeField] private Transform playerTransform;  // The goal / VR camera rig
        [SerializeField] private Transform xrRightControllerTransform;
        [SerializeField] private DogStatusController dogStatus;
        [SerializeField] private Animator dogAnimator;

        [Header("Hand Settings")]
        [Tooltip("Offset from player position where the ball is held")]
        [SerializeField] private Vector3 handOffset = new Vector3(0.5f, 1.0f, 0.5f);
        [Tooltip("Offset in front of the right XR controller where the ball is held before throwing")]
        [SerializeField] private Vector3 xrHandOffset = new Vector3(0f, -0.02f, 0.18f);

        [Header("Throw Settings")]
        [SerializeField] private float throwForce = 10f;
        [SerializeField] private float throwUpAngle = 25f;
        [SerializeField] private float throwReleaseForwardDistance = 0.45f;
        [SerializeField] private float throwReleaseUpDistance = 0.15f;
        [SerializeField] private float ignorePlayerCollisionDuration = 0.2f;
        [Tooltip("Multiplier applied to XR controller linear velocity when throwing in VR")]
        [SerializeField] private float xrThrowVelocityMultiplier = 1.0f;
        [Tooltip("Minimum controller speed required before a VR throw gets extra velocity")]
        [SerializeField] private float xrMinThrowSpeed = 0.25f;
        [Tooltip("If enabled, use a fallback forward speed when the XR controller has no usable velocity")]
        [SerializeField] private bool useXrFallbackThrowSpeed = false;
        [Tooltip("Fallback speed used only when the XR controller does not provide linear velocity and fallback is enabled")]
        [SerializeField] private float xrFallbackThrowSpeed = 8f;

        [Header("Keys")]
        [SerializeField] private KeyCode throwKey = KeyCode.T;
        [SerializeField] private KeyCode fetchKey = KeyCode.F;
        [SerializeField] private KeyCode resetKey = KeyCode.R;

        [Header("VR Input")]
        [SerializeField] private XRNode xrControllerNode = XRNode.RightHand;
        [SerializeField] private XRButton xrThrowButton = XRButton.TriggerButton;
        [SerializeField] private XRButton xrFetchButton = XRButton.PrimaryButton;
        [SerializeField] private XRButton xrResetButton = XRButton.SecondaryButton;

        [Header("Animation")]
        [SerializeField] private string runBoolName = "Run";

        [Header("Debug")]
        [SerializeField] private GameState currentState;
        [SerializeField] private int roundCount;
        [SerializeField] private bool disableOutsideGameMode = true;

        private bool m_IsActiveForCurrentMode = true;
        private InputAction m_XrThrowAction;
        private InputAction m_XrFetchAction;
        private InputAction m_XrResetAction;
        private InputAction m_XrVelocityAction;
        private InputAction m_XrPositionAction;
        private InputAction m_XrRotationAction;

        private void Awake()
        {
            EnsureXrActionsCreated();
            TryAutoAssignRightControllerTransform();
            m_IsActiveForCurrentMode = IsGameMode();

            if (!m_IsActiveForCurrentMode && disableOutsideGameMode)
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (!m_IsActiveForCurrentMode)
            {
                return;
            }

            if (fetchAgent != null)
            {
                fetchAgent.OnFetchSuccess += HandleDogReturned;
            }

            EnableXrActions();
        }

        private void OnDisable()
        {
            if (!m_IsActiveForCurrentMode)
            {
                return;
            }

            if (fetchAgent != null)
            {
                fetchAgent.OnFetchSuccess -= HandleDogReturned;
            }

            DisableXrActions();
        }

        private void Start()
        {
            if (!m_IsActiveForCurrentMode)
            {
                return;
            }

            // Begin with ball in hand
            EnterBallInHand();
        }

        private void Update()
        {
            if (!m_IsActiveForCurrentMode)
            {
                return;
            }

            TryAutoAssignRightControllerTransform();

            bool resetPressed = Input.GetKeyDown(resetKey) || GetXrButtonDown(m_XrResetAction);
            if (resetPressed)
            {
                ResetRound();
                return;
            }

            switch (currentState)
            {
                case GameState.BallInHand:
                    // Keep ball at hand position
                    fetchBall.HoldAtPosition(GetHandPosition());

                    bool keyboardThrowPressed = Input.GetKeyDown(throwKey);
                    bool xrThrowPressed = GetXrButtonDown(m_XrThrowAction);
                    if (keyboardThrowPressed || xrThrowPressed)
                    {
                        ThrowBall(useVrControllerAim: xrThrowPressed && !keyboardThrowPressed);
                    }
                    break;

                case GameState.BallThrown:
                    if (Input.GetKeyDown(fetchKey) || GetXrButtonDown(m_XrFetchAction))
                    {
                        CommandDogFetch();
                    }
                    break;

                case GameState.DogFetching:
                    // Waiting for dog to return — handled by event
                    break;

            }
        }

        private bool IsGameMode()
        {
            if (fetchAgent != null && fetchAgent.area != null)
            {
                FetchArea area = fetchAgent.area.GetComponent<FetchArea>();
                if (area != null)
                {
                    return area.isGameMode;
                }
            }

            if (fetchBall != null)
            {
                return fetchBall.isGameMode;
            }

            return true;
        }

        private Vector3 GetHandPosition()
        {
            Transform vrReference = GetVrThrowReference();
            if (vrReference != null)
            {
                return vrReference.TransformPoint(xrHandOffset);
            }

            if (m_XrPositionAction != null && m_XrRotationAction != null)
            {
                Vector3 xrPosition = m_XrPositionAction.ReadValue<Vector3>();
                Quaternion xrRotation = m_XrRotationAction.ReadValue<Quaternion>();
                if (xrPosition.sqrMagnitude > 0.0001f)
                {
                    Quaternion rotation = xrRotation == default ? Quaternion.identity : xrRotation;
                    return xrPosition + rotation * xrHandOffset;
                }
            }

            if (playerTransform == null) return transform.position + handOffset;
            return playerTransform.position
                   + playerTransform.right * handOffset.x
                   + playerTransform.up * handOffset.y
                   + playerTransform.forward * handOffset.z;
        }

        private Vector3 GetThrowDirection(bool useVrControllerAim)
        {
            Transform vrReference = GetVrThrowReference();
            if (useVrControllerAim && vrReference != null)
            {
                return vrReference.forward.normalized;
            }

            if (useVrControllerAim && m_XrRotationAction != null)
            {
                Quaternion xrRotation = m_XrRotationAction.ReadValue<Quaternion>();
                if (xrRotation != default)
                {
                    return (xrRotation * Vector3.forward).normalized;
                }
            }

            Vector3 forward = playerTransform != null ? playerTransform.forward : transform.forward;
            // Add some upward angle
            return Quaternion.AngleAxis(-throwUpAngle, playerTransform != null ? playerTransform.right : transform.right) * forward;
        }

        private Transform GetVrThrowReference()
        {
            if (xrRightControllerTransform != null)
            {
                return xrRightControllerTransform;
            }

            return null;
        }

        // ───────────────────────────── State transitions ─────────────────────────────

        private void EnterBallInHand()
        {
            SetDogRun(false);
            currentState = GameState.BallInHand;
            Debug.Log($"[FetchGame] Ball in hand. Press [{throwKey}] to throw.");
        }

        private void ThrowBall(bool useVrControllerAim)
        {
            Transform vrReference = GetVrThrowReference();
            bool hasVrReference = useVrControllerAim && vrReference != null;
            Transform reference = hasVrReference
                ? vrReference
                : (playerTransform != null ? playerTransform : transform);
            Vector3 releasePosition = useVrControllerAim
                ? GetHandPosition()
                : reference.position
                  + reference.forward * throwReleaseForwardDistance
                  + reference.up * throwReleaseUpDistance;
            Vector3 velocity = useVrControllerAim
                ? GetVrThrowVelocity(reference)
                : GetThrowDirection(false) * throwForce;
            Transform colliderReference = hasVrReference ? reference.root : reference.root;
            Collider[] playerColliders = colliderReference.GetComponentsInChildren<Collider>(true);
            fetchBall.ThrowBallFrom(releasePosition, velocity, playerColliders, ignorePlayerCollisionDuration);

            currentState = GameState.BallThrown;
            Debug.Log($"[FetchGame] Ball thrown! Press [{fetchKey}] to command dog.");
        }

        private void CommandDogFetch()
        {
            if (fetchAgent == null)
            {
                Debug.LogError("[FetchGame] No FetchAgent assigned!");
                return;
            }

            fetchAgent.StartFetch();
            SetDogRun(true);

            currentState = GameState.DogFetching;
            Debug.Log("[FetchGame] Dog is fetching...");
        }

        private void HandleDogReturned()
        {
            SetDogRun(false);

            if (dogStatus != null)
            {
                dogStatus.AddTraining(1);
            }

            roundCount++;
            Debug.Log($"[FetchGame] Round {roundCount} complete! Dog level: {(dogStatus != null ? dogStatus.CurrentLevel : 0)}");

            EnterBallInHand();
        }

        private void ResetRound()
        {
            if (fetchAgent != null)
            {
                fetchAgent.CancelFetch();
                SetDogRun(false);

                if (fetchAgent.area != null)
                {
                    FetchArea area = fetchAgent.area.GetComponent<FetchArea>();
                    if (area != null)
                    {
                        area.PlaceAgentNearGoal(fetchAgent.gameObject);
                    }
                }
            }

            if (fetchBall != null)
            {
                fetchBall.PrepareForThrow();
                fetchBall.HoldAtPosition(GetHandPosition());
            }

            EnterBallInHand();
            Debug.Log($"[FetchGame] Round reset. Press [{throwKey}] to throw again.");
        }

        private void SetDogRun(bool isRunning)
        {
            if (dogAnimator == null || string.IsNullOrWhiteSpace(runBoolName))
            {
                return;
            }

            dogAnimator.SetBool(runBoolName, isRunning);
        }

        private bool GetXrButtonDown(InputAction action)
        {
            return action != null && action.WasPressedThisFrame();
        }

        private Vector3 GetVrThrowVelocity(Transform reference)
        {
            if (m_XrVelocityAction != null)
            {
                Vector3 deviceVelocity = m_XrVelocityAction.ReadValue<Vector3>();
                float speed = deviceVelocity.magnitude;
                if (speed >= xrMinThrowSpeed)
                {
                    return deviceVelocity * xrThrowVelocityMultiplier;
                }
            }

            if (useXrFallbackThrowSpeed)
            {
                return GetThrowDirection(true) * xrFallbackThrowSpeed;
            }

            return Vector3.zero;
        }

        private void EnsureXrActionsCreated()
        {
            if (m_XrThrowAction != null)
            {
                return;
            }

            string hand = xrControllerNode == XRNode.LeftHand ? "LeftHand" : "RightHand";

            m_XrThrowAction = CreateButtonAction($"<XRController>{{{hand}}}/{GetInputSystemButtonPath(xrThrowButton)}");
            m_XrFetchAction = CreateButtonAction($"<XRController>{{{hand}}}/{GetInputSystemButtonPath(xrFetchButton)}");
            m_XrResetAction = CreateButtonAction($"<XRController>{{{hand}}}/{GetInputSystemButtonPath(xrResetButton)}");
            m_XrVelocityAction = CreateValueAction($"<XRController>{{{hand}}}/deviceVelocity", expectedControlType: "Vector3");
            m_XrPositionAction = CreateValueAction($"<XRController>{{{hand}}}/devicePosition", expectedControlType: "Vector3");
            m_XrRotationAction = CreateValueAction($"<XRController>{{{hand}}}/deviceRotation", expectedControlType: "Quaternion");
        }

        private void EnableXrActions()
        {
            m_XrThrowAction?.Enable();
            m_XrFetchAction?.Enable();
            m_XrResetAction?.Enable();
            m_XrVelocityAction?.Enable();
            m_XrPositionAction?.Enable();
            m_XrRotationAction?.Enable();
        }

        private void DisableXrActions()
        {
            m_XrThrowAction?.Disable();
            m_XrFetchAction?.Disable();
            m_XrResetAction?.Disable();
            m_XrVelocityAction?.Disable();
            m_XrPositionAction?.Disable();
            m_XrRotationAction?.Disable();
        }

        private void TryAutoAssignRightControllerTransform()
        {
            if (xrRightControllerTransform != null)
            {
                return;
            }

            GameObject rightController = GameObject.Find("XR Controller Right");
            if (rightController != null)
            {
                xrRightControllerTransform = rightController.transform;
                return;
            }

            Transform fallback = transform.root.Find("XR Origin (XR Rig)/Camera Offset/XR Controller Right");
            if (fallback != null)
            {
                xrRightControllerTransform = fallback;
            }
        }

        private static InputAction CreateButtonAction(string binding)
        {
            var action = new InputAction(type: InputActionType.Button, binding: binding);
            action.wantsInitialStateCheck = false;
            return action;
        }

        private static InputAction CreateValueAction(string binding, string expectedControlType)
        {
            return new InputAction(type: InputActionType.Value, binding: binding, expectedControlType: expectedControlType);
        }

        private static string GetInputSystemButtonPath(XRButton button)
        {
            return button switch
            {
                XRButton.TriggerButton => "triggerPressed",
                XRButton.GripButton => "gripPressed",
                XRButton.PrimaryButton => "primaryButton",
                XRButton.SecondaryButton => "secondaryButton",
                XRButton.MenuButton => "menu",
                XRButton.Primary2DAxisClick => "primary2DAxisClick",
                _ => "triggerPressed"
            };
        }

        /*
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 350, 200));
            GUILayout.Label($"<size=16><b>Fetch Game - Round {roundCount}</b></size>");
            GUILayout.Label($"State: <b>{currentState}</b>");
            if (dogStatus != null)
            {
                GUILayout.Label($"Dog Level: {dogStatus.CurrentLevel}  |  " +
                                $"Satiety: {dogStatus.CurrentSatiety}/{dogStatus.MaxSatiety}  |  " +
                                $"Mood: {dogStatus.CurrentMood}/{dogStatus.MaxMood}  |  " +
                                $"Training: {dogStatus.CurrentTraining}/{dogStatus.MaxTraining}");
            }

            GUILayout.Space(5);
            switch (currentState)
            {
                case GameState.BallInHand:
                    GUILayout.Label($"Press <b>[{throwKey}]</b> to throw ball");
                    GUILayout.Label($"XR <b>[{xrThrowButton}]</b> to throw ball");
                    GUILayout.Label($"Press <b>[{resetKey}]</b> to reset round");
                    break;
                case GameState.BallThrown:
                    GUILayout.Label($"Press <b>[{fetchKey}]</b> to command dog");
                    GUILayout.Label($"XR <b>[{xrFetchButton}]</b> to command dog");
                    GUILayout.Label($"Press <b>[{resetKey}]</b> to reset round");
                    break;
                case GameState.DogFetching:
                    GUILayout.Label($"Press <b>[{resetKey}]</b> to reset round");
                    GUILayout.Label($"XR <b>[{xrResetButton}]</b> to reset round");
                    string phase = fetchAgent != null ? fetchAgent.currentPhase.ToString() : "?";
                    bool hasBall = fetchAgent != null && fetchAgent.hasBall;
                    GUILayout.Label($"Dog phase: <b>{phase}</b> | hasBall: {hasBall}");
                    if (fetchAgent != null && fetchBall != null)
                    {
                        float distToBall = Vector3.Distance(fetchAgent.transform.position, fetchBall.transform.position);
                        float distToGoal = Vector3.Distance(fetchAgent.transform.position, playerTransform.position);
                        float speed = fetchAgent.GetComponent<Rigidbody>() != null
                            ? fetchAgent.GetComponent<Rigidbody>().linearVelocity.magnitude : 0f;
                        GUILayout.Label($"To ball: <b>{distToBall:F1}m</b> | To player: <b>{distToGoal:F1}m</b> | Speed: <b>{speed:F1}</b>");
                    }
                    break;
            }
            GUILayout.EndArea();
        }
        */
    }
}
