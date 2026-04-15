using UnityEngine;

namespace FetchVR.Dog
{
    public class DogTrainingPlatformTrigger : MonoBehaviour
    {
        [SerializeField] private DogTrainingSession trainingSession;
        [SerializeField] private Collider expectedDogCollider;
        [SerializeField] private string validDogTag = "Dog";
        [SerializeField] private bool requireMatchingTag = false;

        private void OnTriggerEnter(Collider other)
        {
            if (trainingSession == null || other == null)
            {
                return;
            }

            if (expectedDogCollider != null && other != expectedDogCollider)
            {
                return;
            }

            if (requireMatchingTag && !other.CompareTag(validDogTag))
            {
                return;
            }

            trainingSession.NotifyDogReturnedToPlatform();
        }
    }
}
