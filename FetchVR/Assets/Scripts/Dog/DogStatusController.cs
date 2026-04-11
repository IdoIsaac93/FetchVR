using System;
using UnityEngine;
using UnityEngine.Events;

namespace FetchVR.Dog
{
    public enum DogNeedType
    {
        Satiety,
        Mood,
        Training
    }

    [Serializable]
    public class IntEvent : UnityEvent<int>
    {
    }

    [Serializable]
    public class FloatEvent : UnityEvent<float>
    {
    }

    [Serializable]
    public class DogNeedChangedEvent : UnityEvent<DogNeedType, int, int>
    {
    }

    public class DogStatusController : MonoBehaviour
    {
        [Header("Level")]
        [SerializeField] private int currentLevel = 1;
        [SerializeField] private int smartAiUnlockLevel = 3;

        [Header("Need Max Values")]
        [SerializeField] private int maxSatiety = 3;
        [SerializeField] private int maxMood = 3;
        [SerializeField] private int maxTraining = 3;

        [Header("Current Values")]
        [SerializeField] private int currentSatiety;
        [SerializeField] private int currentMood;
        [SerializeField] private int currentTraining;

        [Header("Events")]
        [SerializeField] private IntEvent onLevelChanged = new IntEvent();
        [SerializeField] private UnityEvent onDogLeveledUp = new UnityEvent();
        [SerializeField] private UnityEvent onNeedsResetAfterLevelUp = new UnityEvent();
        [SerializeField] private UnityEvent onSmartAiUnlocked = new UnityEvent();
        [SerializeField] private DogNeedChangedEvent onNeedChanged = new DogNeedChangedEvent();
        [SerializeField] private FloatEvent onSatietyPercentChanged = new FloatEvent();
        [SerializeField] private FloatEvent onMoodPercentChanged = new FloatEvent();
        [SerializeField] private FloatEvent onTrainingPercentChanged = new FloatEvent();

        private bool smartAiUnlockedInvoked;

        public event Action<int> LevelChanged;
        public event Action DogLeveledUp;
        public event Action NeedsResetAfterLevelUp;
        public event Action SmartAiUnlocked;
        public event Action<DogNeedType, int, int> NeedChanged;

        public int CurrentLevel => currentLevel;
        public int SmartAiUnlockLevel => smartAiUnlockLevel;
        public int CurrentSatiety => currentSatiety;
        public int CurrentMood => currentMood;
        public int CurrentTraining => currentTraining;
        public int MaxSatiety => maxSatiety;
        public int MaxMood => maxMood;
        public int MaxTraining => maxTraining;
        public bool IsSmartAiUnlocked => currentLevel >= smartAiUnlockLevel;

        private void Awake()
        {
            currentLevel = Mathf.Max(1, currentLevel);
            maxSatiety = Mathf.Max(1, maxSatiety);
            maxMood = Mathf.Max(1, maxMood);
            maxTraining = Mathf.Max(1, maxTraining);

            currentSatiety = Mathf.Clamp(currentSatiety, 0, maxSatiety);
            currentMood = Mathf.Clamp(currentMood, 0, maxMood);
            currentTraining = Mathf.Clamp(currentTraining, 0, maxTraining);

            if (IsSmartAiUnlocked)
            {
                smartAiUnlockedInvoked = true;
            }

            BroadcastAllState();
        }

        public void AddSatiety(int amount = 1)
        {
            ApplyNeedGain(DogNeedType.Satiety, amount);
        }

        public void AddMood(int amount = 1)
        {
            ApplyNeedGain(DogNeedType.Mood, amount);
        }

        public void AddTraining(int amount = 1)
        {
            ApplyNeedGain(DogNeedType.Training, amount);
        }

        public void ResetAllNeeds()
        {
            currentSatiety = 0;
            currentMood = 0;
            currentTraining = 0;

            BroadcastNeed(DogNeedType.Satiety, currentSatiety, maxSatiety);
            BroadcastNeed(DogNeedType.Mood, currentMood, maxMood);
            BroadcastNeed(DogNeedType.Training, currentTraining, maxTraining);
        }

        [ContextMenu("Force Level Up")]
        public void ForceLevelUp()
        {
            LevelUp();
        }

        private void ApplyNeedGain(DogNeedType needType, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            switch (needType)
            {
                case DogNeedType.Satiety:
                    currentSatiety = Mathf.Clamp(currentSatiety + amount, 0, maxSatiety);
                    BroadcastNeed(needType, currentSatiety, maxSatiety);
                    break;
                case DogNeedType.Mood:
                    currentMood = Mathf.Clamp(currentMood + amount, 0, maxMood);
                    BroadcastNeed(needType, currentMood, maxMood);
                    break;
                case DogNeedType.Training:
                    currentTraining = Mathf.Clamp(currentTraining + amount, 0, maxTraining);
                    BroadcastNeed(needType, currentTraining, maxTraining);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(needType), needType, null);
            }

            TryLevelUp();
        }

        private void TryLevelUp()
        {
            if (currentSatiety < maxSatiety || currentMood < maxMood || currentTraining < maxTraining)
            {
                return;
            }

            LevelUp();
        }

        private void LevelUp()
        {
            currentLevel++;
            onLevelChanged.Invoke(currentLevel);
            LevelChanged?.Invoke(currentLevel);

            onDogLeveledUp.Invoke();
            DogLeveledUp?.Invoke();

            ResetAllNeeds();
            onNeedsResetAfterLevelUp.Invoke();
            NeedsResetAfterLevelUp?.Invoke();

            if (!smartAiUnlockedInvoked && IsSmartAiUnlocked)
            {
                smartAiUnlockedInvoked = true;
                onSmartAiUnlocked.Invoke();
                SmartAiUnlocked?.Invoke();
            }
        }

        private void BroadcastAllState()
        {
            onLevelChanged.Invoke(currentLevel);
            LevelChanged?.Invoke(currentLevel);

            BroadcastNeed(DogNeedType.Satiety, currentSatiety, maxSatiety);
            BroadcastNeed(DogNeedType.Mood, currentMood, maxMood);
            BroadcastNeed(DogNeedType.Training, currentTraining, maxTraining);
        }

        private void BroadcastNeed(DogNeedType needType, int currentValue, int maxValue)
        {
            onNeedChanged.Invoke(needType, currentValue, maxValue);
            NeedChanged?.Invoke(needType, currentValue, maxValue);

            float percent = maxValue <= 0 ? 0f : (float)currentValue / maxValue;

            switch (needType)
            {
                case DogNeedType.Satiety:
                    onSatietyPercentChanged.Invoke(percent);
                    break;
                case DogNeedType.Mood:
                    onMoodPercentChanged.Invoke(percent);
                    break;
                case DogNeedType.Training:
                    onTrainingPercentChanged.Invoke(percent);
                    break;
            }
        }
    }
}
