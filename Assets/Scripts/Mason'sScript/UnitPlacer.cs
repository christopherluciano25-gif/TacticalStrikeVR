using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

/// <summary>
/// VR placement system using Unity XR Input (works with Quest Link)
/// Right controller raycast for placement
/// Trigger: place unit
/// Grip: rotate wall/tower
/// </summary>
public class UnitPlacer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TacticalGrid tacticalGrid;
    [SerializeField] private LayerMask groundLayerMask;

    [Header("VR Controller Setup")]
    [SerializeField] private bool autoFindRightController = true;
    [SerializeField] private Transform rightControllerTransform;

    [Header("Raycast Settings")]
    [SerializeField] private float maxRayDistance = 200f;

    [Header("Visual Feedback")]
    [SerializeField] private bool showRay = true;
    [SerializeField] private LineRenderer rayLine;
    [SerializeField] private Color validRayColor = Color.green;
    [SerializeField] private Color invalidRayColor = Color.red;
    [SerializeField] private Color normalRayColor = Color.cyan;

    [Header("Preview Settings")]
    [SerializeField] private bool showPreview = true;
    [SerializeField] private float previewAlpha = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    // Current placement state
    private bool isPlacementMode = false;
    private GameObject currentPrefab = null;
    private GameObject previewObject = null;
    private UnitType currentUnitType = UnitType.Empty;
    private int currentWidth = 1;
    private int currentHeight = 1;
    private RotationAngle currentRotation = RotationAngle.Rotate0;
    private TeamSide playerTeam = TeamSide.Player;

    // Current hover state
    private GridCell currentHoverCell = null;
    private bool isValidPlacement = false;

    // Input devices
    private InputDevice rightHandDevice;
    private bool rightHandFound = false;

    private void Awake()
    {
        if (tacticalGrid == null)
            tacticalGrid = FindObjectOfType<TacticalGrid>();

        // Force destroy old ray visuals
        GameObject[] oldRays = GameObject.FindGameObjectsWithTag("Untagged");
        foreach (GameObject obj in oldRays)
        {
            if (obj.name == "RayVisual")
            {
                Debug.Log("[UnitPlacer] Destroying old RayVisual");
                Destroy(obj);
            }
        }

        // Auto-find right controller
        if (autoFindRightController && rightControllerTransform == null)
        {
            FindRightController();
        }

        if (rayLine == null && showRay)
            SetupRayLine();
    }

    private void Start()
    {
        Debug.Log("=== UNIT PLACER DIAGNOSTICS ===");
        Debug.Log($"[UnitPlacer] TacticalGrid: {(tacticalGrid != null ? "✓ FOUND" : "✗ NULL")}");
        Debug.Log($"[UnitPlacer] RightController: {(rightControllerTransform != null ? rightControllerTransform.name : "✗ NULL")}");
        Debug.Log($"[UnitPlacer] Ground Layer Mask: {groundLayerMask.value}");
        Debug.Log($"[UnitPlacer] Max Ray Distance: {maxRayDistance}");

        if (rightControllerTransform != null)
        {
            Debug.Log($"[UnitPlacer] Controller Position: {rightControllerTransform.position}");
            Debug.Log($"[UnitPlacer] Controller Forward: {rightControllerTransform.forward}");
        }

        // Find XR input device
        InitializeInputDevice();

        Debug.Log("==============================");
    }

    private void Update()
    {
        // Re-find device if lost
        if (!rightHandFound)
        {
            InitializeInputDevice();
        }

        // Perform raycast
        PerformRaycast();

        // Handle input
        if (isPlacementMode)
        {
            HandleRotationInput();
            HandlePlacementInput();
            UpdatePreview();
        }

        UpdateRayVisual();

        // Debug input state
        if (verboseLogging && Time.frameCount % 60 == 0 && rightHandFound)
        {
            rightHandDevice.TryGetFeatureValue(CommonUsages.grip, out float gripValue);
            rightHandDevice.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue);
            Debug.Log($"[UnitPlacer] Grip: {gripValue:F2} | Trigger: {triggerValue:F2}");
        }
    }

    #region XR Input Setup

    private void InitializeInputDevice()
    {
        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, devices);

        if (devices.Count > 0)
        {
            rightHandDevice = devices[0];
            rightHandFound = true;
            Debug.Log($"[UnitPlacer] ✓ Found right hand input device: {rightHandDevice.name}");
        }
        else
        {
            rightHandFound = false;
            if (Time.frameCount % 120 == 0) // Log every 2 seconds
            {
                Debug.LogWarning("[UnitPlacer] No right hand controller found!");
            }
        }
    }

    #endregion

    #region Controller Setup

    private void FindRightController()
    {
        Debug.Log("[UnitPlacer] Searching for right controller transform...");

        string[] possibleNames = new string[]
        {
            "RightHand",
            "RightHandAnchor",
            "Right Hand Anchor",
            "RightHand Controller",
            "RightController",
            "Right_Hand"
        };

        foreach (string name in possibleNames)
        {
            GameObject found = GameObject.Find(name);
            if (found != null)
            {
                rightControllerTransform = found.transform;
                Debug.Log($"[UnitPlacer] ✓ Found controller transform: {name}");
                return;
            }
        }

        Debug.LogError("[UnitPlacer] ✗ Could not auto-find right controller transform!");
    }

    #endregion

    #region Placement Control

    public void StartPlacement(GameObject prefab, UnitType unitType, int width, int height)
    {
        currentPrefab = prefab;
        currentUnitType = unitType;
        currentWidth = width;
        currentHeight = height;
        currentRotation = RotationAngle.Rotate0;
        isPlacementMode = true;

        CreatePreview();

        Debug.Log($"[UnitPlacer] ✓ Started placing {unitType} ({width}x{height})");
    }

    public void CancelPlacement()
    {
        isPlacementMode = false;
        currentPrefab = null;
        currentUnitType = UnitType.Empty;

        if (previewObject != null)
        {
            Destroy(previewObject);
            previewObject = null;
        }

        if (tacticalGrid != null)
            tacticalGrid.ClearAllHighlights();

        Debug.Log("[UnitPlacer] Cancelled placement");
    }

    public bool IsInPlacementMode()
    {
        return isPlacementMode;
    }

    #endregion

    #region Raycast

    private void PerformRaycast()
    {
        if (rightControllerTransform == null)
        {
            if (Time.frameCount % 120 == 0)
            {
                Debug.LogWarning("[UnitPlacer] Right controller transform is NULL!");
                if (autoFindRightController)
                    FindRightController();
            }
            currentHoverCell = null;
            return;
        }

        if (tacticalGrid == null)
        {
            if (Time.frameCount % 120 == 0)
                Debug.LogWarning("[UnitPlacer] TacticalGrid is NULL!");
            currentHoverCell = null;
            return;
        }

        Vector3 rayOrigin = rightControllerTransform.position;
        Vector3 rayDirection = rightControllerTransform.forward;

        Ray ray = new Ray(rayOrigin, rayDirection);
        RaycastHit hit;

        Debug.DrawRay(rayOrigin, rayDirection * maxRayDistance, Color.yellow);

        if (Physics.Raycast(ray, out hit, maxRayDistance, groundLayerMask))
        {
            GridCell cell = tacticalGrid.GetCellAtWorldPosition(hit.point);
            UpdateHoverCell(cell);
        }
        else
        {
            UpdateHoverCell(null);
        }
    }

    private void UpdateHoverCell(GridCell cell)
    {
        currentHoverCell = cell;

        if (isPlacementMode && currentHoverCell != null)
        {
            isValidPlacement = tacticalGrid.ValidatePlacement(
                currentHoverCell.gridX,
                currentHoverCell.gridZ,
                currentWidth,
                currentHeight,
                playerTeam,
                currentUnitType,
                currentRotation
            );

            List<GridCell> cells = tacticalGrid.GetCellsForPlacement(
                currentHoverCell.gridX,
                currentHoverCell.gridZ,
                currentWidth,
                currentHeight,
                currentRotation
            );

            tacticalGrid.HighlightCells(cells, isValidPlacement);
        }
        else
        {
            if (tacticalGrid != null)
                tacticalGrid.ClearAllHighlights();
        }
    }

    #endregion

    #region Input Handling - Unity XR

    private void HandleRotationInput()
    {
        if (!rightHandFound) return;

        // Get grip button press
        if (rightHandDevice.TryGetFeatureValue(CommonUsages.gripButton, out bool gripPressed))
        {
            // Check for button down (wasn't pressed last frame, is pressed this frame)
            rightHandDevice.TryGetFeatureValue(CommonUsages.grip, out float gripValue);

            if (gripPressed && gripValue > 0.9f)
            {
                RotatePreview();
                Debug.Log("[UnitPlacer] GRIP PRESSED - Rotating!");
            }
        }
    }

    private void HandlePlacementInput()
    {
        if (!rightHandFound) return;

        // Get trigger button press
        if (rightHandDevice.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerPressed))
        {
            rightHandDevice.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue);

            if (triggerPressed && triggerValue > 0.9f)
            {
                Debug.Log("[UnitPlacer] TRIGGER PRESSED - Attempting placement!");
                TryPlaceUnit();

                // Small delay to prevent double-placement
                System.Threading.Thread.Sleep(200);
            }
        }
    }

    #endregion

    #region Rotation

    private void RotatePreview()
    {
        switch (currentRotation)
        {
            case RotationAngle.Rotate0:
                currentRotation = RotationAngle.Rotate90;
                break;
            case RotationAngle.Rotate90:
                currentRotation = RotationAngle.Rotate180;
                break;
            case RotationAngle.Rotate180:
                currentRotation = RotationAngle.Rotate270;
                break;
            case RotationAngle.Rotate270:
                currentRotation = RotationAngle.Rotate0;
                break;
        }

        Debug.Log($"[UnitPlacer] ✓ Rotated to {currentRotation}");

        if (previewObject != null)
        {
            previewObject.transform.rotation = Quaternion.Euler(0f, (float)currentRotation, 0f);
        }

        UpdateHoverCell(currentHoverCell);
    }

    #endregion

    #region Placement

    private void TryPlaceUnit()
    {
        if (!isPlacementMode)
        {
            Debug.LogWarning("[UnitPlacer] ✗ Not in placement mode!");
            return;
        }

        if (currentHoverCell == null)
        {
            Debug.LogWarning("[UnitPlacer] ✗ No cell hovered!");
            return;
        }

        if (!isValidPlacement)
        {
            Debug.LogWarning($"[UnitPlacer] ✗ Invalid placement at ({currentHoverCell.gridX}, {currentHoverCell.gridZ})!");
            Debug.LogWarning($"   Cell team: {currentHoverCell.allowedTeam}, Player team: {playerTeam}");
            Debug.LogWarning($"   Cell occupied: {currentHoverCell.IsOccupied()}");
            return;
        }

        Vector3 spawnPos = GetPlacementWorldPosition();
        Quaternion spawnRot = Quaternion.Euler(0f, (float)currentRotation, 0f);

        GameObject placedUnit = Instantiate(currentPrefab, spawnPos, spawnRot);
        placedUnit.name = $"{currentUnitType}_{currentHoverCell.gridX}_{currentHoverCell.gridZ}";

        bool success = tacticalGrid.PlaceUnit(
            currentHoverCell.gridX,
            currentHoverCell.gridZ,
            currentWidth,
            currentHeight,
            playerTeam,
            currentUnitType,
            placedUnit,
            currentRotation
        );

        if (success)
        {
            Debug.Log($"[UnitPlacer] ✓ PLACED {currentUnitType} at ({currentHoverCell.gridX}, {currentHoverCell.gridZ})");
        }
        else
        {
            Debug.LogError("[UnitPlacer] ✗ Failed to register unit with grid!");
            Destroy(placedUnit);
        }
    }
    private Vector3 GetPlacementWorldPosition()
    {
        if (currentHoverCell == null) return Vector3.zero;

        int rotatedWidth = currentWidth;
        int rotatedHeight = currentHeight;

        if (currentRotation == RotationAngle.Rotate90 || currentRotation == RotationAngle.Rotate270)
        {
            rotatedWidth = currentHeight;
            rotatedHeight = currentWidth;
        }

        float offsetX = (rotatedWidth - 1) * tacticalGrid.CellWidth / 2f;
        float offsetZ = (rotatedHeight - 1) * tacticalGrid.CellHeight / 2f;

        Vector3 basePosition = currentHoverCell.worldPosition + new Vector3(offsetX, 0f, offsetZ);

        // FIXED: Calculate Y offset from preview object if available
        float yOffset = 0f;

        GameObject objectToMeasure = previewObject != null ? previewObject : currentPrefab;

        if (objectToMeasure != null)
        {
            // Get all renderers to calculate combined bounds
            Renderer[] renderers = objectToMeasure.GetComponentsInChildren<Renderer>();

            if (renderers.Length > 0)
            {
                // Calculate combined bounds
                Bounds combinedBounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    combinedBounds.Encapsulate(renderers[i].bounds);
                }

                // Offset by the minimum Y to place bottom at grid level
                float minY = combinedBounds.min.y - objectToMeasure.transform.position.y;
                yOffset = -minY; // Negate to move up
            }
        }

        Debug.Log($"[UnitPlacer] Y Offset: {yOffset:F2}");
        return basePosition + new Vector3(0f, yOffset, 0f);
    }

    #endregion

    #region Preview

    private void CreatePreview()
    {
        if (currentPrefab == null || !showPreview) return;

        if (previewObject != null)
            Destroy(previewObject);

        previewObject = Instantiate(currentPrefab);
        previewObject.name = "PlacementPreview";

        MakeTransparent(previewObject);
        DisableComponents(previewObject);

        Debug.Log("[UnitPlacer] Created placement preview");
    }

    private void UpdatePreview()
    {
        if (previewObject == null || currentHoverCell == null)
        {
            if (previewObject != null)
                previewObject.SetActive(false);
            return;
        }

        previewObject.SetActive(true);

        Vector3 previewPos = GetPlacementWorldPosition();
        previewObject.transform.position = previewPos;
        previewObject.transform.rotation = Quaternion.Euler(0f, (float)currentRotation, 0f);

        UpdatePreviewColor(isValidPlacement);
    }

    private void MakeTransparent(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            foreach (Material mat in r.materials)
            {
                mat.SetFloat("_Mode", 3);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.renderQueue = 3000;

                Color c = mat.color;
                c.a = previewAlpha;
                mat.color = c;
            }
        }
    }

    private void UpdatePreviewColor(bool valid)
    {
        if (previewObject == null) return;

        Color targetColor = valid ? validRayColor : invalidRayColor;

        Renderer[] renderers = previewObject.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            foreach (Material mat in r.materials)
            {
                Color c = targetColor;
                c.a = previewAlpha;
                mat.color = c;
            }
        }
    }

    private void DisableComponents(GameObject obj)
    {
        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
            col.enabled = false;

        Rigidbody[] rbs = obj.GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in rbs)
            rb.isKinematic = true;

        MonoBehaviour[] scripts = obj.GetComponentsInChildren<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts)
        {
            if (script != null && script != this)
                script.enabled = false;
        }
    }

    #endregion

    #region Ray Visual

    private void SetupRayLine()
    {
        if (rightControllerTransform == null)
        {
            Debug.LogWarning("[UnitPlacer] Cannot setup ray - no controller yet");
            return;
        }

        GameObject rayObj = new GameObject("RayVisual");
        rayObj.transform.position = Vector3.zero;

        rayLine = rayObj.AddComponent<LineRenderer>();
        rayLine.startWidth = 0.02f;
        rayLine.endWidth = 0.02f;
        rayLine.positionCount = 2;
        rayLine.material = new Material(Shader.Find("Sprites/Default"));
        rayLine.startColor = normalRayColor;
        rayLine.endColor = normalRayColor;
        rayLine.useWorldSpace = true;

        Debug.Log("[UnitPlacer] ✓ Ray line created");
    }

    private void UpdateRayVisual()
    {
        if (!showRay)
        {
            if (rayLine != null)
                rayLine.enabled = false;
            return;
        }

        if (rayLine == null || rightControllerTransform == null)
        {
            if (showRay && rayLine == null && rightControllerTransform != null)
            {
                SetupRayLine();
            }
            return;
        }

        rayLine.enabled = true;

        Vector3 rayStart = rightControllerTransform.position;
        Vector3 rayEnd;

        if (currentHoverCell != null)
        {
            rayEnd = currentHoverCell.worldPosition;
            Color color = isPlacementMode ? (isValidPlacement ? validRayColor : invalidRayColor) : normalRayColor;
            rayLine.startColor = color;
            rayLine.endColor = color;
        }
        else
        {
            rayEnd = rayStart + rightControllerTransform.forward * maxRayDistance;
            rayLine.startColor = normalRayColor;
            rayLine.endColor = normalRayColor;
        }

        rayLine.SetPosition(0, rayStart);
        rayLine.SetPosition(1, rayEnd);
    }

    #endregion
}