
using UnityEngine;

public class RTSCameraController : MonoBehaviour
{
    public float moveSpeed = 20f;
    public float zoomSpeed = 200f;
    public float minY = 10f;
    public float maxY = 60f;

    void Start()
    {
        if (Camera.main == null)
        {
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.transform.position = new Vector3(0, 25, -25);
            cam.transform.rotation = Quaternion.Euler(45, 0, 0);
        }
    }

    void Update()
    {
        var cam = Camera.main.transform;
        Vector3 dir = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) dir += Vector3.forward;
        if (Input.GetKey(KeyCode.S)) dir += Vector3.back;
        if (Input.GetKey(KeyCode.A)) dir += Vector3.left;
        if (Input.GetKey(KeyCode.D)) dir += Vector3.right;
        cam.position += dir * moveSpeed * Time.deltaTime;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        cam.position += cam.forward * scroll * zoomSpeed * Time.deltaTime;
        cam.position = new Vector3(cam.position.x, Mathf.Clamp(cam.position.y, minY, maxY), cam.position.z);
    }
}
