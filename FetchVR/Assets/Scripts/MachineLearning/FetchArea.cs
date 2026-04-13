using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class FetchArea : Area
{
    [Header("References")]
    public GameObject goal;
    public GameObject[] spawnAreas;
    public GameObject ground;

    [Header("Mode")]
    [Tooltip("When true, operates in game mode: goal tracks VR player, ball is thrown by player, agent is not teleported.")]
    public bool isGameMode;

    [Header("Spawn Settings")]
    public float agentSpawnY = 0.5f;
    public float ballSpawnY = 0.5f;
    public float goalSpawnY = 0.5f;
    public float agentSpawnClearance = 0.8f;
    public float ballSpawnClearance = 0.35f;
    public float goalSpawnClearance = 0.75f;
    public int spawnValidationAttempts = 40;

    [Header("Training Spawn Mode")]
    [Tooltip("When true, all three (goal/agent/ball) are placed randomly. When spawn areas exist, they are preferred over raw map sampling.")]
    public bool randomizePositions = true;
    public bool preferSpawnAreas = true;

    [Tooltip("Minimum distance between ball and goal when randomizing")]
    public float minBallGoalDistance = 3f;

    [Tooltip("Minimum distance between agent and ball when randomizing")]
    public float minAgentBallDistance = 2f;

    [Header("Curriculum")]
    [Tooltip("Optional subset of spawn area indices for goal placement during training. Empty = all spawn areas.")]
    public int[] allowedGoalSpawnAreaIndices;
    [Tooltip("Optional subset of spawn area indices for ball placement during training. Empty = all spawn areas.")]
    public int[] allowedBallSpawnAreaIndices;
    [Tooltip("Optional subset of spawn area indices for agent placement during training. Empty = all spawn areas.")]
    public int[] allowedAgentSpawnAreaIndices;

    [Header("Legacy Training Settings")]
    [Tooltip("(Legacy mode only) Which spawn area index to place the goal. -1 = use center of area.")]
    public int goalSpawnIndex = -1;

    [Tooltip("Shrink map bounds inward by this margin to prevent edge spawns")]
    public float boundsMargin = 1.0f;

    float m_MinX;
    float m_MaxX;
    float m_MinZ;
    float m_MaxZ;
    bool m_BoundsReady;

    public event Action OnFetchRoundComplete;

    bool HasSpawnAreas => preferSpawnAreas && spawnAreas != null && spawnAreas.Length > 0;

    private void Awake()
    {
        ComputeBounds();
    }

    private void ComputeBounds()
    {
        if (ground != null)
        {
            Renderer renderer = ground.GetComponent<Renderer>();
            if (renderer != null)
            {
                Bounds b = renderer.bounds;
                m_MinX = b.min.x + boundsMargin;
                m_MaxX = b.max.x - boundsMargin;
                m_MinZ = b.min.z + boundsMargin;
                m_MaxZ = b.max.z - boundsMargin;
                m_BoundsReady = true;
                return;
            }
        }

        Debug.LogWarning("FetchArea: ground not assigned, using fallback bounds.");
        m_MinX = transform.position.x - 15f;
        m_MaxX = transform.position.x + 15f;
        m_MinZ = transform.position.z - 15f;
        m_MaxZ = transform.position.z + 15f;
        m_BoundsReady = true;
    }

    public Vector3 ClampToMap(Vector3 pos)
    {
        if (!m_BoundsReady)
        {
            ComputeBounds();
        }

        pos.x = Mathf.Clamp(pos.x, m_MinX, m_MaxX);
        pos.z = Mathf.Clamp(pos.z, m_MinZ, m_MaxZ);
        return pos;
    }

    public float GetMapDiagonal()
    {
        if (!m_BoundsReady)
        {
            ComputeBounds();
        }

        float dx = m_MaxX - m_MinX;
        float dz = m_MaxZ - m_MinZ;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    public int GetNearestSpawnAreaIndex(Vector3 position)
    {
        if (spawnAreas == null || spawnAreas.Length == 0)
        {
            return -1;
        }

        int bestIndex = 0;
        float bestDist = float.MaxValue;

        for (int i = 0; i < spawnAreas.Length; i++)
        {
            if (spawnAreas[i] == null)
            {
                continue;
            }

            float dist = Vector3.Distance(position, spawnAreas[i].transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private bool IsSpawnPositionClear(Vector3 pos, float clearanceRadius)
    {
        Collider[] hits = Physics.OverlapSphere(pos, clearanceRadius);
        foreach (Collider hit in hits)
        {
            if (hit == null || hit.isTrigger)
            {
                continue;
            }

            if (hit.CompareTag("wall"))
            {
                return false;
            }
        }

        return true;
    }

    private Vector3 SampleInsideSpawnArea(Transform spawnTransform, float y)
    {
        float xRange = spawnTransform.localScale.x / 2.1f;
        float zRange = spawnTransform.localScale.z / 2.1f;

        return ClampToMap(new Vector3(
            Random.Range(-xRange, xRange),
            y,
            Random.Range(-zRange, zRange)
        ) + spawnTransform.position);
    }

    private Vector3 FindValidPositionInSpawnArea(int spawnAreaIndex, float y, float clearanceRadius)
    {
        Transform spawnTransform = spawnAreas[spawnAreaIndex].transform;
        Vector3 best = SampleInsideSpawnArea(spawnTransform, y);

        if (IsSpawnPositionClear(best, clearanceRadius))
        {
            return best;
        }

        for (int i = 0; i < spawnValidationAttempts; i++)
        {
            Vector3 candidate = SampleInsideSpawnArea(spawnTransform, y);
            if (IsSpawnPositionClear(candidate, clearanceRadius))
            {
                return candidate;
            }
        }

        return best;
    }

    private Vector3 FindValidRandomPosition(float y, float clearanceRadius, int maxAttempts)
    {
        if (!m_BoundsReady)
        {
            ComputeBounds();
        }

        Vector3 best = ClampToMap(new Vector3(
            Random.Range(m_MinX, m_MaxX),
            y + transform.position.y,
            Random.Range(m_MinZ, m_MaxZ)
        ));

        if (IsSpawnPositionClear(best, clearanceRadius))
        {
            return best;
        }

        for (int i = 1; i < maxAttempts; i++)
        {
            Vector3 candidate = ClampToMap(new Vector3(
                Random.Range(m_MinX, m_MaxX),
                y + transform.position.y,
                Random.Range(m_MinZ, m_MaxZ)
            ));

            if (IsSpawnPositionClear(candidate, clearanceRadius))
            {
                return candidate;
            }
        }

        return best;
    }

    private List<int> GetSpawnAreaCandidatesAwayFrom(Vector3 avoidPos, float minDistance)
    {
        List<int> candidates = new List<int>();
        for (int i = 0; i < spawnAreas.Length; i++)
        {
            GameObject area = spawnAreas[i];
            if (area == null)
            {
                continue;
            }

            if (Vector3.Distance(area.transform.position, avoidPos) >= minDistance)
            {
                candidates.Add(i);
            }
        }

        return candidates;
    }

    public int GetRandomSpawnAreaIndex()
    {
        return GetRandomSpawnAreaIndexFromSet(null);
    }

    int GetRandomSpawnAreaIndexFromSet(int[] allowedIndices)
    {
        if (spawnAreas == null || spawnAreas.Length == 0)
        {
            return -1;
        }

        if (allowedIndices != null && allowedIndices.Length > 0)
        {
            int randomListIndex = Random.Range(0, allowedIndices.Length);
            int candidate = allowedIndices[randomListIndex];
            if (candidate >= 0 && candidate < spawnAreas.Length && spawnAreas[candidate] != null)
            {
                return candidate;
            }
        }

        return Random.Range(0, spawnAreas.Length);
    }

    public int GetRandomSpawnAreaIndexAwayFrom(Vector3 avoidPos, float minDistance)
    {
        return GetRandomSpawnAreaIndexAwayFrom(avoidPos, minDistance, null);
    }

    public int GetRandomSpawnAreaIndexAwayFrom(Vector3 avoidPos, float minDistance, int[] allowedIndices)
    {
        if (spawnAreas == null || spawnAreas.Length == 0)
        {
            return -1;
        }

        List<int> candidates = GetSpawnAreaCandidatesAwayFrom(avoidPos, minDistance);
        if (allowedIndices != null && allowedIndices.Length > 0)
        {
            candidates.RemoveAll(index => Array.IndexOf(allowedIndices, index) < 0);
        }
        if (candidates.Count > 0)
        {
            return candidates[Random.Range(0, candidates.Count)];
        }

        int bestIndex = 0;
        float bestDist = -1f;
        for (int i = 0; i < spawnAreas.Length; i++)
        {
            if (allowedIndices != null && allowedIndices.Length > 0 && Array.IndexOf(allowedIndices, i) < 0)
            {
                continue;
            }

            GameObject area = spawnAreas[i];
            if (area == null)
            {
                continue;
            }

            float dist = Vector3.Distance(area.transform.position, avoidPos);
            if (dist > bestDist)
            {
                bestDist = dist;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    public Vector3 GetRandomPosition(float y)
    {
        return GetRandomPosition(y, 0.1f);
    }

    public Vector3 GetRandomPosition(float y, float clearanceRadius)
    {
        if (HasSpawnAreas)
        {
            int index = GetRandomSpawnAreaIndex();
            return FindValidPositionInSpawnArea(index, y, clearanceRadius);
        }

        return FindValidRandomPosition(y, clearanceRadius, spawnValidationAttempts);
    }

    public Vector3 GetRandomPositionFromAllowedAreas(float y, float clearanceRadius, int[] allowedIndices)
    {
        if (HasSpawnAreas)
        {
            int index = GetRandomSpawnAreaIndexFromSet(allowedIndices);
            return FindValidPositionInSpawnArea(index, y, clearanceRadius);
        }

        return FindValidRandomPosition(y, clearanceRadius, spawnValidationAttempts);
    }

    public Vector3 GetRandomPositionAwayFrom(float y, Vector3 avoidPos, float minDistance, int maxAttempts = 30)
    {
        return GetRandomPositionAwayFrom(y, 0.1f, avoidPos, minDistance, maxAttempts);
    }

    public Vector3 GetRandomPositionAwayFrom(float y, float clearanceRadius, Vector3 avoidPos, float minDistance, int maxAttempts = 30)
    {
        if (HasSpawnAreas)
        {
            int index = GetRandomSpawnAreaIndexAwayFrom(avoidPos, minDistance);
            return FindValidPositionInSpawnArea(index, y, clearanceRadius);
        }

        Vector3 best = GetRandomPosition(y, clearanceRadius);
        float bestDist = Vector3.Distance(best, avoidPos);

        for (int i = 1; i < maxAttempts; i++)
        {
            Vector3 candidate = GetRandomPosition(y, clearanceRadius);
            float dist = Vector3.Distance(candidate, avoidPos);
            if (dist >= minDistance)
            {
                return candidate;
            }

            if (dist > bestDist)
            {
                best = candidate;
                bestDist = dist;
            }
        }

        return best;
    }

    public Vector3 GetRandomPositionAwayFromAllowedAreas(float y, float clearanceRadius, Vector3 avoidPos, float minDistance, int[] allowedIndices, int maxAttempts = 30)
    {
        if (HasSpawnAreas)
        {
            int index = GetRandomSpawnAreaIndexAwayFrom(avoidPos, minDistance, allowedIndices);
            return FindValidPositionInSpawnArea(index, y, clearanceRadius);
        }

        return GetRandomPositionAwayFrom(y, clearanceRadius, avoidPos, minDistance, maxAttempts);
    }

    public void PlaceGoalFixed()
    {
        if (goal == null)
        {
            Debug.LogError("FetchArea: goal reference is not assigned!");
            return;
        }

        if (goalSpawnIndex >= 0 && goalSpawnIndex < spawnAreas.Length)
        {
            PlaceObject(goal, goalSpawnIndex, goalSpawnY);
        }
        else
        {
            goal.transform.position = GetRandomPosition(goalSpawnY, goalSpawnClearance);
        }

        goal.SetActive(true);
    }

    public void PlaceAgentNearGoal(GameObject agent)
    {
        Vector3 pos = GetRandomPositionAwayFrom(
            agentSpawnY,
            agentSpawnClearance,
            goal.transform.position,
            0.8f,
            spawnValidationAttempts);

        agent.transform.position = ClampToMap(pos);
    }

    public void PlaceObject(GameObject objectToPlace, int spawnAreaIndex, float spawnY)
    {
        float clearance = ballSpawnClearance;
        if (objectToPlace == goal)
        {
            clearance = goalSpawnClearance;
        }
        else if (objectToPlace.CompareTag("agent"))
        {
            clearance = agentSpawnClearance;
        }

        objectToPlace.transform.position = FindValidPositionInSpawnArea(spawnAreaIndex, spawnY, clearance);
    }

    public void PlaceBall(GameObject ball, int spawnAreaIndex)
    {
        PlaceObject(ball, spawnAreaIndex, ballSpawnY);
    }

    public void NotifyFetchRoundComplete()
    {
        OnFetchRoundComplete?.Invoke();
    }

    public override void ResetArea()
    {
    }
}
