using UnityEngine;
using UnityEngine.Events;

namespace FetchVR.Dog
{
    public class DogPetInteraction : MonoBehaviour
    {
        [SerializeField] private DogStatusController dogStatus;
        [SerializeField] private Collider dogPettableArea;
        [SerializeField] private string validInteractorTag = "PlayerHand";
        [SerializeField] private bool requireMatchingTag = true;
        [SerializeField] private float petCooldownSeconds = 1f;
        [SerializeField] private int moodGainPerPet = 1;
        [Header("Pet Animation")]
        [SerializeField] private Animator dogAnimator;
        [SerializeField] private AnimationClip petAnimationClip;
        [SerializeField] private string petAnimationTriggerName = "Pet";
        [SerializeField] private string petAnimationStateName;
        [SerializeField] private UnityEvent onPetSucceeded = new UnityEvent();

        private float lastPetTime = float.NegativeInfinity;
        private int interactorCountInRange;

        private void Reset()
        {
            dogPettableArea = GetComponent<Collider>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!enabled)
            {
                return;
            }

            if (dogPettableArea != null && other == dogPettableArea)
            {
                return;
            }

            if (IsValidInteractor(other))
            {
                interactorCountInRange++;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (dogPettableArea != null && other == dogPettableArea)
            {
                return;
            }

            if (IsValidInteractor(other))
            {
                interactorCountInRange = Mathf.Max(0, interactorCountInRange - 1);
            }
        }

        public void TryPetFromTriggerPress()
        {
            if (dogStatus == null || !CanPetNow() || interactorCountInRange <= 0)
            {
                return;
            }

            lastPetTime = Time.time;
            dogStatus.AddMood(moodGainPerPet);
            PlayPetAnimation();
            onPetSucceeded.Invoke();
        }

        public void PetFromEvent()
        {
            TryPetFromTriggerPress();
        }

        private bool CanPetNow()
        {
            return Time.time >= lastPetTime + petCooldownSeconds;
        }

        private bool IsValidInteractor(Collider interactor)
        {
            if (interactor == null)
            {
                return false;
            }

            return !requireMatchingTag || interactor.CompareTag(validInteractorTag);
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
