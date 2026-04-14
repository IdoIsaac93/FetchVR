using UnityEngine;
using System.Collections;

public class FetchBall : MonoBehaviour
{
    [Header("Mode")]
    [Tooltip("When true, ball is managed by VR player (thrown by hand). When false, ball is spawned by training system.")]
    public bool isGameMode;

    bool m_IsCollected;
    FetchArea m_AreaComponent;
    Rigidbody m_Rb;
    Collider m_Col;
    Coroutine m_IgnoreCollisionCoroutine;

    // Original parent so we can un-attach on reset
    Transform m_OriginalParent;

    // Last valid spawn index for fallback repositioning (training mode)
    int m_LastSpawnIndex;

    // Fall detection threshold
    const float k_FallThreshold = -5f;

    // Game mode: position to respawn ball when it falls out of bounds
    Vector3 m_LastValidPosition;

    public bool IsCollected => m_IsCollected;

    void Awake()
    {
        m_Rb = GetComponent<Rigidbody>();
        m_Col = GetComponent<Collider>();
        m_OriginalParent = transform.parent;

        // In training mode, area component is expected on parent
        if (!isGameMode && transform.parent != null)
        {
            m_AreaComponent = transform.parent.GetComponent<FetchArea>();
        }

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

    // ───────────────────────────── Training-mode API ─────────────────────────────

    /// <summary>
    /// Reset ball to a specific spawn area (training mode only).
    /// </summary>
    public void ResetBall(int spawnAreaIndex)
    {
        // Detach from agent if attached
        transform.SetParent(m_OriginalParent);

        m_IsCollected = false;
        m_LastSpawnIndex = spawnAreaIndex;
        transform.rotation = Quaternion.identity;

        ReenablePhysics();

        gameObject.SetActive(true);

        if (m_AreaComponent != null)
        {
            m_AreaComponent.PlaceBall(gameObject, spawnAreaIndex);
        }
    }

    // ───────────────────────────── Game-mode API ─────────────────────────────

    /// <summary>
    /// Hold the ball at a world position (e.g. player hand). Makes it kinematic and disables collider.
    /// </summary>
    public void HoldAtPosition(Vector3 worldPos)
    {
        m_IsCollected = false;

        // Detach from agent if parented
        if (transform.parent != m_OriginalParent)
        {
            transform.SetParent(m_OriginalParent);
        }

        transform.position = worldPos;
        transform.rotation = Quaternion.identity;

        if (m_Rb != null)
        {
            m_Rb.isKinematic = true;
            // m_Rb.linearVelocity = Vector3.zero;
            // m_Rb.angularVelocity = Vector3.zero;
        }

        if (m_Col != null)
        {
            m_Col.enabled = false;
        }
    }

    /// <summary>
    /// Release the ball from hand with a given velocity. Re-enables physics and collider.
    /// </summary>
    public void ThrowBall(Vector3 velocity)
    {
        m_IsCollected = false;

        if (m_Rb != null)
        {
            m_Rb.isKinematic = false;
            m_Rb.linearVelocity = velocity;
            m_Rb.angularVelocity = Vector3.zero;
        }

        if (m_Col != null)
        {
            m_Col.enabled = true;
        }
    }

    public void ThrowBallFrom(Vector3 worldPos, Vector3 velocity, Collider[] ignoreColliders, float ignoreDuration = 0.2f)
    {
        if (transform.parent != m_OriginalParent)
        {
            transform.SetParent(m_OriginalParent);
        }

        transform.position = worldPos;
        transform.rotation = Quaternion.identity;

        ThrowBall(velocity);

        if (m_Col != null && ignoreColliders != null && ignoreColliders.Length > 0)
        {
            if (m_IgnoreCollisionCoroutine != null)
            {
                StopCoroutine(m_IgnoreCollisionCoroutine);
            }

            m_IgnoreCollisionCoroutine = StartCoroutine(IgnoreCollisionsTemporarily(ignoreColliders, ignoreDuration));
        }
    }

    /// <summary>
    /// Call when the VR player picks up the ball to prepare it for a new throw.
    /// Resets the collected state so the agent can collect it again after throwing.
    /// </summary>
    public void PrepareForThrow()
    {
        m_IsCollected = false;

        // Detach from agent if it was still parented
        if (transform.parent != m_OriginalParent)
        {
            transform.SetParent(m_OriginalParent);
        }

        ReenablePhysics();
    }

    /// <summary>
    /// Drop the ball from the agent's mouth (called when dog returns to player).
    /// Ball becomes physics-enabled again so the player can pick it up.
    /// </summary>
    public void DropBall()
    {
        m_IsCollected = false;

        // Detach from agent
        transform.SetParent(m_OriginalParent);

        ReenablePhysics();

        // Give a slight downward velocity so it drops naturally
        if (m_Rb != null)
        {
            m_Rb.linearVelocity = Vector3.down * 0.5f;
        }
    }

    /// <summary>
    /// Attach the ball to the agent's mouth (called by FetchAgent distance check in game mode,
    /// or by OnCollisionEnter in training mode).
    /// </summary>
    public void AttachToAgent(Transform agentTransform)
    {
        if (m_IsCollected) return;

        m_IsCollected = true;

        if (m_Rb != null)
        {
            m_Rb.isKinematic = true;
            m_Rb.linearVelocity = Vector3.zero;
            m_Rb.angularVelocity = Vector3.zero;
        }

        if (m_Col != null)
        {
            m_Col.enabled = false;
        }

        transform.SetParent(agentTransform);
        transform.localPosition = Vector3.forward * 0.5f + Vector3.up * 0.3f;
    }

    // ───────────────────────────── Shared ─────────────────────────────

    void ReenablePhysics()
    {
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
    }

    IEnumerator IgnoreCollisionsTemporarily(Collider[] ignoreColliders, float duration)
    {
        for (int i = 0; i < ignoreColliders.Length; i++)
        {
            if (ignoreColliders[i] != null && m_Col != null)
            {
                Physics.IgnoreCollision(m_Col, ignoreColliders[i], true);
            }
        }

        yield return new WaitForSeconds(duration);

        for (int i = 0; i < ignoreColliders.Length; i++)
        {
            if (ignoreColliders[i] != null && m_Col != null)
            {
                Physics.IgnoreCollision(m_Col, ignoreColliders[i], false);
            }
        }

        m_IgnoreCollisionCoroutine = null;
    }

    void FixedUpdate()
    {
        // Track last valid position (above ground, not collected)
        if (!m_IsCollected && transform.position.y > k_FallThreshold)
        {
            m_LastValidPosition = transform.position;
        }

        // If ball falls below the map, reposition it
        if (!m_IsCollected && transform.position.y < k_FallThreshold)
        {
            Debug.LogWarning("FetchBall: ball fell out of map, repositioning.");

            if (m_Rb != null)
            {
                m_Rb.linearVelocity = Vector3.zero;
                m_Rb.angularVelocity = Vector3.zero;
            }

            if (isGameMode)
            {
                // Game mode: return to last known good position
                transform.position = m_LastValidPosition;
            }
            else if (m_AreaComponent != null)
            {
                // Training mode: use spawn area
                m_AreaComponent.PlaceBall(gameObject, m_LastSpawnIndex);
            }
        }
    }

    void OnCollisionEnter(Collision other)
    {
        if (!other.gameObject.CompareTag("agent") || m_IsCollected)
            return;

        // In game mode, ball pickup is handled by FetchAgent distance check — skip collision
        if (isGameMode)
            return;

        // Training mode: collision-based pickup
        var agent = other.gameObject.GetComponent<FetchAgent>();
        if (agent == null)
            return;

        AttachToAgent(other.transform);
        agent.OnBallCollected();
    }
}
