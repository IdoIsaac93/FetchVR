using UnityEngine;
using UnityEngine.Events;

namespace FetchVR.Dog
{
    public class DogTrainingSession : MonoBehaviour
    {
        [Header("Dog Status")]
        [SerializeField] private DogStatusController dogStatus;
        [SerializeField] private DogAudioController dogAudioController;
        [SerializeField] private int trainingGainPerSuccess = 1;

        [Header("Ball")]
        [SerializeField] private Rigidbody trainingBall;
        [SerializeField] private float minimumThrowSpeed = 1.5f;

        [Header("ML Agent")]
        [Tooltip("The FetchAgent controlling the dog. Assign this to enable AI-driven fetch.")]
        [SerializeField] private FetchAgent fetchAgent;
        [Tooltip("The FetchBall component on the training ball.")]
        [SerializeField] private FetchBall fetchBall;

        [Header("Manual Return (fallback if no ML Agent)")]
        [SerializeField] private Transform dogTransform;
        [SerializeField] private Transform returnPlatform;
        [SerializeField] private float platformReachDistance = 1.25f;

        [Header("Events")]
        [SerializeField] private UnityEvent onBallPickedUp = new UnityEvent();
        [SerializeField] private UnityEvent onBallThrown = new UnityEvent();
        [SerializeField] private UnityEvent onDogReturnedToPlatform = new UnityEvent();
        [SerializeField] private UnityEvent onTrainingCompleted = new UnityEvent();

        private bool ballPickedUp;
        private bool ballThrown;

        public bool IsTrainingInProgress => ballPickedUp || ballThrown;

        private void Awake()
        {
            if (dogAudioController == null && fetchAgent != null)
            {
                dogAudioController = fetchAgent.GetComponent<DogAudioController>();
            }
        }

        private void OnEnable()
        {
            if (fetchAgent != null)
            {
                fetchAgent.OnFetchSuccess += HandleAgentFetchSuccess;
            }
        }

        private void OnDisable()
        {
            if (fetchAgent != null)
            {
                fetchAgent.OnFetchSuccess -= HandleAgentFetchSuccess;
            }
        }

        /// <summary>
        /// Called when the VR player picks up the ball.
        /// Hook this to XR Grab Interactable's Select Entered event.
        /// </summary>
        public void NotifyBallPickedUp()
        {
            ballPickedUp = true;
            ballThrown = false;

            // Prepare ball for a new throw cycle
            if (fetchBall != null)
            {
                fetchBall.PrepareForThrow();
            }

            onBallPickedUp.Invoke();
        }

        /// <summary>
        /// Called when the VR player releases/throws the ball.
        /// Hook this to XR Grab Interactable's Select Exited event.
        /// </summary>
        public void NotifyBallThrown()
        {
            if (!ballPickedUp)
            {
                return;
            }

            if (trainingBall != null && trainingBall.linearVelocity.magnitude < minimumThrowSpeed)
            {
                return;
            }

            ballThrown = true;
            onBallThrown.Invoke();

            // Start the ML agent fetching
            if (fetchAgent != null)
            {
                fetchAgent.StartFetch();
                dogAudioController?.PlayFetchBark();
            }
        }

        /// <summary>
        /// Called by FetchAgent (via OnFetchSuccess event) when the dog returns the ball.
        /// This is the primary path when ML Agent is assigned.
        /// </summary>
        private void HandleAgentFetchSuccess()
        {
            if (!ballThrown) return;

            onDogReturnedToPlatform.Invoke();
            CompleteFetchRound();
        }

        /// <summary>
        /// Manual fallback: called by DogTrainingPlatformTrigger when no ML Agent is used.
        /// </summary>
        public void NotifyDogReturnedToPlatform()
        {
            // If using ML agent, ignore manual trigger — agent handles it via event
            if (fetchAgent != null) return;

            if (!ballThrown)
            {
                return;
            }

            if (!IsDogCloseEnoughToPlatform())
            {
                return;
            }

            onDogReturnedToPlatform.Invoke();
            CompleteFetchRound();
        }

        public void CancelCurrentTraining()
        {
            if (fetchAgent != null)
            {
                fetchAgent.CancelFetch();
            }
            ResetTrainingState();
        }

        private void CompleteFetchRound()
        {
            if (dogStatus != null)
            {
                dogStatus.AddTraining(trainingGainPerSuccess);
            }

            onTrainingCompleted.Invoke();
            ResetTrainingState();
        }

        private bool IsDogCloseEnoughToPlatform()
        {
            if (dogTransform == null || returnPlatform == null)
            {
                return true;
            }

            float distance = Vector3.Distance(dogTransform.position, returnPlatform.position);
            return distance <= platformReachDistance;
        }

        private void ResetTrainingState()
        {
            ballPickedUp = false;
            ballThrown = false;
        }
    }
}
