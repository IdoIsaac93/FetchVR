using UnityEngine;
using UnityEngine.Events;

namespace FetchVR.Dog
{
    public class DogTrainingSession : MonoBehaviour
    {
        [SerializeField] private DogStatusController dogStatus;
        [SerializeField] private Rigidbody trainingBall;
        [SerializeField] private Transform dogTransform;
        [SerializeField] private Transform returnPlatform;
        [SerializeField] private float minimumThrowSpeed = 1.5f;
        [SerializeField] private float platformReachDistance = 1.25f;
        [SerializeField] private int trainingGainPerSuccess = 1;
        [SerializeField] private UnityEvent onBallPickedUp = new UnityEvent();
        [SerializeField] private UnityEvent onBallThrown = new UnityEvent();
        [SerializeField] private UnityEvent onDogReturnedToPlatform = new UnityEvent();
        [SerializeField] private UnityEvent onTrainingCompleted = new UnityEvent();

        private bool ballPickedUp;
        private bool ballThrown;

        public bool IsTrainingInProgress => ballPickedUp || ballThrown;

        public void NotifyBallPickedUp()
        {
            ballPickedUp = true;
            ballThrown = false;
            onBallPickedUp.Invoke();
        }

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
        }

        public void NotifyDogReturnedToPlatform()
        {
            if (!ballThrown)
            {
                return;
            }

            if (!IsDogCloseEnoughToPlatform())
            {
                return;
            }

            onDogReturnedToPlatform.Invoke();

            if (dogStatus != null)
            {
                dogStatus.AddTraining(trainingGainPerSuccess);
            }

            onTrainingCompleted.Invoke();
            ResetTrainingState();
        }

        public void CancelCurrentTraining()
        {
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
