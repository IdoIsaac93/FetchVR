using System;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using Random = UnityEngine.Random;

public class FetchAgent : Agent
{
    [Header("References")]
    public GameObject area;
    public GameObject areaBall;
    public bool useVectorObs;

    [Header("Gameplay Settings")]
    [Tooltip("Minimum distance agent must travel from goal before 'empty return' penalty kicks in")]
    public float minFetchDistance = 3f;

    [Tooltip("(Game mode) Distance to goal at which the dog counts as 'returned to player'")]
    public float goalReachDistance = 1.5f;

    [Tooltip("(Game mode) Distance to ball at which the dog picks it up")]
    public float ballPickupDistance = 1.0f;

    [Header("Training Rewards")]
    public float approachRewardScale = 0.006f;
    public float enterTargetAreaReward = 0.05f;
    public float wallCollisionPenalty = -0.02f;
    public float ballCollectedReward = 0.25f;
    public float fetchCompleteReward = 4.0f;
    public float failedEpisodePenalty = -0.5f;
    public bool faceBallAtEpisodeStart = true;

    FetchArea m_MyArea;
    Rigidbody m_AgentRb;
    FetchBall m_MyBall;
    GameObject m_Goal;

    public enum FetchPhase { SearchingBall, ReturningGoal }
    public FetchPhase currentPhase;
    public bool hasBall;
    public bool isFetching;

    bool m_HasLeftGoalArea;
    float m_PrevDistToBall;
    float m_PrevDistToGoal;
    Vector3 m_PrevPosition;
    float m_StuckTimer;
    float m_MaxDistance = 30f;

    int m_BallSpawnAreaIndex = -1;
    int m_GoalSpawnAreaIndex = -1;
    bool m_EnteredBallArea;
    bool m_EnteredGoalArea;
    bool m_EpisodeSucceeded;

    const float k_StuckThreshold = 0.1f;
    const float k_StuckTimeLimit = 2.0f;

    bool IsGameMode => m_MyArea != null && m_MyArea.isGameMode;

    public event Action OnFetchSuccess;
    public event Action<bool> OnTrainingEpisodeFinished;

    void EnsureInitialized()
    {
        if (m_AgentRb == null)
        {
            m_AgentRb = GetComponent<Rigidbody>();
        }

        if (m_MyArea == null && area != null)
        {
            m_MyArea = area.GetComponent<FetchArea>();
        }

        if (m_MyBall == null && areaBall != null)
        {
            m_MyBall = areaBall.GetComponent<FetchBall>();
        }

        if (m_Goal == null && m_MyArea != null)
        {
            m_Goal = m_MyArea.goal;
        }
    }

    public override void Initialize()
    {
        EnsureInitialized();

        if (m_MyArea == null)
        {
            Debug.LogError("FetchAgent is missing a valid FetchArea reference.");
            return;
        }

        float diag = m_MyArea.GetMapDiagonal();
        if (diag > 1f)
        {
            m_MaxDistance = diag;
        }

        if (IsGameMode)
        {
            MaxStep = 0;
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        EnsureInitialized();

        if (!useVectorObs)
        {
            return;
        }

        sensor.AddObservation(hasBall);
        sensor.AddObservation(transform.InverseTransformDirection(m_AgentRb.linearVelocity));

        Vector3 toBall = areaBall.transform.position - transform.position;
        sensor.AddObservation(transform.InverseTransformDirection(toBall.normalized));
        sensor.AddObservation(toBall.magnitude / m_MaxDistance);

        Vector3 toGoal = m_Goal.transform.position - transform.position;
        sensor.AddObservation(transform.InverseTransformDirection(toGoal.normalized));
        sensor.AddObservation(toGoal.magnitude / m_MaxDistance);
    }

    public void MoveAgent(ActionSegment<int> act)
    {
        Vector3 dirToGo = Vector3.zero;
        Vector3 rotateDir = Vector3.zero;

        int action = act[0];
        switch (action)
        {
            case 1:
                dirToGo = transform.forward;
                break;
            case 2:
                dirToGo = -transform.forward;
                break;
            case 3:
                rotateDir = transform.up;
                break;
            case 4:
                rotateDir = -transform.up;
                break;
        }

        transform.Rotate(rotateDir, Time.deltaTime * 200f);
        m_AgentRb.AddForce(dirToGo * 2f, ForceMode.VelocityChange);

        if (m_AgentRb.linearVelocity.magnitude > 5f)
        {
            m_AgentRb.linearVelocity = m_AgentRb.linearVelocity.normalized * 5f;
        }
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        if (IsGameMode)
        {
            if (isFetching)
            {
                MoveAgent(actionBuffers.DiscreteActions);

                if (!hasBall)
                {
                    float distToBall = Vector3.Distance(transform.position, areaBall.transform.position);
                    if (distToBall <= ballPickupDistance)
                    {
                        m_MyBall.AttachToAgent(transform);
                        OnBallCollected();
                    }
                }
                else
                {
                    float distToPlayer = Vector3.Distance(transform.position, m_Goal.transform.position);
                    if (distToPlayer <= goalReachDistance)
                    {
                        CompleteGameFetch();
                    }
                }
            }
            return;
        }

        AddReward(-1f / MaxStep);
        MoveAgent(actionBuffers.DiscreteActions);

        float distToGoal = Vector3.Distance(transform.position, m_Goal.transform.position);
        if (!m_HasLeftGoalArea && distToGoal > minFetchDistance)
        {
            m_HasLeftGoalArea = true;
        }

        if (currentPhase == FetchPhase.SearchingBall)
        {
            float distToBall = Vector3.Distance(transform.position, areaBall.transform.position);

            if (!hasBall && distToBall <= ballPickupDistance)
            {
                m_MyBall.AttachToAgent(transform);
                OnBallCollected();
                distToBall = 0f;
            }

            float delta = m_PrevDistToBall - distToBall;
            if (delta > 0f)
            {
                AddReward(delta * approachRewardScale);
            }
            m_PrevDistToBall = distToBall;

            int currentArea = m_MyArea.GetNearestSpawnAreaIndex(transform.position);
            if (!m_EnteredBallArea && currentArea >= 0 && currentArea == m_BallSpawnAreaIndex)
            {
                AddReward(enterTargetAreaReward);
                m_EnteredBallArea = true;
            }
        }
        else if (currentPhase == FetchPhase.ReturningGoal)
        {
            float delta = m_PrevDistToGoal - distToGoal;
            if (delta > 0f)
            {
                AddReward(delta * approachRewardScale);
            }
            m_PrevDistToGoal = distToGoal;

            int currentArea = m_MyArea.GetNearestSpawnAreaIndex(transform.position);
            if (!m_EnteredGoalArea && currentArea >= 0 && currentArea == m_GoalSpawnAreaIndex)
            {
                AddReward(enterTargetAreaReward);
                m_EnteredGoalArea = true;
            }

            if (hasBall && distToGoal <= goalReachDistance)
            {
                m_EpisodeSucceeded = true;
                OnTrainingEpisodeFinished?.Invoke(true);
                SetReward(fetchCompleteReward);
                EndEpisode();
                return;
            }
        }

        float moved = Vector3.Distance(transform.position, m_PrevPosition);
        if (moved < k_StuckThreshold * Time.fixedDeltaTime)
        {
            m_StuckTimer += Time.fixedDeltaTime;
            if (m_StuckTimer > k_StuckTimeLimit)
            {
                AddReward(-0.01f);
                m_StuckTimer = 0f;
            }
        }
        else
        {
            m_StuckTimer = 0f;
        }

        m_PrevPosition = transform.position;
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        if (Input.GetKey(KeyCode.D))
        {
            discreteActionsOut[0] = 3;
        }
        else if (Input.GetKey(KeyCode.W))
        {
            discreteActionsOut[0] = 1;
        }
        else if (Input.GetKey(KeyCode.A))
        {
            discreteActionsOut[0] = 4;
        }
        else if (Input.GetKey(KeyCode.S))
        {
            discreteActionsOut[0] = 2;
        }
    }

    public override void OnEpisodeBegin()
    {
        if (!IsGameMode && StepCount > 0 && !m_EpisodeSucceeded)
        {
            OnTrainingEpisodeFinished?.Invoke(false);
            AddReward(failedEpisodePenalty);
        }

        hasBall = false;
        currentPhase = FetchPhase.SearchingBall;
        m_HasLeftGoalArea = false;
        m_StuckTimer = 0f;
        m_EnteredBallArea = false;
        m_EnteredGoalArea = false;
        m_EpisodeSucceeded = false;

        if (IsGameMode)
        {
            m_PrevDistToBall = Vector3.Distance(transform.position, areaBall.transform.position);
            m_PrevDistToGoal = Vector3.Distance(transform.position, m_Goal.transform.position);
            m_PrevPosition = transform.position;
            return;
        }

        m_AgentRb.linearVelocity = Vector3.zero;
        m_AgentRb.angularVelocity = Vector3.zero;

        if (m_MyArea.randomizePositions)
        {
            m_Goal.transform.position = m_MyArea.GetRandomPositionFromAllowedAreas(
                m_MyArea.goalSpawnY,
                m_MyArea.goalSpawnClearance,
                m_MyArea.allowedGoalSpawnAreaIndices);
            m_Goal.SetActive(true);
            m_GoalSpawnAreaIndex = m_MyArea.GetNearestSpawnAreaIndex(m_Goal.transform.position);

            Vector3 ballPos = m_MyArea.GetRandomPositionAwayFromAllowedAreas(
                m_MyArea.ballSpawnY,
                m_MyArea.ballSpawnClearance,
                m_Goal.transform.position,
                m_MyArea.minBallGoalDistance,
                m_MyArea.allowedBallSpawnAreaIndices);
            areaBall.transform.SetParent(m_MyArea.transform);
            areaBall.transform.position = ballPos;
            m_MyBall.PrepareForThrow();
            m_BallSpawnAreaIndex = m_MyArea.GetNearestSpawnAreaIndex(areaBall.transform.position);

            Vector3 agentPos = m_MyArea.GetRandomPositionAwayFromAllowedAreas(
                m_MyArea.agentSpawnY,
                m_MyArea.agentSpawnClearance,
                areaBall.transform.position,
                m_MyArea.minAgentBallDistance,
                m_MyArea.allowedAgentSpawnAreaIndices);
            transform.position = agentPos;
        }
        else
        {
            m_MyArea.PlaceGoalFixed();
            m_MyArea.PlaceAgentNearGoal(gameObject);
            int ballSlot = PickBallSlot();
            m_MyBall.ResetBall(ballSlot);
            m_GoalSpawnAreaIndex = m_MyArea.GetNearestSpawnAreaIndex(m_Goal.transform.position);
            m_BallSpawnAreaIndex = ballSlot;
        }

        if (faceBallAtEpisodeStart)
        {
            Vector3 flatToBall = areaBall.transform.position - transform.position;
            flatToBall.y = 0f;
            if (flatToBall.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(flatToBall.normalized, Vector3.up);
            }
            else
            {
                transform.rotation = Quaternion.Euler(0f, Random.Range(0, 360f), 0f);
            }
        }
        else
        {
            transform.rotation = Quaternion.Euler(0f, Random.Range(0, 360f), 0f);
        }
        m_PrevDistToBall = Vector3.Distance(transform.position, areaBall.transform.position);
        m_PrevDistToGoal = Vector3.Distance(transform.position, m_Goal.transform.position);
        m_PrevPosition = transform.position;
    }

    public void StartFetch()
    {
        if (!IsGameMode)
        {
            Debug.LogWarning("FetchAgent.StartFetch() should only be called in game mode.");
            return;
        }

        isFetching = true;
        EndEpisode();
    }

    public void CancelFetch()
    {
        if (!IsGameMode)
        {
            return;
        }

        isFetching = false;
        hasBall = false;
        currentPhase = FetchPhase.SearchingBall;
        m_AgentRb.linearVelocity = Vector3.zero;
        m_AgentRb.angularVelocity = Vector3.zero;
    }

    public void ResetForEvaluation(int goalSpawnAreaIndex, int ballSpawnAreaIndex, int agentSpawnAreaIndex, float agentYaw)
    {
        EnsureInitialized();

        if (IsGameMode)
        {
            Debug.LogWarning("ResetForEvaluation is intended for training mode setups.");
        }

        hasBall = false;
        isFetching = false;
        currentPhase = FetchPhase.SearchingBall;
        m_HasLeftGoalArea = false;
        m_StuckTimer = 0f;
        m_EnteredBallArea = false;
        m_EnteredGoalArea = false;
        m_EpisodeSucceeded = false;

        m_AgentRb.linearVelocity = Vector3.zero;
        m_AgentRb.angularVelocity = Vector3.zero;

        m_MyArea.PlaceObject(m_Goal, goalSpawnAreaIndex, m_MyArea.goalSpawnY);
        m_Goal.SetActive(true);
        m_GoalSpawnAreaIndex = goalSpawnAreaIndex;

        areaBall.transform.SetParent(m_MyArea.transform);
        m_MyBall.ResetBall(ballSpawnAreaIndex);
        m_BallSpawnAreaIndex = ballSpawnAreaIndex;

        m_MyArea.PlaceObject(gameObject, agentSpawnAreaIndex, m_MyArea.agentSpawnY);
        transform.rotation = Quaternion.Euler(0f, agentYaw, 0f);

        m_PrevDistToBall = Vector3.Distance(transform.position, areaBall.transform.position);
        m_PrevDistToGoal = Vector3.Distance(transform.position, m_Goal.transform.position);
        m_PrevPosition = transform.position;

        RequestDecision();
    }

    int PickBallSlot()
    {
        int bestSlot = 0;
        float bestDist = 0f;
        Vector3 goalPos = m_Goal.transform.position;

        for (int i = 0; i < m_MyArea.spawnAreas.Length; i++)
        {
            float dist = Vector3.Distance(m_MyArea.spawnAreas[i].transform.position, goalPos);
            if (dist > bestDist)
            {
                bestDist = dist;
                bestSlot = i;
            }
        }

        return bestSlot;
    }

    public void OnBallCollected()
    {
        hasBall = true;
        currentPhase = FetchPhase.ReturningGoal;
        m_EnteredGoalArea = false;

        if (!IsGameMode)
        {
            AddReward(ballCollectedReward);
        }

        m_PrevDistToGoal = Vector3.Distance(transform.position, m_Goal.transform.position);
    }

    void CompleteGameFetch()
    {
        isFetching = false;
        m_MyArea.NotifyFetchRoundComplete();
        OnFetchSuccess?.Invoke();

        m_MyBall.DropBall();
        hasBall = false;
        currentPhase = FetchPhase.SearchingBall;
        m_AgentRb.linearVelocity = Vector3.zero;
        m_AgentRb.angularVelocity = Vector3.zero;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!IsGameMode && collision.gameObject.CompareTag("wall"))
        {
            AddReward(wallCollisionPenalty);
        }

        if (!collision.gameObject.CompareTag("goal"))
        {
            return;
        }

        if (hasBall)
        {
            if (IsGameMode)
            {
                CompleteGameFetch();
            }
            else
            {
                m_EpisodeSucceeded = true;
                OnTrainingEpisodeFinished?.Invoke(true);
                SetReward(fetchCompleteReward);
                EndEpisode();
            }
        }
        else if (!IsGameMode && m_HasLeftGoalArea)
        {
            AddReward(-0.1f);
        }
    }
}
