using UnityEngine;

namespace FetchVR.Dog
{
    public class DogAIModelSwitcher : MonoBehaviour
    {
        [SerializeField] private DogStatusController dogStatus;
        [SerializeField] private Behaviour dumbAiBehaviour;
        [SerializeField] private Behaviour smartAiBehaviour;
        [SerializeField] private GameObject dumbAiVisualRoot;
        [SerializeField] private GameObject smartAiVisualRoot;

        private void OnEnable()
        {
            if (dogStatus != null)
            {
                dogStatus.LevelChanged += HandleLevelChanged;
            }

            RefreshModelState();
        }

        private void OnDisable()
        {
            if (dogStatus != null)
            {
                dogStatus.LevelChanged -= HandleLevelChanged;
            }
        }

        public void RefreshModelState()
        {
            if (dogStatus == null)
            {
                return;
            }

            bool useSmartAi = dogStatus.IsSmartAiUnlocked;
            SetModelState(useSmartAi);
        }

        private void HandleLevelChanged(int _)
        {
            RefreshModelState();
        }

        private void SetModelState(bool useSmartAi)
        {
            if (dumbAiBehaviour != null)
            {
                dumbAiBehaviour.enabled = !useSmartAi;
            }

            if (smartAiBehaviour != null)
            {
                smartAiBehaviour.enabled = useSmartAi;
            }

            if (dumbAiVisualRoot != null)
            {
                dumbAiVisualRoot.SetActive(!useSmartAi);
            }

            if (smartAiVisualRoot != null)
            {
                smartAiVisualRoot.SetActive(useSmartAi);
            }
        }
    }
}
