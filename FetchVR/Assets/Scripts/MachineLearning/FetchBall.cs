using UnityEngine;

public class FetchBall : MonoBehaviour
{
    bool m_IsCollected;
    FetchArea m_AreaComponent;
    Rigidbody m_Rb;
    Collider m_Col;

    // Original parent so we can un-attach on reset
    Transform m_OriginalParent;

    // Last valid spawn index for fallback repositioning
    int m_LastSpawnIndex;

    // Fall detection threshold
    const float k_FallThreshold = -5f;

    public bool IsCollected => m_IsCollected;

    void Awake()
    {
        m_AreaComponent = transform.parent.GetComponent<FetchArea>();
        m_Rb = GetComponent<Rigidbody>();
        m_Col = GetComponent<Collider>();
        m_OriginalParent = transform.parent;

        // Apply high-friction physics material (tennis ball on grass)
        ApplyGrassFriction();
    }

    void ApplyGrassFriction()
    {
        if (m_Col == null) return;
        var mat = new PhysicsMaterial("TennisBallGrass")
        {
            dynamicFriction = 0.8f,
            staticFriction = 1.0f,
            bounciness = 0.3f,
            frictionCombine = PhysicsMaterialCombine.Maximum
        };
        m_Col.material = mat;
    }

    public void ResetBall(int spawnAreaIndex)
    {
        // Detach from agent if attached
        transform.SetParent(m_OriginalParent);

        m_IsCollected = false;
        m_LastSpawnIndex = spawnAreaIndex;
        transform.rotation = Quaternion.identity;

        // Re-enable physics
        if (m_Rb != null)
        {
            m_Rb.isKinematic = false;
            m_Rb.linearVelocity = Vector3.zero;
            m_Rb.angularVelocity = Vector3.zero;
        }
        if (m_Col != null)
        {
            m_Col.enabled = true;
        }

        gameObject.SetActive(true);
        m_AreaComponent.PlaceBall(gameObject, spawnAreaIndex);
    }

    void FixedUpdate()
    {
        // If ball falls below the map, reposition it
        if (!m_IsCollected && transform.position.y < k_FallThreshold)
        {
            Debug.LogWarning("FetchBall: ball fell out of map, repositioning.");
            if (m_Rb != null)
            {
                m_Rb.linearVelocity = Vector3.zero;
                m_Rb.angularVelocity = Vector3.zero;
            }
            m_AreaComponent.PlaceBall(gameObject, m_LastSpawnIndex);
        }
    }

    void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.CompareTag("agent") && !m_IsCollected)
        {
            m_IsCollected = true;

            // Attach ball to agent — make kinematic so it doesn't bounce around
            if (m_Rb != null)
            {
                m_Rb.isKinematic = true;
                m_Rb.linearVelocity = Vector3.zero;
                m_Rb.angularVelocity = Vector3.zero;
            }

            // Disable collider so ball doesn't interfere with agent physics
            if (m_Col != null)
            {
                m_Col.enabled = false;
            }

            // Parent to agent and position at its "mouth"
            Transform agentTransform = other.transform;
            transform.SetParent(agentTransform);
            transform.localPosition = agentTransform.forward * 0.5f + Vector3.up * 0.3f;

            // Notify agent
            var agent = other.gameObject.GetComponent<FetchAgent>();
            if (agent != null)
            {
                agent.OnBallCollected();
            }
        }
    }
}
