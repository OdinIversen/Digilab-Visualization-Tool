using UnityEngine;

public class OrbitCamera : MonoBehaviour
{
    public Transform target;
    public LoadTimeSeries timeSeriesHandeler;
    public float distance = 50.0f;
    public float rotationSpeed = 50.0f;

    private float _horizontalRotation;
    private float _verticalRotation;
    private Vector3 positionOffset;
    public Quaternion rotation;

    void Start()
    {
        if (target == null)
        {
            Debug.LogError("No target GameObject assigned for the OrbitCamera script.");
            return;
        }

        transform.position = target.position + (Vector3.back * distance);
        transform.LookAt(target);
    }

    void LateUpdate()
    {
        if (target != null)
        {
            float horizontalInput = Input.GetAxis("Horizontal");
            float verticalInput = Input.GetAxis("Vertical");

            _horizontalRotation += horizontalInput * rotationSpeed * Time.deltaTime;
            _verticalRotation -= verticalInput * rotationSpeed * Time.deltaTime;
            _verticalRotation = Mathf.Clamp(_verticalRotation, -80, 80);

            rotation = Quaternion.Euler(_verticalRotation, _horizontalRotation, 0);

            if (timeSeriesHandeler.timeLineCanvasInstance != null)
            {

                positionOffset = rotation * (Vector3.back * (distance + 10f));
            }
            else
            {
                positionOffset = rotation * (Vector3.back * (distance));
            }

            transform.position = target.position + positionOffset;
            transform.LookAt(target);
        }
    }
}