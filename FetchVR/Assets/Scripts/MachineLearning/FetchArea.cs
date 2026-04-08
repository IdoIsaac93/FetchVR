using UnityEngine;

public class FetchArea : Area
{
    public GameObject goal;
    public GameObject[] spawnAreas;
    public float range;

    public void CreateGoal(int spawnAreaIndex)
    {
        CreateObject(goal, spawnAreaIndex);
    }

    void CreateObject(GameObject desiredObject, int spawnAreaIndex)
    {
        var newObject = Instantiate(desiredObject, Vector3.zero, Quaternion.Euler(0f, 0f, 0f), transform);
        PlaceObject(newObject, spawnAreaIndex);
    }

    public void PlaceObject(GameObject objectToPlace, int spawnAreaIndex)
    {
        var spawnTransform = spawnAreas[spawnAreaIndex].transform;
        var xRange = spawnTransform.localScale.x / 2.1f;
        var zRange = spawnTransform.localScale.z / 2.1f;

        objectToPlace.transform.position = new Vector3(Random.Range(-xRange, xRange), 2f, Random.Range(-zRange, zRange))
            + spawnTransform.position;
    }

    public void CleanFetchArea()
    {
        foreach (Transform child in transform)
            if (child.CompareTag("goal"))
            {
                Destroy(child.gameObject);
            }
    }

    public override void ResetArea()
    {
    }
}