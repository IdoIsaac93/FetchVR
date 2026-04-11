using UnityEngine;
using UnityEngine.Events;

namespace FetchVR.Dog
{
    public class DogFeedAction : MonoBehaviour
    {
        [SerializeField] private DogStatusController dogStatus;
        [SerializeField] private int satietyGainPerFeed = 1;
        [SerializeField] private UnityEvent onFeedPerformed = new UnityEvent();

        public void FeedDog()
        {
            if (dogStatus == null)
            {
                Debug.LogWarning($"{nameof(DogFeedAction)} is missing a {nameof(DogStatusController)} reference.", this);
                return;
            }

            dogStatus.AddSatiety(satietyGainPerFeed);
            onFeedPerformed.Invoke();
        }
    }
}
