using UnityEngine;
using UnityEngine.Events;

namespace FetchVR.Dog
{
    public class DogFeedAction : MonoBehaviour
    {
        [SerializeField] private DogStatusController dogStatus;
        [SerializeField] private DogAudioController dogAudioController;
        [SerializeField] private int satietyGainPerFeed = 1;
        [SerializeField] private UnityEvent onFeedPerformed = new UnityEvent();

        private void Awake()
        {
            ResolveReferences();
        }

        public void FeedDog()
        {
            ResolveReferences();

            if (dogStatus == null)
            {
                Debug.LogWarning($"{nameof(DogFeedAction)} is missing a {nameof(DogStatusController)} reference.", this);
                return;
            }

            dogStatus.AddSatiety(satietyGainPerFeed);
            dogAudioController?.PlayFeedSound();
            onFeedPerformed.Invoke();
        }

        private void ResolveReferences()
        {
            if (dogAudioController == null)
            {
                dogAudioController = GetComponent<DogAudioController>();
            }

            if (dogStatus == null)
            {
                dogStatus = FindFirstObjectByType<DogStatusController>();
            }

            if (dogAudioController == null && dogStatus != null)
            {
                dogAudioController = dogStatus.GetComponent<DogAudioController>();
            }

            if (dogAudioController == null)
            {
                dogAudioController = FindFirstObjectByType<DogAudioController>(FindObjectsInactive.Include);
            }
        }
    }
}
