using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// VR interaction system for grid-based placement
/// Handles raycast, snapping, and placement validation
/// </summary>
public class VRGridInteraction : UnityEngine.MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridSystem gridSystem;
    [SerializeField] private Transform rightController;
    [SerializeField] private Transform leftController;
    
    [Header("Raycast Settings")]
    [SerializeField] private float maxRaycastDistance = 20f;
    [SerializeField] private LayerMask gridLayerMask;
    [SerializeField] private bool useRightController = true;
    
    [Header("Visual Feedback")]
    [SerializeField] private bool showRayVisual = true;
    [SerializeField] private LineRenderer rayLineRenderer;
    [SerializeField] private Color validRayColor = Color.green;
    [SerializeField] private Color invalidRayColor = Color.red;
    [SerializeField] private Color normalRayColor = Color.blue;
    
    [Header("Placement Preview")]
    [SerializeField] private GameObject placementPreviewPrefab;
    [SerializeField] private bool snapToGrid = true;
    [SerializeField] private bool showPreview = true;
    
    // Current state
    private GridCell currentHoveredCell = null;
    private GameObject currentPreview = null;
    private bool isPlacementMode = false;
    private GameObject objectToPlace = null;
    
    // Multi-cell placement
    private int placementWidth = 1;
    private int placementHeight = 1;
    
    // Events
    public delegate void CellHoveredDelegate(GridCell cell);
    public event CellHoveredDelegate OnCellHovered;
    
    public delegate void PlacementAttemptDelegate(GridCell cell, bool success);
    public event PlacementAttemptDelegate OnPlacementAttempt;
    
    private void Awake()
    {
        // Auto-find grid system
        if (gridSystem == null)
        {
            gridSystem = UnityEngine.Object.FindObjectOfType<GridSystem>();
        }
        
        // Setup ray line renderer
        if (rayLineRenderer == null && showRayVisual)
        {
            SetupRayLineRenderer();
        }
    }
    
    private void Update()
    {
        PerformGridRaycast();
        UpdatePlacementPreview();
        UpdateRayVisual();
    }
    
    #region Raycast System
    
    /// <summary>
    /// Perform raycast from active controller to grid
    /// </summary>
    private void PerformGridRaycast()
    {
        Transform activeController = useRightController ? rightController : leftController;
        
        if (activeController == null || gridSystem == null)
        {
            SetHoveredCell(null);
            return;
        }
        
        // Cast ray from controller
        Ray ray = new Ray(activeController.position, activeController.forward);
        RaycastHit hit;
        
        bool didHit = Physics.Raycast(ray, out hit, maxRaycastDistance, gridLayerMask);
        
        if (didHit)
        {
            // Get cell at hit position
            GridCell cell = gridSystem.GetCellAtWorldPosition(hit.point);
            SetHoveredCell(cell);
        }
        else
        {
            // Check if ray intersects grid bounds (even without collider)
            if (RayIntersectsGridPlane(ray, out Vector3 intersectionPoint))
            {
                GridCell cell = gridSystem.GetCellAtWorldPosition(intersectionPoint);
                SetHoveredCell(cell);
            }
            else
            {
                SetHoveredCell(null);
            }
        }
    }
    
    /// <summary>
    /// Check if ray intersects the grid plane (Y = 0)
    /// </summary>
    private bool RayIntersectsGridPlane(Ray ray, out Vector3 intersectionPoint)
    {
        Plane gridPlane = new Plane(Vector3.up, gridSystem.GridOrigin);
        float enter;
        
        if (gridPlane.Raycast(ray, out enter))
        {
            intersectionPoint = ray.GetPoint(enter);
            
            // Check if point is within grid bounds
            return gridSystem.GridBounds.Contains(intersectionPoint);
        }
        
        intersectionPoint = Vector3.zero;
        return false;
    }
    
    /// <summary>
    /// Set the currently hovered cell
    /// </summary>
    private void SetHoveredCell(GridCell cell)
    {
        if (currentHoveredCell != cell)
        {
            currentHoveredCell = cell;
            
            // Update grid system highlighting
            if (gridSystem != null)
            {
                gridSystem.SetHoveredCell(cell);
            }
            
            // Invoke event
            OnCellHovered?.Invoke(cell);
        }
    }
    
    #endregion
    
    #region Placement System
    
    /// <summary>
    /// Start placement mode with a specific object
    /// </summary>
    public void StartPlacement(GameObject prefabToPlace, int width = 1, int height = 1)
    {
        objectToPlace = prefabToPlace;
        placementWidth = width;
        placementHeight = height;
        isPlacementMode = true;
        
        // Create preview if enabled
        if (showPreview)
        {
            CreatePreview();
        }
        
        Debug.Log($"[VRGridInteraction] Started placement mode: {prefabToPlace.name} ({width}x{height})");
    }
    
    /// <summary>
    /// Attempt to place object at current hovered cell
    /// </summary>
    public bool TryPlaceObject()
    {
        if (!isPlacementMode || currentHoveredCell == null || objectToPlace == null)
        {
            Debug.LogWarning("[VRGridInteraction] Cannot place: Invalid state");
            return false;
        }
        
        // Check if placement is valid
        if (!IsPlacementValid(currentHoveredCell, placementWidth, placementHeight))
        {
            Debug.LogWarning("[VRGridInteraction] Cannot place: Invalid position");
            OnPlacementAttempt?.Invoke(currentHoveredCell, false);
            return false;
        }
        
        // Get placement position (snapped to grid)
        Vector3 placementPosition = GetPlacementPosition(currentHoveredCell);
        
        // Instantiate object
        GameObject placedObject = Instantiate(objectToPlace, placementPosition, Quaternion.identity);
        placedObject.name = objectToPlace.name + "_Placed";
        
        // Mark cells as occupied
        MarkCellsAsOccupied(currentHoveredCell, placementWidth, placementHeight, placedObject);
        
        Debug.Log($"[VRGridInteraction] Placed {objectToPlace.name} at grid ({currentHoveredCell.gridX}, {currentHoveredCell.gridZ})");
        
        OnPlacementAttempt?.Invoke(currentHoveredCell, true);
        return true;
    }
    
    /// <summary>
    /// Cancel placement mode
    /// </summary>
    public void CancelPlacement()
    {
        isPlacementMode = false;
        objectToPlace = null;
        
        if (currentPreview != null)
        {
            Destroy(currentPreview);
            currentPreview = null;
        }
        
        if (gridSystem != null)
        {
            gridSystem.ClearAllHighlights();
        }
        
        Debug.Log("[VRGridInteraction] Cancelled placement mode");
    }
    
    /// <summary>
    /// Check if placement is valid at the given cell
    /// </summary>
    public bool IsPlacementValid(GridCell startCell, int width, int height)
    {
        if (startCell == null || gridSystem == null) return false;
        
        // Get all cells that would be occupied
        var cellsToOccupy = gridSystem.GetCellsInArea(
            startCell.gridX,
            startCell.gridZ,
            width,
            height
        );
        
        // Check if we got the expected number of cells (ensures they're all within bounds)
        if (cellsToOccupy.Count != width * height)
        {
            return false;
        }
        
        // Check if all cells are available
        foreach (GridCell cell in cellsToOccupy)
        {
            if (!cell.IsAvailable())
            {
                return false;
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// Get the world position for placement (snapped to grid)
    /// </summary>
    private Vector3 GetPlacementPosition(GridCell startCell)
    {
        if (!snapToGrid)
        {
            return startCell.worldPosition;
        }
        
        // Calculate center position for multi-cell objects
        float offsetX = (placementWidth - 1) * gridSystem.CellWidth / 2f;
        float offsetZ = (placementHeight - 1) * gridSystem.CellHeight / 2f;
        
        return startCell.worldPosition + new Vector3(offsetX, 0f, offsetZ);
    }
    
    /// <summary>
    /// Mark cells as occupied by a placed object
    /// </summary>
    private void MarkCellsAsOccupied(GridCell startCell, int width, int height, GameObject placedObject)
    {
        var cellsToOccupy = gridSystem.GetCellsInArea(
            startCell.gridX,
            startCell.gridZ,
            width,
            height
        );
        
        bool isMultiCell = (width > 1 || height > 1);
        
        foreach (GridCell cell in cellsToOccupy)
        {
            cell.Occupy(placedObject, isMultiCell, placedObject);
        }
    }
    
    #endregion
    
    #region Preview System
    
    /// <summary>
    /// Create placement preview object
    /// </summary>
    private void CreatePreview()
    {
        if (objectToPlace == null) return;
        
        // Destroy existing preview
        if (currentPreview != null)
        {
            Destroy(currentPreview);
        }
        
        // Create new preview
        currentPreview = Instantiate(objectToPlace);
        currentPreview.name = "PlacementPreview";
        
        // Make it transparent
        MakePreviewTransparent(currentPreview);
        
        // Disable colliders and scripts
        DisablePreviewComponents(currentPreview);
    }
    
    /// <summary>
    /// Update placement preview position
    /// </summary>
    private void UpdatePlacementPreview()
    {
        if (!isPlacementMode || currentPreview == null || currentHoveredCell == null)
        {
            if (currentPreview != null)
            {
                currentPreview.SetActive(false);
            }
            return;
        }
        
        currentPreview.SetActive(true);
        
        // Position preview at placement position
        Vector3 previewPosition = GetPlacementPosition(currentHoveredCell);
        currentPreview.transform.position = previewPosition;
        
        // Update preview color based on validity
        bool isValid = IsPlacementValid(currentHoveredCell, placementWidth, placementHeight);
        UpdatePreviewColor(currentPreview, isValid);
        
        // Highlight affected cells
        HighlightPlacementCells(currentHoveredCell, placementWidth, placementHeight, isValid);
    }
    
    /// <summary>
    /// Make preview transparent
    /// </summary>
    private void MakePreviewTransparent(GameObject preview)
    {
        Renderer[] renderers = preview.GetComponentsInChildren<Renderer>();
        
        foreach (Renderer renderer in renderers)
        {
            foreach (Material mat in renderer.materials)
            {
                // Set to transparent mode
                mat.SetFloat("_Mode", 3);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
                
                Color color = mat.color;
                color.a = 0.5f;
                mat.color = color;
            }
        }
    }
    
    /// <summary>
    /// Update preview color based on placement validity
    /// </summary>
    private void UpdatePreviewColor(GameObject preview, bool isValid)
    {
        Color targetColor = isValid ? validRayColor : invalidRayColor;
        
        Renderer[] renderers = preview.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            foreach (Material mat in renderer.materials)
            {
                Color color = targetColor;
                color.a = 0.5f;
                mat.color = color;
            }
        }
    }
    
    /// <summary>
    /// Disable preview components
    /// </summary>
    private void DisablePreviewComponents(GameObject preview)
    {
        // Disable colliders
        Collider[] colliders = preview.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }
        
        // Disable rigidbodies
        Rigidbody[] rigidbodies = preview.GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in rigidbodies)
        {
            rb.isKinematic = true;
        }
        
        // Disable custom scripts (but not this one)
        MonoBehaviour[] scripts = preview.GetComponentsInChildren<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts)
        {
            if (script != null && script != this)
            {
                script.enabled = false;
            }
        }
    }
    
    /// <summary>
    /// Highlight cells that would be occupied by placement
    /// </summary>
    private void HighlightPlacementCells(GridCell startCell, int width, int height, bool isValid)
    {
        if (gridSystem == null) return;
        
        // Clear previous highlights
        gridSystem.ClearAllHighlights();
        
        // Get cells to highlight
        var cellsToHighlight = gridSystem.GetCellsInArea(
            startCell.gridX,
            startCell.gridZ,
            width,
            height
        );
        
        // Highlight them
        gridSystem.HighlightCells(cellsToHighlight, true);
    }
    
    #endregion
    
    #region Ray Visual
    
    /// <summary>
    /// Setup ray line renderer
    /// </summary>
    private void SetupRayLineRenderer()
    {
        Transform activeController = useRightController ? rightController : leftController;
        
        if (activeController == null) return;
        
        GameObject rayObj = new GameObject("RayVisual");
        rayObj.transform.SetParent(activeController);
        rayObj.transform.localPosition = Vector3.zero;
        rayObj.transform.localRotation = Quaternion.identity;
        
        rayLineRenderer = rayObj.AddComponent<LineRenderer>();
        rayLineRenderer.startWidth = 0.01f;
        rayLineRenderer.endWidth = 0.01f;
        rayLineRenderer.positionCount = 2;
        rayLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        rayLineRenderer.startColor = normalRayColor;
        rayLineRenderer.endColor = normalRayColor;
    }
    
    /// <summary>
    /// Update ray visual
    /// </summary>
    private void UpdateRayVisual()
    {
        if (!showRayVisual || rayLineRenderer == null) return;
        
        Transform activeController = useRightController ? rightController : leftController;
        if (activeController == null) return;
        
        rayLineRenderer.SetPosition(0, activeController.position);
        
        if (currentHoveredCell != null)
        {
            rayLineRenderer.SetPosition(1, currentHoveredCell.worldPosition);
            
            // Set color based on validity
            if (isPlacementMode)
            {
                bool isValid = IsPlacementValid(currentHoveredCell, placementWidth, placementHeight);
                Color color = isValid ? validRayColor : invalidRayColor;
                rayLineRenderer.startColor = color;
                rayLineRenderer.endColor = color;
            }
            else
            {
                rayLineRenderer.startColor = normalRayColor;
                rayLineRenderer.endColor = normalRayColor;
            }
        }
        else
        {
            rayLineRenderer.SetPosition(1, activeController.position + activeController.forward * maxRaycastDistance);
            rayLineRenderer.startColor = normalRayColor;
            rayLineRenderer.endColor = normalRayColor;
        }
    }
    
    /// <summary>
    /// Toggle which controller to use
    /// </summary>
    public void SetActiveController(bool useRight)
    {
        useRightController = useRight;
    }
    
    #endregion
    
    #region Public Utilities
    
    /// <summary>
    /// Get currently hovered cell
    /// </summary>
    public GridCell GetHoveredCell()
    {
        return currentHoveredCell;
    }
    
    /// <summary>
    /// Check if currently in placement mode
    /// </summary>
    public bool IsInPlacementMode()
    {
        return isPlacementMode;
    }
    
    #endregion
}