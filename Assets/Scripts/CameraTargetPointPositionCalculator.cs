using Speckle.ConnectorUnity.Wrappers;
using UnityEngine;
using System.Collections.Generic;

public class CameraTargetPointPositionCalculator : MonoBehaviour
{
    public List<GameObject> gameObjects;
    public OrbitCamera mainCamera;
    public float offsetDistance = 10f;
    private Vector3 offset;
    private Vector3 center;

    void Start()
    {
        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float minZ = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;
        float maxZ = float.NegativeInfinity;

        foreach (GameObject obj in gameObjects)
        {
            Transform rooms = obj.transform.Find("@Rooms");
            if (rooms != null)
            {
                foreach (Transform child in rooms)
                {
                    SpeckleProperties speckleProperties = child.GetComponent<SpeckleProperties>();
                    if (speckleProperties != null)
                    {
                        Bounds bounds = child.GetComponent<Renderer>().bounds;
                        minX = Mathf.Min(minX, bounds.min.x);
                        minY = Mathf.Min(minY, bounds.min.y);
                        minZ = Mathf.Min(minZ, bounds.min.z);
                        maxX = Mathf.Max(maxX, bounds.max.x);
                        maxY = Mathf.Max(maxY, bounds.max.y);
                        maxZ = Mathf.Max(maxZ, bounds.max.z);
                    }
                }
            }
        }

        center = new Vector3((minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2);
        transform.position = center;
    }

    void Update()
    {
        if (mainCamera.timeSeriesHandeler.timeLineCanvasInstance != null)
        {
            offset = mainCamera.rotation * Vector3.down * offsetDistance;
            transform.position = center + offset;
        }
        else
        {
            transform.position = center;
        }
    }
}