using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class FetchBall : MonoBehaviour
{
    static readonly Vector3 k_CarryLocalOffset = Vector3.forward * 0.5f + Vector3.up * 0.3f;

    [SerializeField] FetchArea areaOverride;
    [SerializeField] FetchAgent fetchAgentOverride;

    bool m_IsCollected;
    FetchArea m_AreaComponent;
    Rigidbody m_Rb;
    Collider m_Col;
    XRGrabInteractable m_GrabInteractable;
    FetchAgent m_FetchAgent;

    // Original parent so we can un-attach on reset
    Transform m_OriginalParent;
    Vector3 m_StartingPosition;
    Quaternion m_StartingRotation;

    // Last valid spawn index for fallback repositioning
    int m_LastSpawnIndex;

    // Fall detection threshold
    const float k_FallThreshold = -5f;

    public bool IsCollected => m_IsCollected;

    void Awake()
    {
        m_AreaComponent = areaOverride;
        if (m_AreaComponent == null && transform.parent != null)
        {
            m_AreaComponent = transform.parent.GetComponent<FetchArea>();
        }
        if (m_AreaComponent == null)
        {
            m_AreaComponent = FindObjectOfType<FetchArea>();
        }

        m_FetchAgent = fetchAgentOverride != null ? fetchAgentOverride : FindObjectOfType<FetchAgent>();
        m_Rb = GetComponent<Rigidbody>();
        m_Col = GetComponent<Collider>();
        m_GrabInteractable = GetComponent<XRGrabInteractable>();
        m_OriginalParent = transform.parent;
        m_StartingPosition = transform.position;
        m_StartingRotation = transform.rotation;

        // Apply high-friction physics material (tennis ball on grass)
        ApplyGrassFriction();
    }

    void OnEnable()
    {
        if (m_GrabInteractable != null)
        {
            m_GrabInteractable.selectExited.AddListener(OnBallReleased);
        }
    }

    void OnDisable()
    {
        if (m_GrabInteractable != null)
        {
            m_GrabInteractable.selectExited.RemoveListener(OnBallReleased);
        }
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
        if (m_AreaComponent == null)
        {
            Debug.LogError($"{nameof(FetchBall)} is missing a {nameof(FetchArea)} reference.", this);
            return;
        }

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

    public void ResetToStartingPose()
    {
        transform.SetParent(m_OriginalParent);
        m_IsCollected = false;
        transform.position = m_StartingPosition;
        transform.rotation = m_StartingRotation;

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
    }

    void FixedUpdate()
    {
        // If ball falls below the map, reposition it
        if (!m_IsCollected && transform.position.y < k_FallThreshold)
        {
            if (m_AreaComponent == null)
            {
                return;
            }

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
            var agent = other.gameObject.GetComponent<FetchAgent>();
            if (agent == null || !agent.fetchActive)
            {
                return;
            }

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
            transform.localPosition = k_CarryLocalOffset;
            transform.localRotation = Quaternion.identity;

            // Notify agent
            if (agent != null)
            {
                agent.OnBallCollected();
            }
        }
    }

    void OnBallReleased(SelectExitEventArgs args)
    {
        if (m_IsCollected)
        {
            return;
        }

        m_FetchAgent?.BeginFetch();
    }
}
