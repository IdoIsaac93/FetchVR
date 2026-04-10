using UnityEngine;

public class FetchArea : Area
{
    [Header("References")]
    public GameObject goal;         // The "player" — always in scene
    public GameObject[] spawnAreas; // 9 spawn zones
    public GameObject ground;       // The ground plane — used to compute bounds

    [Header("Spawn Settings")]
    public float agentSpawnY = 0.5f;
    public float ballSpawnY = 0.5f;
    public float goalSpawnY = 0.5f;

    [Header("Goal Settings")]
    [Tooltip("Which spawn area index to place the goal (player). -1 = use center of area.")]
    public int goalSpawnIndex = -1;

    // Cached map bounds (computed from ground)
    float m_MinX, m_MaxX, m_MinZ, m_MaxZ;
    bool m_BoundsReady;
    [Tooltip("Shrink map bounds inward by this margin to prevent edge spawns")]
    public float boundsMargin = 1.0f;

    void Awake()
    {
        ComputeBounds();
    }

    void ComputeBounds()
    {
        if (ground != null)
        {
            var renderer = ground.GetComponent<Renderer>();
            if (renderer != null)
            {
                var b = renderer.bounds;
                m_MinX = b.min.x + boundsMargin;
                m_MaxX = b.max.x - boundsMargin;
                m_MinZ = b.min.z + boundsMargin;
                m_MaxZ = b.max.z - boundsMargin;
                m_BoundsReady = true;
                return;
            }
        }

        // Fallback: use area transform position with default size
        Debug.LogWarning("FetchArea: ground not assigned, using fallback bounds.");
        m_MinX = transform.position.x - 15f;
        m_MaxX = transform.position.x + 15f;
        m_MinZ = transform.position.z - 15f;
        m_MaxZ = transform.position.z + 15f;
        m_BoundsReady = true;
    }

    /// <summary>
    /// Clamp a position to stay within map bounds.
    /// </summary>
    public Vector3 ClampToMap(Vector3 pos)
    {
        if (!m_BoundsReady) ComputeBounds();
        pos.x = Mathf.Clamp(pos.x, m_MinX, m_MaxX);
        pos.z = Mathf.Clamp(pos.z, m_MinZ, m_MaxZ);
        return pos;
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
            goal.transform.position = new Vector3(
                transform.position.x,
                goalSpawnY + transform.position.y,
                transform.position.z
            );
        }
        goal.SetActive(true);
    }

    public void PlaceAgentNearGoal(GameObject agent)
    {
        Vector3 goalPos = goal.transform.position;
        float offsetX = Random.Range(-1.5f, 1.5f);
        float offsetZ = Random.Range(-1.5f, 1.5f);
        Vector3 pos = new Vector3(
            goalPos.x + offsetX,
            agentSpawnY + transform.position.y,
            goalPos.z + offsetZ
        );
        agent.transform.position = ClampToMap(pos);
    }

    public void PlaceObject(GameObject objectToPlace, int spawnAreaIndex, float spawnY)
    {
        var spawnTransform = spawnAreas[spawnAreaIndex].transform;
        var xRange = spawnTransform.localScale.x / 2.1f;
        var zRange = spawnTransform.localScale.z / 2.1f;

        Vector3 pos = new Vector3(
            Random.Range(-xRange, xRange),
            spawnY,
            Random.Range(-zRange, zRange)
        ) + spawnTransform.position;

        objectToPlace.transform.position = ClampToMap(pos);
    }

    public void PlaceBall(GameObject ball, int spawnAreaIndex)
    {
        PlaceObject(ball, spawnAreaIndex, ballSpawnY);
    }

    public override void ResetArea()
    {
    }
}
