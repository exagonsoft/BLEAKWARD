using Unity.Cinemachine;
using UnityEngine;

public class CameraHandler : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private int zoomAmount = 2;
    [SerializeField] private float zoomSpeed = 5f;
    [SerializeField] private CinemachineCamera cameraBase;
    [SerializeField] private float minOrthoSize = 10f;
    [SerializeField] private float maxOrthoSize = 30f;
    private float ortograficSize;
    private float targetOrthoSize;

    private void Start()
    {
        ortograficSize = cameraBase.Lens.OrthographicSize;
        targetOrthoSize = ortograficSize;
    }

    private void Update()
    {
        HandleCameraMovement();
        HandleZoom();
    }

    private void HandleCameraMovement()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");

        Vector2 moveDir = new Vector2(x, y);
        transform.position += (Vector3)moveDir * moveSpeed * Time.deltaTime;
    }

    private void HandleZoom()
    {
        targetOrthoSize -= Input.mouseScrollDelta.y * zoomAmount;
        targetOrthoSize = Mathf.Clamp(targetOrthoSize, minOrthoSize, maxOrthoSize);

        ortograficSize = Mathf.Lerp(ortograficSize, targetOrthoSize, Time.deltaTime * zoomSpeed);

        cameraBase.Lens.OrthographicSize = ortograficSize;
    }
}
