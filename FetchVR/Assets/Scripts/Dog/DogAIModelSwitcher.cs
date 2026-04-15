using UnityEngine;
using Unity.InferenceEngine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;

namespace FetchVR.Dog
{
    public class DogAIModelSwitcher : MonoBehaviour
    {
        private enum DogModelTier
        {
            None,
            Level1Novice,
            Level2Intermediate,
            Level3Smart
        }

        [SerializeField] private DogStatusController dogStatus;
        [SerializeField] private Agent agent;
        [SerializeField] private BehaviorParameters behaviorParameters;
        [Header("Level Models")]
        [SerializeField] private ModelAsset level1Model;
        [SerializeField] private ModelAsset level2Model;
        [SerializeField] private ModelAsset level3Model;
        [Header("Debug")]
        [SerializeField] private DogModelTier currentModelTier;
        [SerializeField] private string currentModelName;

        private ModelAsset appliedModel;

        private void OnEnable()
        {
            if (dogStatus == null)
            {
                dogStatus = GetComponent<DogStatusController>();
            }

            if (agent == null)
            {
                agent = GetComponent<Agent>();
            }

            if (behaviorParameters == null)
            {
                behaviorParameters = GetComponent<BehaviorParameters>();
            }

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
            if (dogStatus == null || agent == null || behaviorParameters == null)
            {
                currentModelTier = DogModelTier.None;
                currentModelName = string.Empty;
                return;
            }

            var targetModel = GetModelForLevel(dogStatus.CurrentLevel);
            SetModelState(targetModel, GetTierForLevel(dogStatus.CurrentLevel));
        }

        private void HandleLevelChanged(int _)
        {
            RefreshModelState();
        }

        private ModelAsset GetModelForLevel(int level)
        {
            if (level <= 1)
            {
                return level1Model;
            }

            if (level == 2)
            {
                return level2Model != null ? level2Model : level1Model;
            }

            return level3Model != null
                ? level3Model
                : (level2Model != null ? level2Model : level1Model);
        }

        private DogModelTier GetTierForLevel(int level)
        {
            if (level <= 1)
            {
                return DogModelTier.Level1Novice;
            }

            if (level == 2)
            {
                return DogModelTier.Level2Intermediate;
            }

            return DogModelTier.Level3Smart;
        }

        private void SetModelState(ModelAsset modelToApply, DogModelTier tierToApply)
        {
            currentModelTier = modelToApply == null ? DogModelTier.None : tierToApply;
            currentModelName = modelToApply != null ? modelToApply.name : string.Empty;

            if (modelToApply == null || modelToApply == appliedModel)
            {
                return;
            }

            appliedModel = modelToApply;
            agent.SetModel(behaviorParameters.BehaviorName, modelToApply, behaviorParameters.InferenceDevice);
        }
    }
}
