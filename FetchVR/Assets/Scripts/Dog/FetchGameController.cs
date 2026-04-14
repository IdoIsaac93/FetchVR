using UnityEngine;

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
        [SerializeField] private DogStatusController dogStatus;
        [SerializeField] private Animator dogAnimator;

        [Header("Hand Settings")]
        [Tooltip("Offset from player position where the ball is held")]
        [SerializeField] private Vector3 handOffset = new Vector3(0.5f, 1.0f, 0.5f);

        [Header("Throw Settings")]
        [SerializeField] private float throwForce = 10f;
        [SerializeField] private float throwUpAngle = 25f;
        [SerializeField] private float throwReleaseForwardDistance = 0.45f;
        [SerializeField] private float throwReleaseUpDistance = 0.15f;
        [SerializeField] private float ignorePlayerCollisionDuration = 0.2f;

        [Header("Keys")]
        [SerializeField] private KeyCode throwKey = KeyCode.T;
        [SerializeField] private KeyCode fetchKey = KeyCode.F;
        [SerializeField] private KeyCode resetKey = KeyCode.R;

        [Header("Animation")]
        [SerializeField] private string runBoolName = "Run";

        [Header("Debug")]
        [SerializeField] private GameState currentState;
        [SerializeField] private int roundCount;
        [SerializeField] private bool disableOutsideGameMode = true;

        private bool m_IsActiveForCurrentMode = true;

        private void Awake()
        {
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

            if (Input.GetKeyDown(resetKey))
            {
                ResetRound();
                return;
            }

            switch (currentState)
            {
                case GameState.BallInHand:
                    // Keep ball at hand position
                    fetchBall.HoldAtPosition(GetHandPosition());

                    if (Input.GetKeyDown(throwKey))
                    {
                        ThrowBall();
                    }
                    break;

                case GameState.BallThrown:
                    if (Input.GetKeyDown(fetchKey))
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
            if (playerTransform == null) return transform.position + handOffset;
            return playerTransform.position
                   + playerTransform.right * handOffset.x
                   + playerTransform.up * handOffset.y
                   + playerTransform.forward * handOffset.z;
        }

        private Vector3 GetThrowDirection()
        {
            Vector3 forward = playerTransform != null ? playerTransform.forward : transform.forward;
            // Add some upward angle
            return Quaternion.AngleAxis(-throwUpAngle, playerTransform != null ? playerTransform.right : transform.right) * forward;
        }

        // ───────────────────────────── State transitions ─────────────────────────────

        private void EnterBallInHand()
        {
            SetDogRun(false);
            currentState = GameState.BallInHand;
            Debug.Log($"[FetchGame] Ball in hand. Press [{throwKey}] to throw.");
        }

        private void ThrowBall()
        {
            Transform reference = playerTransform != null ? playerTransform : transform;
            Vector3 releasePosition = GetHandPosition()
                                      + reference.forward * throwReleaseForwardDistance
                                      + reference.up * throwReleaseUpDistance;
            Vector3 velocity = GetThrowDirection() * throwForce;
            Collider[] playerColliders = reference.root.GetComponentsInChildren<Collider>(true);
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
                    GUILayout.Label($"Press <b>[{resetKey}]</b> to reset round");
                    break;
                case GameState.BallThrown:
                    GUILayout.Label($"Press <b>[{fetchKey}]</b> to command dog");
                    GUILayout.Label($"Press <b>[{resetKey}]</b> to reset round");
                    break;
                case GameState.DogFetching:
                    GUILayout.Label($"Press <b>[{resetKey}]</b> to reset round");
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
    }
}
