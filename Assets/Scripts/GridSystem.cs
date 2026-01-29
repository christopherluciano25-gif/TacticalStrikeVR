using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Main grid system for VR strategy game
/// Manages grid creation, visualization, and cell tracking
/// Supports both fixed and area-based grid modes
/// </summary>
public class GridSystem : UnityEngine.MonoBehaviour
{
    #region Inspector Settings
    
    [Header("Grid Mode")]
    [SerializeField] private GridMode gridMode = GridMode.Fixed;
    
    [Header("Fixed Grid Settings")]
    [SerializeField] private int gridRows = 10;
    [SerializeField] private int gridColumns = 10;
    
    [Header("Area-Based Grid Settings")]
    [SerializeField] private Vector3 areaStart = new Vector3(-5f, 0f, -5f);
    [SerializeField] private Vector3 areaEnd = new Vector3(5f, 0f, 5f);
    [SerializeField] private bool lockYAxis = true;
    
    [Header("Cell Settings")]
    [SerializeField] private float cellWidth = 1f;
    [SerializeField] private float cellHeight = 1f;
    [SerializeField] private float gridHeightOffset = 0.01f;
    
    [Header("Grid Origin")]
    [SerializeField] private Vector3 gridOrigin = Vector3.zero;
    [SerializeField] private bool centerGrid = true;
    
    [Header("Visual Settings")]
    [SerializeField] private bool showGridLines = true;
    [SerializeField] private Color gridLineColor = new Color(1f, 1f, 1f, 0.3f);
    [SerializeField] private float gridLineThickness = 0.02f;
    [SerializeField] private Material gridLineMaterial;
    
    [Header("Cell Highlight Colors")]
    [SerializeField] private Color availableColor = new Color(0f, 1f, 0f, 0.3f);
    [SerializeField] private Color occupiedColor = new Color(1f, 0f, 0f, 0.3f);
    [SerializeField] private Color restrictedColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
    [SerializeField] private Color hoveredColor = new Color(0f, 0.5f, 1f, 0.5f);
    
    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = true;
    [SerializeField] private bool showCellCoordinates = false;
    
    #endregion
    
    #region Private Variables
    
    // Grid data
    private GridCell[,] cells;
    private int actualRows;
    private int actualColumns;
    private Vector3 actualGridOrigin;
    
    // Visual components
    private GameObject gridLinesParent;
    private List<GameObject> gridLineObjects = new List<GameObject>();
    private GameObject cellHighlightsParent;
    private Dictionary<GridCell, GameObject> cellHighlights = new Dictionary<GridCell, GameObject>();
    
    // Currently hovered cell
    private GridCell hoveredCell = null;
    
    // Grid bounds
    private Bounds gridBounds;
    
    #endregion
    
    #region Properties
    
    public int Rows => actualRows;
    public int Columns => actualColumns;
    public float CellWidth => cellWidth;
    public float CellHeight => cellHeight;
    public Vector3 GridOrigin => actualGridOrigin;
    public GridCell[,] Cells => cells;
    public Bounds GridBounds => gridBounds;
    
    #endregion
    
    #region Unity Lifecycle
    
    private void Awake()
    {
        InitializeGrid();
    }
    
    private void OnValidate()
    {
        // Ensure valid values
        gridRows = Mathf.Max(1, gridRows);
        gridColumns = Mathf.Max(1, gridColumns);
        cellWidth = Mathf.Max(0.1f, cellWidth);
        cellHeight = Mathf.Max(0.1f, cellHeight);
        gridLineThickness = Mathf.Max(0.001f, gridLineThickness);
    }
    
    #endregion
    
    #region Grid Initialization
    
    /// <summary>
    /// Initialize the grid system
    /// </summary>
    public void InitializeGrid()
    {
        // Clear existing grid
        ClearGrid();
        
        // Calculate grid dimensions based on mode
        CalculateGridDimensions();
        
        // Create grid cells
        CreateCells();
        
        // Create visual representation
        if (showGridLines)
        {
            CreateGridLines();
        }
        
        // Create cell highlight system
        CreateCellHighlightSystem();
        
        Debug.Log($"[GridSystem] Initialized {actualRows}x{actualColumns} grid at {actualGridOrigin}");
    }
    
    /// <summary>
    /// Calculate grid dimensions based on current mode
    /// </summary>
    private void CalculateGridDimensions()
    {
        if (gridMode == GridMode.Fixed)
        {
            // Use predefined dimensions
            actualRows = gridRows;
            actualColumns = gridColumns;
            
            // Calculate origin
            if (centerGrid)
            {
                float totalWidth = actualColumns * cellWidth;
                float totalDepth = actualRows * cellHeight;
                actualGridOrigin = gridOrigin - new Vector3(totalWidth / 2f, 0f, totalDepth / 2f);
            }
            else
            {
                actualGridOrigin = gridOrigin;
            }
        }
        else // AreaBased
        {
            // Calculate dimensions from area
            Vector3 areaDiff = areaEnd - areaStart;
            
            if (lockYAxis)
            {
                areaDiff.y = 0f;
            }
            
            actualColumns = Mathf.Max(1, Mathf.FloorToInt(Mathf.Abs(areaDiff.x) / cellWidth));
            actualRows = Mathf.Max(1, Mathf.FloorToInt(Mathf.Abs(areaDiff.z) / cellHeight));
            
            // Use area start as origin
            actualGridOrigin = new Vector3(
                Mathf.Min(areaStart.x, areaEnd.x),
                gridOrigin.y,
                Mathf.Min(areaStart.z, areaEnd.z)
            );
        }
        
        // Calculate grid bounds
        gridBounds = new Bounds(
            actualGridOrigin + new Vector3(actualColumns * cellWidth / 2f, 0f, actualRows * cellHeight / 2f),
            new Vector3(actualColumns * cellWidth, 0.1f, actualRows * cellHeight)
        );
    }
    
    /// <summary>
    /// Create all grid cells
    /// </summary>
    private void CreateCells()
    {
        cells = new GridCell[actualRows, actualColumns];
        
        for (int row = 0; row < actualRows; row++)
        {
            for (int col = 0; col < actualColumns; col++)
            {
                // Calculate world position (center of cell)
                Vector3 worldPos = GetWorldPositionFromGridCoords(col, row);
                worldPos.y += gridHeightOffset;
                
                // Create cell
                cells[row, col] = new GridCell(col, row, worldPos);
            }
        }
    }
    
    #endregion
    
    #region Grid Visualization
    
    /// <summary>
    /// Create visual grid lines
    /// </summary>
    private void CreateGridLines()
    {
        // Create parent object
        gridLinesParent = new GameObject("GridLines");
        gridLinesParent.transform.SetParent(transform);
        gridLinesParent.transform.localPosition = Vector3.zero;
        
        // Create material if not assigned
        if (gridLineMaterial == null)
        {
            gridLineMaterial = new Material(Shader.Find("Sprites/Default"));
            gridLineMaterial.color = gridLineColor;
        }
        
        // Create horizontal lines (along Z-axis)
        for (int row = 0; row <= actualRows; row++)
        {
            Vector3 startPos = actualGridOrigin + new Vector3(0f, 0f, row * cellHeight);
            Vector3 endPos = actualGridOrigin + new Vector3(actualColumns * cellWidth, 0f, row * cellHeight);
            CreateGridLine(startPos, endPos, "HorizontalLine_" + row);
        }
        
        // Create vertical lines (along X-axis)
        for (int col = 0; col <= actualColumns; col++)
        {
            Vector3 startPos = actualGridOrigin + new Vector3(col * cellWidth, 0f, 0f);
            Vector3 endPos = actualGridOrigin + new Vector3(col * cellWidth, 0f, actualRows * cellHeight);
            CreateGridLine(startPos, endPos, "VerticalLine_" + col);
        }
    }
    
    /// <summary>
    /// Create a single grid line using LineRenderer
    /// </summary>
    private void CreateGridLine(Vector3 start, Vector3 end, string lineName)
    {
        GameObject lineObj = new GameObject(lineName);
        lineObj.transform.SetParent(gridLinesParent.transform);
        
        LineRenderer lineRenderer = lineObj.AddComponent<LineRenderer>();
        lineRenderer.material = gridLineMaterial;
        lineRenderer.startColor = gridLineColor;
        lineRenderer.endColor = gridLineColor;
        lineRenderer.startWidth = gridLineThickness;
        lineRenderer.endWidth = gridLineThickness;
        lineRenderer.positionCount = 2;
        lineRenderer.useWorldSpace = true;
        
        start.y += gridHeightOffset;
        end.y += gridHeightOffset;
        
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
        
        // Disable shadows
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        
        gridLineObjects.Add(lineObj);
    }
    
    /// <summary>
    /// Create cell highlight system (plane per cell)
    /// </summary>
    private void CreateCellHighlightSystem()
    {
        cellHighlightsParent = new GameObject("CellHighlights");
        cellHighlightsParent.transform.SetParent(transform);
        cellHighlightsParent.transform.localPosition = Vector3.zero;
        
        // Create a highlight quad for each cell (hidden by default)
        for (int row = 0; row < actualRows; row++)
        {
            for (int col = 0; col < actualColumns; col++)
            {
                GridCell cell = cells[row, col];
                GameObject highlight = CreateCellHighlight(cell);
                cellHighlights[cell] = highlight;
                highlight.SetActive(false); // Hidden by default
            }
        }
    }
    
    /// <summary>
    /// Create a highlight quad for a cell
    /// </summary>
    private GameObject CreateCellHighlight(GridCell cell)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = $"CellHighlight_{cell.gridX}_{cell.gridZ}";
        quad.transform.SetParent(cellHighlightsParent.transform);
        
        // Position and rotate to be flat on grid
        quad.transform.position = cell.worldPosition;
        quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        quad.transform.localScale = new Vector3(cellWidth * 0.95f, cellHeight * 0.95f, 1f);
        
        // Remove collider
        UnityEngine.Object.Destroy(quad.GetComponent<Collider>());
        
        // Create transparent material
        Renderer renderer = quad.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Standard"));
        mat.SetFloat("_Mode", 3); // Transparent
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        mat.color = availableColor;
        
        renderer.material = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        
        return quad;
    }
    
    /// <summary>
    /// Toggle grid line visibility
    /// </summary>
    public void SetGridLinesVisible(bool visible)
    {
        showGridLines = visible;
        if (gridLinesParent != null)
        {
            gridLinesParent.SetActive(visible);
        }
    }
    
    #endregion
    
    #region Coordinate Conversion
    
    /// <summary>
    /// Convert world position to grid coordinates
    /// </summary>
    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        Vector3 localPos = worldPosition - actualGridOrigin;
        
        int col = Mathf.FloorToInt(localPos.x / cellWidth);
        int row = Mathf.FloorToInt(localPos.z / cellHeight);
        
        return new Vector2Int(col, row);
    }
    
    /// <summary>
    /// Convert grid coordinates to world position (cell center)
    /// </summary>
    public Vector3 GridToWorld(int col, int row)
    {
        return GetWorldPositionFromGridCoords(col, row);
    }
    
    /// <summary>
    /// Get world position from grid coordinates (cell center)
    /// </summary>
    private Vector3 GetWorldPositionFromGridCoords(int col, int row)
    {
        return actualGridOrigin + new Vector3(
            col * cellWidth + cellWidth / 2f,
            0f,
            row * cellHeight + cellHeight / 2f
        );
    }
    
    /// <summary>
    /// Check if grid coordinates are valid
    /// </summary>
    public bool IsValidGridPosition(int col, int row)
    {
        return col >= 0 && col < actualColumns && row >= 0 && row < actualRows;
    }
    
    /// <summary>
    /// Check if grid coordinates are valid (Vector2Int)
    /// </summary>
    public bool IsValidGridPosition(Vector2Int gridPos)
    {
        return IsValidGridPosition(gridPos.x, gridPos.y);
    }
    
    #endregion
    
    #region Cell Access & Management
    
    /// <summary>
    /// Get cell at grid coordinates
    /// </summary>
    public GridCell GetCell(int col, int row)
    {
        if (IsValidGridPosition(col, row))
        {
            return cells[row, col];
        }
        return null;
    }
    
    /// <summary>
    /// Get cell at grid coordinates (Vector2Int)
    /// </summary>
    public GridCell GetCell(Vector2Int gridPos)
    {
        return GetCell(gridPos.x, gridPos.y);
    }
    
    /// <summary>
    /// Get cell at world position
    /// </summary>
    public GridCell GetCellAtWorldPosition(Vector3 worldPosition)
    {
        Vector2Int gridPos = WorldToGrid(worldPosition);
        return GetCell(gridPos);
    }
    
    /// <summary>
    /// Get nearest valid cell to world position with snapping
    /// </summary>
    public GridCell GetNearestCell(Vector3 worldPosition, out Vector3 snappedPosition)
    {
        Vector2Int gridPos = WorldToGrid(worldPosition);
        
        // Clamp to grid bounds
        gridPos.x = Mathf.Clamp(gridPos.x, 0, actualColumns - 1);
        gridPos.y = Mathf.Clamp(gridPos.y, 0, actualRows - 1);
        
        GridCell cell = GetCell(gridPos);
        snappedPosition = cell != null ? cell.worldPosition : worldPosition;
        
        return cell;
    }
    
    /// <summary>
    /// Get all cells in a rectangular area
    /// </summary>
    public List<GridCell> GetCellsInArea(int startCol, int startRow, int width, int height)
    {
        List<GridCell> cellsInArea = new List<GridCell>();
        
        for (int row = startRow; row < startRow + height; row++)
        {
            for (int col = startCol; col < startCol + width; col++)
            {
                GridCell cell = GetCell(col, row);
                if (cell != null)
                {
                    cellsInArea.Add(cell);
                }
            }
        }
        
        return cellsInArea;
    }
    
    #endregion
    
    #region Cell Highlighting
    
    /// <summary>
    /// Highlight a cell based on its state
    /// </summary>
    public void HighlightCell(GridCell cell, bool highlight = true)
    {
        if (cell == null || !cellHighlights.ContainsKey(cell)) return;
        
        GameObject highlightObj = cellHighlights[cell];
        highlightObj.SetActive(highlight);
        
        if (highlight)
        {
            // Set color based on cell state
            Color color = GetColorForCellState(cell.state);
            highlightObj.GetComponent<Renderer>().material.color = color;
        }
    }
    
    /// <summary>
    /// Highlight multiple cells
    /// </summary>
    public void HighlightCells(List<GridCell> cellsToHighlight, bool highlight = true)
    {
        foreach (GridCell cell in cellsToHighlight)
        {
            HighlightCell(cell, highlight);
        }
    }
    
    /// <summary>
    /// Set hovered cell (automatically updates highlighting)
    /// </summary>
    public void SetHoveredCell(GridCell cell)
    {
        // Clear previous hover
        if (hoveredCell != null && hoveredCell != cell)
        {
            HighlightCell(hoveredCell, false);
        }
        
        // Set new hover
        hoveredCell = cell;
        
        if (hoveredCell != null)
        {
            HighlightCell(hoveredCell, true);
            
            // Temporarily override color to hovered color
            if (cellHighlights.ContainsKey(hoveredCell))
            {
                cellHighlights[hoveredCell].GetComponent<Renderer>().material.color = hoveredColor;
            }
        }
    }
    
    /// <summary>
    /// Clear all highlights
    /// </summary>
    public void ClearAllHighlights()
    {
        foreach (var highlight in cellHighlights.Values)
        {
            highlight.SetActive(false);
        }
        hoveredCell = null;
    }
    
    /// <summary>
    /// Get color for cell state
    /// </summary>
    private Color GetColorForCellState(CellState state)
    {
        switch (state)
        {
            case CellState.Available:
                return availableColor;
            case CellState.Occupied:
                return occupiedColor;
            case CellState.Restricted:
                return restrictedColor;
            case CellState.Hovered:
                return hoveredColor;
            default:
                return availableColor;
        }
    }
    
    #endregion
    
    #region Utility Methods
    
    /// <summary>
    /// Clear the entire grid
    /// </summary>
    public void ClearGrid()
    {
        // Destroy visual elements
        if (gridLinesParent != null)
        {
            UnityEngine.Object.DestroyImmediate(gridLinesParent);
        }
        
        if (cellHighlightsParent != null)
        {
            UnityEngine.Object.DestroyImmediate(cellHighlightsParent);
        }
        
        gridLineObjects.Clear();
        cellHighlights.Clear();
        hoveredCell = null;
    }
    
    /// <summary>
    /// Rebuild the grid (useful for runtime changes)
    /// </summary>
    public void RebuildGrid()
    {
        InitializeGrid();
    }
    
    #endregion
    
    #region Debug Visualization
    
    private void OnDrawGizmos()
    {
        if (!showDebugInfo || cells == null) return;
        
        // Draw grid bounds
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(gridBounds.center, gridBounds.size);
        
        // Draw cell coordinates
        if (showCellCoordinates)
        {
            for (int row = 0; row < actualRows; row++)
            {
                for (int col = 0; col < actualColumns; col++)
                {
                    GridCell cell = cells[row, col];
                    if (cell != null)
                    {
                        // Draw coordinate text would go here (requires editor script)
                        Gizmos.color = Color.white;
                        Gizmos.DrawWireSphere(cell.worldPosition, 0.1f);
                    }
                }
            }
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (!showDebugInfo) return;
        
        // Draw area bounds for area-based mode
        if (gridMode == GridMode.AreaBased)
        {
            Gizmos.color = Color.cyan;
            Vector3 center = (areaStart + areaEnd) / 2f;
            Vector3 size = areaEnd - areaStart;
            Gizmos.DrawWireCube(center, size);
        }
    }
    
    #endregion
}