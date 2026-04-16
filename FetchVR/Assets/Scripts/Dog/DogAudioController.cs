using UnityEngine;

namespace FetchVR.Dog
{
    public class DogAudioController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator dogAnimator;
        [SerializeField] private AudioSource loopAudioSource;
        [SerializeField] private AudioSource oneShotAudioSource;

        [Header("Loop Clips")]
        [SerializeField] private AudioClip idleBreathingClip;
        [SerializeField] private float idleBreathingVolume = 0.35f;

        [Header("One Shot Clips")]
        [SerializeField] private AudioClip[] fetchBarkClips;
        [SerializeField] private AudioClip[] petClips;
        [SerializeField] private AudioClip[] feedClips;
        [SerializeField] private float oneShotVolume = 1f;

        [Header("Animation State Names")]
        [SerializeField] private string runBoolName = "Run";

        [Header("Timing")]
        [SerializeField] private float oneShotBreathingResumeDelay = 0.25f;

        private float m_BreathingSuppressedUntil;

        private void Awake()
        {
            if (dogAnimator == null)
            {
                dogAnimator = GetComponent<Animator>();
            }

            if (loopAudioSource == null)
            {
                loopAudioSource = gameObject.AddComponent<AudioSource>();
                loopAudioSource.playOnAwake = false;
                loopAudioSource.loop = true;
                loopAudioSource.spatialBlend = 1f;
            }

            if (oneShotAudioSource == null)
            {
                oneShotAudioSource = gameObject.AddComponent<AudioSource>();
                oneShotAudioSource.playOnAwake = false;
                oneShotAudioSource.loop = false;
                oneShotAudioSource.spatialBlend = 1f;
            }
        }

        private void Update()
        {
            UpdateIdleBreathing();
        }

        public void PlayFetchBark()
        {
            PlayRandomOneShot(fetchBarkClips);
        }

        public void PlayPetSound()
        {
            PlayRandomOneShot(petClips);
        }

        public void PlayFeedSound()
        {
            PlayRandomOneShot(feedClips);
        }

        public void SuppressIdleBreathing(float duration)
        {
            m_BreathingSuppressedUntil = Mathf.Max(m_BreathingSuppressedUntil, Time.time + Mathf.Max(0f, duration));
            StopIdleBreathing();
        }

        private void UpdateIdleBreathing()
        {
            if (idleBreathingClip == null || loopAudioSource == null)
            {
                return;
            }

            bool shouldPlayBreathing = Time.time >= m_BreathingSuppressedUntil && !IsDogRunning();
            if (!shouldPlayBreathing)
            {
                StopIdleBreathing();
                return;
            }

            if (loopAudioSource.clip != idleBreathingClip)
            {
                loopAudioSource.clip = idleBreathingClip;
                loopAudioSource.volume = idleBreathingVolume;
                loopAudioSource.loop = true;
            }

            if (!loopAudioSource.isPlaying)
            {
                loopAudioSource.Play();
            }
        }

        private void StopIdleBreathing()
        {
            if (loopAudioSource != null && loopAudioSource.isPlaying)
            {
                loopAudioSource.Stop();
            }
        }

        private bool IsDogRunning()
        {
            return dogAnimator != null
                   && !string.IsNullOrWhiteSpace(runBoolName)
                   && dogAnimator.GetBool(runBoolName);
        }

        private void PlayRandomOneShot(AudioClip[] clips)
        {
            if (oneShotAudioSource == null)
            {
                return;
            }

            AudioClip clip = GetRandomClip(clips);
            if (clip == null)
            {
                return;
            }

            float resumeDelay = clip.length + oneShotBreathingResumeDelay;
            SuppressIdleBreathing(resumeDelay);
            oneShotAudioSource.PlayOneShot(clip, oneShotVolume);
        }

        private static AudioClip GetRandomClip(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0)
            {
                return null;
            }

            int clipCount = 0;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null)
                {
                    clipCount++;
                }
            }

            if (clipCount == 0)
            {
                return null;
            }

            int chosenIndex = Random.Range(0, clipCount);
            int seenValidClips = 0;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null)
                {
                    continue;
                }

                if (seenValidClips == chosenIndex)
                {
                    return clips[i];
                }

                seenValidClips++;
            }

            return null;
        }
    }
}
