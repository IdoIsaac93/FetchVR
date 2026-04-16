using UnityEngine;
using UnityEngine.Events;

namespace FetchVR.Dog
{
    public class DogPetInteraction : MonoBehaviour
    {
        [SerializeField] private DogStatusController dogStatus;
        [SerializeField] private DogAudioController dogAudioController;
        [SerializeField] private float petCooldownSeconds = 1f;
        [SerializeField] private int moodGainPerPet = 1;
        [Header("Pet Animation")]
        [SerializeField] private Animator dogAnimator;
        [SerializeField] private AnimationClip petAnimationClip;
        [SerializeField] private string petAnimationTriggerName = "Pet";
        [SerializeField] private string petAnimationStateName;
        [SerializeField] private UnityEvent onPetSucceeded = new UnityEvent();

        private float lastPetTime = float.NegativeInfinity;

        private void Awake()
        {
            if (dogAudioController == null)
            {
                dogAudioController = GetComponent<DogAudioController>();
            }
        }

        public void TryPetFromTriggerPress()
        {
            PerformPet();
        }

        public void PetFromEvent()
        {
            PerformPet();
        }

        private bool CanPetNow()
        {
            return Time.time >= lastPetTime + petCooldownSeconds;
        }

        private void PerformPet()
        {
            if (dogStatus == null || !CanPetNow())
            {
                return;
            }

            lastPetTime = Time.time;
            dogStatus.AddMood(moodGainPerPet);
            PlayPetAnimation();
            dogAudioController?.PlayPetSound();
            onPetSucceeded.Invoke();
        }

        private void PlayPetAnimation()
        {
            if (dogAnimator == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(petAnimationTriggerName))
            {
                dogAnimator.SetTrigger(petAnimationTriggerName);
            }

            if (!string.IsNullOrWhiteSpace(petAnimationStateName))
            {
                dogAnimator.Play(petAnimationStateName, 0, 0f);
                return;
            }

            if (petAnimationClip != null)
            {
                dogAnimator.Play(petAnimationClip.name, 0, 0f);
            }
        }
    }
}
