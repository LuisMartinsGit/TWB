using UnityEngine;

/// <summary>
/// Modern RTS Camera System with Rig Architecture
/// 
/// Hierarchy:
///   CameraRig (this script)        ← Focus point, moves in world space
///   └─ CameraArm (child)           ← Offsets back and up for tilt
///      └─ Camera (grandchild)      ← Actual camera, looks at rig center
///
/// Features:
/// - Edge scrolling
/// - Middle mouse drag panning
/// - Scroll wheel zoom
/// - Q/E or Right-drag rotation
/// - R/F tilt control
/// - WASD movement
/// - Smooth damping
/// - World bounds clamping
/// - Minimap click support
/// </summary>
public class RTSCameraRig : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Auto-created if null")]
    public Camera mainCamera;
    
    [Header("Movement")]
    public float keyboardSpeed = 25f;
    public float edgeScrollSpeed = 30f;
    public float edgeScrollBorder = 15f; // pixels from screen edge
    public float panSpeed = 1f; // Middle mouse sensitivity
    public float moveDamping = 0.15f;
    
    [Header("Zoom")]
    public float zoomSpeed = 10f;
    public float minZoom = 15f; // Camera distance from rig
    public float maxZoom = 80f;
    public float zoomDamping = 0.2f;
    
    [Header("Rotation")]
    public float rotationSpeed = 100f;
    public float mouseRotationSpeed = 0.3f;
    public float rotationDamping = 0.15f;
    
    [Header("Tilt")]
    public float tiltSpeed = 30f;
    public float minTilt = 30f; // degrees
    public float maxTilt = 75f;
    public float tiltDamping = 0.15f;
    
    [Header("World Bounds")]
    public Vector2 worldMin = new Vector2(-125, -125);
    public Vector2 worldMax = new Vector2(125, 125);
    
    [Header("Debug")]
    public bool showDebugInfo = false;

    // Internal state
    private Transform _arm;
    private Transform _camTransform;
    private Vector3 _targetPosition;
    private Vector3 _velocity = Vector3.zero;
    private float _currentZoom;
    private float _targetZoom;
    private float _zoomVelocity;
    private float _currentRotation; // Y-axis rotation
    private float _targetRotation;
    private float _rotationVelocity;
    private float _currentTilt; // X-axis rotation (pitch)
    private float _targetTilt;
    private float _tiltVelocity;
    
    private Vector3? _lastMousePanPos;
    private bool _isRotatingWithMouse;

    void Start()
    {
        InitializeCameraRig();
        
        // Start at current position
        _targetPosition = transform.position;
        
        // Calculate zoom from camera's local position (distance from arm center)
        _currentZoom = _targetZoom = _camTransform.localPosition.magnitude;
        
        _currentRotation = _targetRotation = transform.eulerAngles.y;
        _currentTilt = _targetTilt = _arm.localEulerAngles.x;
        
        Debug.Log($"[RTSCameraRig] Start - Rig pos: {transform.position}, Camera local: {_camTransform.localPosition}, Zoom: {_currentZoom}");
        
        // Clamp to bounds
        ClampPositionToBounds(ref _targetPosition);
    }

    void InitializeCameraRig()
    {
        // Create or find camera
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                var camGO = new GameObject("Main Camera");
                camGO.tag = "MainCamera";
                mainCamera = camGO.AddComponent<Camera>();
                mainCamera.clearFlags = CameraClearFlags.Skybox;
                mainCamera.fieldOfView = 40f;
                mainCamera.nearClipPlane = 0.1f;
                mainCamera.farClipPlane = 5000f;
                camGO.AddComponent<AudioListener>();
            }
        }

        _camTransform = mainCamera.transform;

        // Create arm if needed
        _arm = transform.Find("CameraArm");
        if (_arm == null)
        {
            var armGO = new GameObject("CameraArm");
            _arm = armGO.transform;
            _arm.SetParent(transform, false);
        }

        // Parent camera under arm (keeping world position initially)
        bool wasReparented = false;
        if (_camTransform.parent != _arm)
        {
            _camTransform.SetParent(_arm, true); // true = keep world position
            wasReparented = true;
        }

        // Always set initial configuration when camera is reparented
        // OR if the rig is at origin (first time setup)
        if (wasReparented || transform.position.sqrMagnitude < 0.1f)
        {
            // Position rig at origin (ground level focus point)
            transform.position = new Vector3(0f, 0f, 0f);
            transform.rotation = Quaternion.identity;
            
            // Tilt arm (this rotates the camera view downward)
            _arm.localPosition = Vector3.zero;
            _arm.localRotation = Quaternion.Euler(55f, 0f, 0f);
            
            // Position camera back from arm (negative Z = back in local space)
            // This creates the "looking down at an angle" effect
            _camTransform.localPosition = new Vector3(0f, 0f, -40f);
            _camTransform.localRotation = Quaternion.identity;
            
            Debug.Log($"[RTSCameraRig] Initialized camera at world pos: {_camTransform.position}, local: {_camTransform.localPosition}");
        }
    }

    void Update()
    {
        HandleKeyboardMovement();
        HandleEdgeScrolling();
        HandleMousePan();
        HandleRotation();
        HandleTilt();
        HandleZoom();
        
        ApplySmoothMovement();
    }

    void HandleKeyboardMovement()
    {
        Vector3 input = Vector3.zero;
        
        if (Input.GetKey(KeyCode.W)) input.z += 1f;
        if (Input.GetKey(KeyCode.S)) input.z -= 1f;
        if (Input.GetKey(KeyCode.A)) input.x -= 1f;
        if (Input.GetKey(KeyCode.D)) input.x += 1f;
        
        if (input.sqrMagnitude > 0.01f)
        {
            // Move relative to camera rotation
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            
            Vector3 moveDir = (forward * input.z + right * input.x).normalized;
            _targetPosition += moveDir * keyboardSpeed * Time.deltaTime;
            ClampPositionToBounds(ref _targetPosition);
        }
    }

    void HandleEdgeScrolling()
    {
        Vector3 mousePos = Input.mousePosition;
        Vector3 edgeMove = Vector3.zero;
        
        if (mousePos.x < edgeScrollBorder) edgeMove.x = -1f;
        else if (mousePos.x > Screen.width - edgeScrollBorder) edgeMove.x = 1f;
        
        if (mousePos.y < edgeScrollBorder) edgeMove.z = -1f;
        else if (mousePos.y > Screen.height - edgeScrollBorder) edgeMove.z = 1f;
        
        if (edgeMove.sqrMagnitude > 0.01f)
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            
            Vector3 moveDir = (forward * edgeMove.z + right * edgeMove.x).normalized;
            _targetPosition += moveDir * edgeScrollSpeed * Time.deltaTime;
            ClampPositionToBounds(ref _targetPosition);
        }
    }

    void HandleMousePan()
    {
        // Middle mouse button for panning
        if (Input.GetMouseButtonDown(2))
        {
            _lastMousePanPos = Input.mousePosition;
        }
        else if (Input.GetMouseButton(2) && _lastMousePanPos.HasValue)
        {
            Vector3 delta = Input.mousePosition - _lastMousePanPos.Value;
            _lastMousePanPos = Input.mousePosition;
            
            // Pan in camera-relative directions
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            
            Vector3 move = (-right * delta.x - forward * delta.y) * panSpeed * (_currentZoom / 40f);
            _targetPosition += move;
            ClampPositionToBounds(ref _targetPosition);
        }
        else if (Input.GetMouseButtonUp(2))
        {
            _lastMousePanPos = null;
        }
    }

    void HandleRotation()
    {
        // Keyboard rotation
        if (Input.GetKey(KeyCode.Q))
        {
            _targetRotation -= rotationSpeed * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.E))
        {
            _targetRotation += rotationSpeed * Time.deltaTime;
        }
        
        // Right mouse drag rotation
        if (Input.GetMouseButtonDown(1))
        {
            _isRotatingWithMouse = true;
        }
        else if (Input.GetMouseButton(1) && _isRotatingWithMouse)
        {
            float mouseDelta = Input.GetAxis("Mouse X");
            _targetRotation += mouseDelta * mouseRotationSpeed * 100f * Time.deltaTime;
        }
        else if (Input.GetMouseButtonUp(1))
        {
            _isRotatingWithMouse = false;
        }
    }

    void HandleTilt()
    {
        if (Input.GetKey(KeyCode.R))
        {
            _targetTilt = Mathf.Clamp(_targetTilt - tiltSpeed * Time.deltaTime, minTilt, maxTilt);
        }
        if (Input.GetKey(KeyCode.F))
        {
            _targetTilt = Mathf.Clamp(_targetTilt + tiltSpeed * Time.deltaTime, minTilt, maxTilt);
        }
    }

    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            _targetZoom -= scroll * zoomSpeed;
            _targetZoom = Mathf.Clamp(_targetZoom, minZoom, maxZoom);
        }
    }

    void ApplySmoothMovement()
    {
        // Position (ensure Y stays at 0 - rig is ground-level focus point)
        transform.position = Vector3.SmoothDamp(transform.position, _targetPosition, ref _velocity, moveDamping);
        transform.position = new Vector3(transform.position.x, 0f, transform.position.z);
        
        // Rotation (Y-axis)
        _currentRotation = Mathf.SmoothDampAngle(_currentRotation, _targetRotation, ref _rotationVelocity, rotationDamping);
        transform.rotation = Quaternion.Euler(0f, _currentRotation, 0f);
        
        // Tilt (X-axis on arm)
        _currentTilt = Mathf.SmoothDampAngle(_currentTilt, _targetTilt, ref _tiltVelocity, tiltDamping);
        _arm.localRotation = Quaternion.Euler(_currentTilt, 0f, 0f);
        
        // Zoom (camera distance) - negative Z in local space moves camera back
        _currentZoom = Mathf.SmoothDamp(_currentZoom, _targetZoom, ref _zoomVelocity, zoomDamping);
        _camTransform.localPosition = new Vector3(0f, 0f, -_currentZoom);
    }

    void ClampPositionToBounds(ref Vector3 pos)
    {
        pos.x = Mathf.Clamp(pos.x, worldMin.x, worldMax.x);
        pos.z = Mathf.Clamp(pos.z, worldMin.y, worldMax.y);
    }

    /// <summary>
    /// Public method for external systems (like minimap) to move the rig
    /// </summary>
    public void MoveToPosition(Vector3 worldPos, bool instant = false)
    {
        // Rig always stays at ground level (Y=0)
        _targetPosition = new Vector3(worldPos.x, 0f, worldPos.z);
        ClampPositionToBounds(ref _targetPosition);
        
        if (instant)
        {
            transform.position = _targetPosition;
            _velocity = Vector3.zero;
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[RTSCameraRig] MoveToPosition called: target=({worldPos.x:F1}, {worldPos.z:F1}), clamped=({_targetPosition.x:F1}, {_targetPosition.z:F1})");
        }
    }

    /// <summary>
    /// Get the ground focus point (rig center projected to Y=0)
    /// </summary>
    public Vector3 GetGroundFocusPoint()
    {
        Vector3 pos = transform.position;
        return new Vector3(pos.x, 0f, pos.z);
    }

    void OnDrawGizmos()
    {
        if (!showDebugInfo) return;
        
        // Draw rig center
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 1f);
        
        // Draw ground focus point
        Vector3 groundPoint = new Vector3(transform.position.x, 0f, transform.position.z);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundPoint, 1.5f);
        Gizmos.DrawLine(transform.position, groundPoint);
        
        // Draw world bounds
        Gizmos.color = Color.cyan;
        Vector3 c1 = new Vector3(worldMin.x, 0, worldMin.y);
        Vector3 c2 = new Vector3(worldMax.x, 0, worldMin.y);
        Vector3 c3 = new Vector3(worldMax.x, 0, worldMax.y);
        Vector3 c4 = new Vector3(worldMin.x, 0, worldMax.y);
        Gizmos.DrawLine(c1, c2);
        Gizmos.DrawLine(c2, c3);
        Gizmos.DrawLine(c3, c4);
        Gizmos.DrawLine(c4, c1);
    }

    void OnGUI()
    {
        if (!showDebugInfo) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label($"Position: {transform.position}");
        GUILayout.Label($"Rotation: {_currentRotation:F1}°");
        GUILayout.Label($"Tilt: {_currentTilt:F1}°");
        GUILayout.Label($"Zoom: {_currentZoom:F1}");
        GUILayout.Label($"\nControls:");
        GUILayout.Label($"WASD - Move | Edge - Scroll");
        GUILayout.Label($"Mouse3 - Pan | Wheel - Zoom");
        GUILayout.Label($"Q/E - Rotate | Right Drag - Rotate");
        GUILayout.Label($"R/F - Tilt");
        GUILayout.EndArea();
    }
}