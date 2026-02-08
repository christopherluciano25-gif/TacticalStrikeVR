using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// VR placement system for Unity XR
/// Works with Meta XR SDK
/// Right controller raycast for placement
/// Grip button: rotate wall/tower
/// Trigger: place unit
/// </summary>
public class UnitPlacer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TacticalGrid tacticalGrid;
    [SerializeField] private Transform rightControllerTransform; // Drag RightInteractors here
    [SerializeField] private LayerMask groundLayerMask;

    [Header("Raycast Settings")]
    [SerializeField] private float maxRayDistance = 30f;

    [Header("Visual Feedback")]
    [SerializeField] private bool showRay = true;
    [SerializeField] private LineRenderer rayLine;
    [SerializeField] private Color validRayColor = Color.green;
    [SerializeField] private Color invalidRayColor = Color.red;
    [SerializeField] private Color normalRayColor = Color.cyan;

    [Header("Preview Settings")]
    [SerializeField] private bool showPreview = true;
    [SerializeField] private float previewAlpha = 0.5f;

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

    // Input tracking
    private bool previousGripState = false;
    private bool previousTriggerState = false;

    private void Awake()
    {
        if (tacticalGrid == null)
            tacticalGrid = FindObjectOfType<TacticalGrid>();

        if (rayLine == null && showRay)
            SetupRayLine();
    }

    private void Update()
    {
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
    }

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

        Debug.Log($"[UnitPlacer] Started placing {unitType} ({width}x{height})");
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
            Debug.LogWarning("[UnitPlacer] Right controller transform is NULL!");
            currentHoverCell = null;
            return;
        }

        if (tacticalGrid == null)
        {
            Debug.LogWarning("[UnitPlacer] TacticalGrid is NULL!");
            currentHoverCell = null;
            return;
        }

        Ray ray = new Ray(rightControllerTransform.position, rightControllerTransform.forward);
        RaycastHit hit;

        // Draw debug ray in Scene view
        Debug.DrawRay(ray.origin, ray.direction * maxRayDistance, Color.yellow);

        // Debug log once per second
        if (Time.frameCount % 60 == 0)
        {
            Debug.Log($"[UnitPlacer] Ray origin: {ray.origin}, direction: {ray.direction}");
            Debug.Log($"[UnitPlacer] Ground layer mask: {groundLayerMask.value}");
        }

        if (Physics.Raycast(ray, out hit, maxRayDistance, groundLayerMask))
        {
            Debug.Log($"[UnitPlacer] HIT: {hit.collider.gameObject.name} on layer {hit.collider.gameObject.layer}");
            GridCell cell = tacticalGrid.GetCellAtWorldPosition(hit.point);

            if (cell != null)
            {
                Debug.Log($"[UnitPlacer] Found cell at ({cell.gridX}, {cell.gridZ})");
            }
            else
            {
                Debug.LogWarning($"[UnitPlacer] Hit point {hit.point} didn't match any grid cell!");
            }

            UpdateHoverCell(cell);
        }
        else
        {
            // Only log occasionally to avoid spam
            if (Time.frameCount % 120 == 0)
            {
                Debug.LogWarning("[UnitPlacer] Raycast missed - check BattleArea has collider and correct layer!");
            }
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

    #region Input Handling

    private void HandleRotationInput()
    {
        // Try Meta XR input first
        bool gripPressed = OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch);

        if (gripPressed && !previousGripState)
        {
            RotatePreview();
        }

        previousGripState = gripPressed;
    }

    private void HandlePlacementInput()
    {
        // Try Meta XR input first
        bool triggerPressed = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch);

        if (triggerPressed && !previousTriggerState)
        {
            TryPlaceUnit();
        }

        previousTriggerState = triggerPressed;
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

        Debug.Log($"[UnitPlacer] Rotated to {currentRotation}");

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
        if (!isPlacementMode || currentHoverCell == null || !isValidPlacement)
        {
            Debug.LogWarning("[UnitPlacer] Cannot place: invalid state");
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
            Debug.Log($"[UnitPlacer] Placed {currentUnitType} at ({currentHoverCell.gridX}, {currentHoverCell.gridZ})");
        }
        else
        {
            Destroy(placedUnit);
            Debug.LogError("[UnitPlacer] Failed to register unit with grid");
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

        return currentHoverCell.worldPosition + new Vector3(offsetX, 0f, offsetZ);
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
        if (rightControllerTransform == null) return;

        GameObject rayObj = new GameObject("RayVisual");
        rayObj.transform.SetParent(rightControllerTransform);
        rayObj.transform.localPosition = Vector3.zero;

        rayLine = rayObj.AddComponent<LineRenderer>();
        rayLine.startWidth = 0.01f;
        rayLine.endWidth = 0.01f;
        rayLine.positionCount = 2;
        rayLine.material = new Material(Shader.Find("Sprites/Default"));
        rayLine.startColor = normalRayColor;
        rayLine.endColor = normalRayColor;
    }

    private void UpdateRayVisual()
    {
        if (!showRay || rayLine == null || rightControllerTransform == null) return;

        rayLine.SetPosition(0, rightControllerTransform.position);

        if (currentHoverCell != null)
        {
            rayLine.SetPosition(1, currentHoverCell.worldPosition);

            Color color = isPlacementMode ? (isValidPlacement ? validRayColor : invalidRayColor) : normalRayColor;
            rayLine.startColor = color;
            rayLine.endColor = color;
        }
        else
        {
            rayLine.SetPosition(1, rightControllerTransform.position + rightControllerTransform.forward * maxRayDistance);
            rayLine.startColor = normalRayColor;
            rayLine.endColor = normalRayColor;
        }
    }

    #endregion
}