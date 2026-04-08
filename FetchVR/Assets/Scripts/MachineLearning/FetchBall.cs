using UnityEngine;

public class FetchBall : MonoBehaviour
{
    //public GameObject myBall;
    bool m_State;
    GameObject m_Area;
    FetchArea m_AreaComponent;
    int m_GoalIndex;

    public bool GetState()
    {
        return m_State;
    }

    void Start()
    {
        m_Area = gameObject.transform.parent.gameObject;
        m_AreaComponent = m_Area.GetComponent<FetchArea>();
    }

    public void ResetSwitch(int spawnAreaIndex, int goalSpawnIndex)
    {
        m_AreaComponent.PlaceObject(gameObject, spawnAreaIndex);
        m_State = false;
        m_GoalIndex = goalSpawnIndex;
        transform.rotation = Quaternion.Euler(0f, 0f, 0f);
    }

    void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.CompareTag("agent") && m_State == false)
        {
            m_State = true;
            m_AreaComponent.CreateGoal(m_GoalIndex);
        }
    }
}