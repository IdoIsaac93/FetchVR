using System;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class FetchAgent : Agent
{
    [Header("References")]
    public GameObject area;
    public GameObject areaBall;
    public bool useVectorObs;

    [Header("Gameplay Settings")]
    [Tooltip("Minimum distance agent must travel from goal before 'empty return' penalty kicks in")]
    public float minFetchDistance = 3f;

    FetchArea m_MyArea;
    Rigidbody m_AgentRb;
    FetchBall m_MyBall;
    GameObject m_Goal;

    // Phase state
    public enum FetchPhase { SearchingBall, ReturningGoal }
    [HideInInspector] public FetchPhase currentPhase;
    [HideInInspector] public bool hasBall;

    // Track whether agent has ventured far enough from goal
    bool m_HasLeftGoalArea;

    // Distance tracking for shaping reward
    float m_PrevDistToBall;
    float m_PrevDistToGoal;

    // Stuck detection
    Vector3 m_PrevPosition;
    float m_StuckTimer;
    const float k_StuckThreshold = 0.1f;
    const float k_StuckTimeLimit = 2.0f;

    // For distance normalization
    float m_MaxDistance = 30f;

    public override void Initialize()
    {
        m_AgentRb = GetComponent<Rigidbody>();
        m_MyArea = area.GetComponent<FetchArea>();
        m_MyBall = areaBall.GetComponent<FetchBall>();
        m_Goal = m_MyArea.goal;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (useVectorObs)
        {
            // 1. Phase state (1 obs)
            sensor.AddObservation(hasBall);

            // 2. Agent local velocity (3 obs)
            sensor.AddObservation(transform.InverseTransformDirection(m_AgentRb.linearVelocity));

            // 3. Ball relative direction + distance (4 obs)
            Vector3 toBall = areaBall.transform.position - transform.position;
            sensor.AddObservation(transform.InverseTransformDirection(toBall.normalized));
            sensor.AddObservation(toBall.magnitude / m_MaxDistance);

            // 4. Goal (player) relative direction + distance (4 obs)
            Vector3 toGoal = m_Goal.transform.position - transform.position;
            sensor.AddObservation(transform.InverseTransformDirection(toGoal.normalized));
            sensor.AddObservation(toGoal.magnitude / m_MaxDistance);

            // Total: 1 + 3 + 4 + 4 = 12
        }
    }

    public void MoveAgent(ActionSegment<int> act)
    {
        var dirToGo = Vector3.zero;
        var rotateDir = Vector3.zero;

        var action = act[0];
        switch (action)
        {
            case 1:
                dirToGo = transform.forward * 1f;
                break;
            case 2:
                dirToGo = transform.forward * -1f;
                break;
            case 3:
                rotateDir = transform.up * 1f;
                break;
            case 4:
                rotateDir = transform.up * -1f;
                break;
        }
        transform.Rotate(rotateDir, Time.deltaTime * 200f);
        m_AgentRb.AddForce(dirToGo * 2f, ForceMode.VelocityChange);
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        // Step penalty
        AddReward(-1f / MaxStep);

        MoveAgent(actionBuffers.DiscreteActions);

        float distToGoal = Vector3.Distance(transform.position, m_Goal.transform.position);

        // Check if agent has traveled far enough from goal
        if (!m_HasLeftGoalArea && distToGoal > minFetchDistance)
        {
            m_HasLeftGoalArea = true;
        }

        // Shaping reward based on current phase
        if (currentPhase == FetchPhase.SearchingBall)
        {
            float distToBall = Vector3.Distance(transform.position, areaBall.transform.position);
            float delta = m_PrevDistToBall - distToBall;
            AddReward(delta * 0.02f);
            m_PrevDistToBall = distToBall;
        }
        else if (currentPhase == FetchPhase.ReturningGoal)
        {
            float delta = m_PrevDistToGoal - distToGoal;
            AddReward(delta * 0.02f);
            m_PrevDistToGoal = distToGoal;
        }

        // Stuck detection
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
        // Reset phase state
        hasBall = false;
        currentPhase = FetchPhase.SearchingBall;
        m_HasLeftGoalArea = false;
        m_StuckTimer = 0f;

        // 1. Place goal (player) at fixed position (center or designated slot)
        m_MyArea.PlaceGoalFixed();

        // 2. Place agent near goal (dog starts near player)
        m_AgentRb.linearVelocity = Vector3.zero;
        m_AgentRb.angularVelocity = Vector3.zero;
        m_MyArea.PlaceAgentNearGoal(gameObject);
        transform.rotation = Quaternion.Euler(0f, Random.Range(0, 360), 0f);

        // 3. Place ball at a random spawn zone far from goal
        int ballSlot = PickBallSlot();
        m_MyBall.ResetBall(ballSlot);

        // Initialize distance tracking
        m_PrevDistToBall = Vector3.Distance(transform.position, areaBall.transform.position);
        m_PrevDistToGoal = Vector3.Distance(transform.position, m_Goal.transform.position);
        m_PrevPosition = transform.position;
    }

    /// <summary>
    /// Pick a spawn slot for the ball that is far from the goal.
    /// Picks the farthest slot from goal out of a few random candidates.
    /// </summary>
    int PickBallSlot()
    {
        int bestSlot = 0;
        float bestDist = 0f;
        Vector3 goalPos = m_Goal.transform.position;

        // Try a few random candidates and pick the farthest from goal
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

    /// <summary>
    /// Called by FetchBall when the agent picks up the ball.
    /// </summary>
    public void OnBallCollected()
    {
        hasBall = true;
        currentPhase = FetchPhase.ReturningGoal;
        AddReward(1.0f);

        m_PrevDistToGoal = Vector3.Distance(transform.position, m_Goal.transform.position);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("goal"))
        {
            if (hasBall)
            {
                // Success: fetched ball and returned to player
                SetReward(2f);
                EndEpisode();
            }
            else if (m_HasLeftGoalArea)
            {
                // Came back to player without the ball (only penalize if dog actually left)
                AddReward(-0.1f);
            }
            // If dog hasn't left goal area yet, no penalty — it's just starting
        }
    }
}
