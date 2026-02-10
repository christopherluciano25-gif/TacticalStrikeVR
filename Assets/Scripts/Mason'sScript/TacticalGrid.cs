using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 19x9 tactical grid system with path validation for fair gameplay
/// Player zone: rows 0-3, No man's land: row 4, Bot zone: rows 5-8
/// </summary>
public class TacticalGrid : MonoBehaviour
{
    [Header("Grid Configuration")]
    [SerializeField] private int gridColumns = 19;
    [SerializeField] private int gridRows = 9;
    [SerializeField] private Transform groundPlane; // The plane to fit the grid to

    [Header("Visual Settings")]
    [SerializeField] private bool showGridLines = true;
    [SerializeField] private bool showZoneColors = true;
    [SerializeField] private Color playerZoneColor = new Color(0f, 0.5f, 1f, 0.2f); // Blue
    [SerializeField] private Color botZoneColor = new Color(1f, 0.5f, 0f, 0.2f); // Orange
    [SerializeField] private Color noMansLandColor = new Color(0.5f, 0.5f, 0.5f, 0.2f); // Gray
    [SerializeField] private Color gridLineColor = Color.white;
    [SerializeField] private float gridLineWidth = 0.02f;

    [Header("Highlight Colors")]
    [SerializeField] private Color validPlacementColor = new Color(0f, 1f, 0f, 0.5f);
    [SerializeField] private Color invalidPlacementColor = new Color(1f, 0f, 0f, 0.5f);
    [SerializeField] private Color hoveredColor = new Color(0f, 0.8f, 1f, 0.4f);

    // Grid data
    private GridCell[,] grid;
    private float cellWidth;
    private float cellHeight;
    private Vector3 gridOrigin;
    private Bounds gridBounds;

    // Visual elements
    private GameObject gridLinesParent;
    private GameObject zoneVisualsParent;
    private GameObject cellHighlightsParent;
    private Dictionary<Vector2Int, GameObject> cellHighlightObjects = new Dictionary<Vector2Int, GameObject>();

    // Zone definitions for 19x9 grid
    private const int PLAYER_ZONE_START = 0;
    private const int PLAYER_ZONE_END = 8;
    private const int NO_MANS_LAND = 9;
    private const int BOT_ZONE_START = 10;
    private const int BOT_ZONE_END = 18;

    // Properties
    public int Columns => gridColumns;
    public int Rows => gridRows;
    public float CellWidth => cellWidth;
    public float CellHeight => cellHeight;
    public Vector3 GridOrigin => gridOrigin;
    public Bounds GridBounds => gridBounds;

    private void Awake()
    {
        InitializeGrid();
    }

    #region Initialization

    /// <summary>
    /// Initialize the grid system fitted to the ground plane
    /// </summary>
    public void InitializeGrid()
    {
        if (groundPlane == null)
        {
            Debug.LogError("[TacticalGrid] Ground plane not assigned!");
            return;
        }

        // Calculate grid dimensions based on plane
        CalculateGridDimensions();

        // Create grid cells
        CreateGridCells();

        // Create visuals
        if (showGridLines) CreateGridLines();
        if (showZoneColors) CreateZoneVisuals();
        CreateCellHighlights();

        Debug.Log($"[TacticalGrid] Initialized {gridColumns}x{gridRows} grid");
        Debug.Log($"[TacticalGrid] Cell size: {cellWidth}x{cellHeight}");
        Debug.Log($"[TacticalGrid] Origin: {gridOrigin}");
    }

    /// <summary>
    /// Calculate cell dimensions to fit the plane
    /// </summary>
    private void CalculateGridDimensions()
    {
        // Get plane bounds
        Renderer planeRenderer = groundPlane.GetComponent<Renderer>();
        if (planeRenderer != null)
        {
            Bounds planeBounds = planeRenderer.bounds;

            // Calculate cell size to fit plane
            cellWidth = planeBounds.size.x / gridColumns;
            cellHeight = planeBounds.size.z / gridRows;

            // Set origin to bottom-left corner of plane
            gridOrigin = new Vector3(
                planeBounds.min.x,
                planeBounds.center.y + 0.01f, // Slightly above plane
                planeBounds.min.z
            );

            // Calculate grid bounds
            gridBounds = new Bounds(
                gridOrigin + new Vector3(gridColumns * cellWidth / 2f, 0f, gridRows * cellHeight / 2f),
                new Vector3(gridColumns * cellWidth, 0.1f, gridRows * cellHeight)
            );
        }
        else
        {
            Debug.LogError("[TacticalGrid] Ground plane has no Renderer component!");
        }
    }

    /// <summary>
    /// Create all grid cells
    /// </summary>
    private void CreateGridCells()
    {
        grid = new GridCell[gridRows, gridColumns];

        for (int row = 0; row < gridRows; row++)
        {
            for (int col = 0; col < gridColumns; col++)
            {
                Vector3 worldPos = GridToWorld(col, row);
                TeamSide allowedTeam = GetTeamForRow(row);

                grid[row, col] = new GridCell(col, row, worldPos, allowedTeam);
            }
        }
    }

    /// <summary>
    /// Determine which team can place in this row
    /// </summary>
    private TeamSide GetTeamForRow(int row)
    {
        if (row >= PLAYER_ZONE_START && row <= PLAYER_ZONE_END)
            return TeamSide.Player;
        else if (row == NO_MANS_LAND)
            return TeamSide.Neutral; // No one can place here
        else if (row >= BOT_ZONE_START && row <= BOT_ZONE_END)
            return TeamSide.Bot;

        return TeamSide.Neutral;
    }

    #endregion

    #region Coordinate Conversion

    /// <summary>
    /// Convert grid coordinates to world position (cell center)
    /// </summary>
    public Vector3 GridToWorld(int col, int row)
    {
        return gridOrigin + new Vector3(
            col * cellWidth + cellWidth / 2f,
            0f,
            row * cellHeight + cellHeight / 2f
        );
    }

    /// <summary>
    /// Convert world position to grid coordinates
    /// </summary>
    public Vector2Int WorldToGrid(Vector3 worldPosition)
    {
        Vector3 localPos = worldPosition - gridOrigin;

        int col = Mathf.FloorToInt(localPos.x / cellWidth);
        int row = Mathf.FloorToInt(localPos.z / cellHeight);

        return new Vector2Int(col, row);
    }

    /// <summary>
    /// Check if grid coordinates are valid
    /// </summary>
    public bool IsValidGridPosition(int col, int row)
    {
        return col >= 0 && col < gridColumns && row >= 0 && row < gridRows;
    }

    public bool IsValidGridPosition(Vector2Int gridPos)
    {
        return IsValidGridPosition(gridPos.x, gridPos.y);
    }

    #endregion

    #region Cell Access

    /// <summary>
    /// Get cell at grid coordinates
    /// </summary>
    public GridCell GetCell(int col, int row)
    {
        if (IsValidGridPosition(col, row))
            return grid[row, col];
        return null;
    }

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
    /// Get all cells that would be occupied by a unit with rotation
    /// </summary>
    public List<GridCell> GetCellsForPlacement(int col, int row, int width, int height, RotationAngle rotation)
    {
        List<GridCell> cells = new List<GridCell>();

        // Apply rotation to width/height
        int rotatedWidth = width;
        int rotatedHeight = height;

        if (rotation == RotationAngle.Rotate90 || rotation == RotationAngle.Rotate270)
        {
            // Swap dimensions for 90/270 degree rotations
            rotatedWidth = height;
            rotatedHeight = width;
        }

        // Get all cells in the rotated area
        for (int r = 0; r < rotatedHeight; r++)
        {
            for (int c = 0; c < rotatedWidth; c++)
            {
                GridCell cell = GetCell(col + c, row + r);
                if (cell != null)
                {
                    cells.Add(cell);
                }
            }
        }

        return cells;
    }

    #endregion

    #region Placement Validation

    /// <summary>
    /// Validate if a unit can be placed at the given position
    /// Checks: zone restrictions, cell availability, and path validation
    /// </summary>
    public bool ValidatePlacement(int col, int row, int width, int height, TeamSide team, UnitType unitType, RotationAngle rotation = RotationAngle.Rotate0)
    {
        // Get cells that would be occupied
        List<GridCell> cellsToOccupy = GetCellsForPlacement(col, row, width, height, rotation);

        // Check if we got the expected number of cells (all within bounds)
        int expectedCells = width * height;
        if (cellsToOccupy.Count != expectedCells)
        {
            return false; // Out of bounds
        }

        // Check all cells
        foreach (GridCell cell in cellsToOccupy)
        {
            // Check zone restrictions
            if (cell.allowedTeam == TeamSide.Neutral)
            {
                return false; // Can't place in no man's land
            }

            if (cell.allowedTeam != team)
            {
                return false; // Wrong team zone
            }

            // Check if cell is available
            if (!cell.IsAvailable())
            {
                return false; // Cell occupied
            }
        }

        // For walls and towers (blocking units), validate paths
        if (unitType == UnitType.Wall || unitType == UnitType.ArcherTower)
        {
            if (!ValidatePathsAfterPlacement(cellsToOccupy))
            {
                return false; // Would block all paths
            }
        }

        return true; // All checks passed
    }

    /// <summary>
    /// Validate that both teams still have a path after placement
    /// Uses flood fill algorithm to check connectivity
    /// </summary>
    private bool ValidatePathsAfterPlacement(List<GridCell> cellsToBlock)
    {
        // Temporarily mark cells as blocked
        foreach (GridCell cell in cellsToBlock)
        {
            cell.isTemporarilyBlocked = true;
        }

        // Check player path: row 4 → row 0
        bool playerHasPath = HasPathBetweenRows(NO_MANS_LAND, PLAYER_ZONE_START);

        // Check bot path: row 4 → row 8
        bool botHasPath = HasPathBetweenRows(NO_MANS_LAND, BOT_ZONE_END);

        // Unmark cells
        foreach (GridCell cell in cellsToBlock)
        {
            cell.isTemporarilyBlocked = false;
        }

        // Both teams must have a path
        return playerHasPath && botHasPath;
    }

    /// <summary>
    /// Flood fill to check if there's a path between two rows
    /// Uses 4-directional movement (N, S, E, W)
    /// </summary>
    private bool HasPathBetweenRows(int startRow, int targetRow)
    {
        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        // Start from all cells in start row
        for (int col = 0; col < gridColumns; col++)
        {
            GridCell startCell = GetCell(col, startRow);
            if (startCell != null && startCell.IsWalkable())
            {
                queue.Enqueue(new Vector2Int(col, startRow));
                visited.Add(new Vector2Int(col, startRow));
            }
        }

        // Flood fill
        Vector2Int[] directions = {
            new Vector2Int(0, 1),   // North
            new Vector2Int(0, -1),  // South
            new Vector2Int(1, 0),   // East
            new Vector2Int(-1, 0)   // West
        };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();

            // Check if we reached target row
            if (current.y == targetRow)
            {
                return true; // Path found!
            }

            // Explore neighbors
            foreach (Vector2Int dir in directions)
            {
                Vector2Int neighbor = current + dir;

                if (!IsValidGridPosition(neighbor) || visited.Contains(neighbor))
                    continue;

                GridCell neighborCell = GetCell(neighbor);
                if (neighborCell != null && neighborCell.IsWalkable())
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        return false; // No path found
    }

    #endregion

    #region Unit Placement & Management

    /// <summary>
    /// Place a unit on the grid
    /// </summary>
    public bool PlaceUnit(int col, int row, int width, int height, TeamSide team, UnitType unitType, GameObject unitObject, RotationAngle rotation = RotationAngle.Rotate0)
    {
        // Validate placement first
        if (!ValidatePlacement(col, row, width, height, team, unitType, rotation))
        {
            Debug.LogWarning($"[TacticalGrid] Cannot place {unitType} at ({col}, {row})");
            return false;
        }

        // Get cells to occupy
        List<GridCell> cellsToOccupy = GetCellsForPlacement(col, row, width, height, rotation);

        // Occupy all cells
        bool isMultiCell = cellsToOccupy.Count > 1;
        foreach (GridCell cell in cellsToOccupy)
        {
            cell.Occupy(unitObject, unitType, team, isMultiCell, unitObject);
        }

        Debug.Log($"[TacticalGrid] Placed {unitType} for {team} at ({col}, {row})");
        return true;
    }

    /// <summary>
    /// Remove a unit from the grid
    /// </summary>
    public void RemoveUnit(GameObject unitObject)
    {
        for (int row = 0; row < gridRows; row++)
        {
            for (int col = 0; col < gridColumns; col++)
            {
                GridCell cell = grid[row, col];
                if (cell.occupyingObject == unitObject)
                {
                    cell.Clear();
                }
            }
        }
    }

    /// <summary>
    /// Move a unit from one cell to another (for knights)
    /// </summary>
    public void MoveUnit(GameObject unitObject, Vector2Int oldPos, Vector2Int newPos)
    {
        GridCell oldCell = GetCell(oldPos);
        GridCell newCell = GetCell(newPos);

        if (oldCell == null || newCell == null)
        {
            Debug.LogWarning("[TacticalGrid] Invalid move positions");
            return;
        }

        // Clear old position
        oldCell.Clear();

        // Occupy new position
        newCell.Occupy(unitObject, UnitType.Knight, oldCell.team, false, unitObject);
    }

    /// <summary>
    /// Get count of units of a specific type for a team
    /// (Useful for checking "max 3 archers" later)
    /// </summary>
    public int GetUnitCount(TeamSide team, UnitType unitType)
    {
        HashSet<GameObject> uniqueUnits = new HashSet<GameObject>();

        for (int row = 0; row < gridRows; row++)
        {
            for (int col = 0; col < gridColumns; col++)
            {
                GridCell cell = grid[row, col];
                if (cell.team == team && cell.unitType == unitType && cell.occupyingObject != null)
                {
                    uniqueUnits.Add(cell.occupyingObject);
                }
            }
        }

        return uniqueUnits.Count;
    }

    /// <summary>
    /// Get all units for a team (for bot AI)
    /// </summary>
    public List<UnitData> GetAllUnits(TeamSide team)
    {
        List<UnitData> units = new List<UnitData>();
        HashSet<GameObject> processedObjects = new HashSet<GameObject>();

        for (int row = 0; row < gridRows; row++)
        {
            for (int col = 0; col < gridColumns; col++)
            {
                GridCell cell = grid[row, col];
                if (cell.team == team && cell.occupyingObject != null)
                {
                    // Avoid duplicates for multi-cell units
                    if (!processedObjects.Contains(cell.occupyingObject))
                    {
                        processedObjects.Add(cell.occupyingObject);

                        units.Add(new UnitData
                        {
                            type = cell.unitType,
                            team = cell.team,
                            unitObject = cell.occupyingObject,
                            gridPosition = new Vector2Int(col, row)
                        });
                    }
                }
            }
        }

        return units;
    }

    /// <summary>
    /// Get unit data at specific position
    /// </summary>
    public UnitData GetUnitAt(int col, int row)
    {
        GridCell cell = GetCell(col, row);
        if (cell == null || !cell.IsOccupied())
            return null;

        return new UnitData
        {
            type = cell.unitType,
            team = cell.team,
            unitObject = cell.occupyingObject,
            gridPosition = new Vector2Int(col, row)
        };
    }

    #endregion

    #region Visualization

    /// <summary>
    /// Create grid lines
    /// </summary>
    private void CreateGridLines()
    {
        gridLinesParent = new GameObject("GridLines");
        gridLinesParent.transform.SetParent(transform);

        Material lineMat = new Material(Shader.Find("Sprites/Default"));
        lineMat.color = gridLineColor;

        // Horizontal lines
        for (int row = 0; row <= gridRows; row++)
        {
            Vector3 start = gridOrigin + new Vector3(0f, 0f, row * cellHeight);
            Vector3 end = gridOrigin + new Vector3(gridColumns * cellWidth, 0f, row * cellHeight);
            CreateLine(start, end, lineMat, gridLinesParent.transform);
        }

        // Vertical lines
        for (int col = 0; col <= gridColumns; col++)
        {
            Vector3 start = gridOrigin + new Vector3(col * cellWidth, 0f, 0f);
            Vector3 end = gridOrigin + new Vector3(col * cellWidth, 0f, gridRows * cellHeight);
            CreateLine(start, end, lineMat, gridLinesParent.transform);
        }
    }

    private void CreateLine(Vector3 start, Vector3 end, Material mat, Transform parent)
    {
        GameObject lineObj = new GameObject("GridLine");
        lineObj.transform.SetParent(parent);

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.material = mat;
        lr.startWidth = gridLineWidth;
        lr.endWidth = gridLineWidth;
        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>
    /// Create zone color overlays
    /// </summary>
    private void CreateZoneVisuals()
    {
        zoneVisualsParent = new GameObject("ZoneVisuals");
        zoneVisualsParent.transform.SetParent(transform);

        // Player zone (rows 0-3)
        CreateZoneQuad("PlayerZone", PLAYER_ZONE_START, PLAYER_ZONE_END - PLAYER_ZONE_START + 1, playerZoneColor);

        // No man's land (row 4)
        CreateZoneQuad("NoMansLand", NO_MANS_LAND, 1, noMansLandColor);

        // Bot zone (rows 5-8)
        CreateZoneQuad("BotZone", BOT_ZONE_START, BOT_ZONE_END - BOT_ZONE_START + 1, botZoneColor);
    }

    private void CreateZoneQuad(string name, int startRow, int rowCount, Color color)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        quad.transform.SetParent(zoneVisualsParent.transform);

        // Position and scale
        Vector3 center = gridOrigin + new Vector3(
            gridColumns * cellWidth / 2f,
            -0.005f, // Slightly below grid
            startRow * cellHeight + (rowCount * cellHeight / 2f)
        );

        quad.transform.position = center;
        quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        quad.transform.localScale = new Vector3(gridColumns * cellWidth, rowCount * cellHeight, 1f);

        // Material
        Destroy(quad.GetComponent<Collider>());
        Renderer renderer = quad.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Standard"));
        mat.SetFloat("_Mode", 3); // Transparent
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = 3000;
        mat.color = color;
        renderer.material = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>
    /// Create cell highlight system
    /// </summary>
    private void CreateCellHighlights()
    {
        cellHighlightsParent = new GameObject("CellHighlights");
        cellHighlightsParent.transform.SetParent(transform);

        for (int row = 0; row < gridRows; row++)
        {
            for (int col = 0; col < gridColumns; col++)
            {
                GameObject highlight = CreateCellHighlight(col, row);
                cellHighlightObjects[new Vector2Int(col, row)] = highlight;
                highlight.SetActive(false);
            }
        }
    }

    private GameObject CreateCellHighlight(int col, int row)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = $"Highlight_{col}_{row}";
        quad.transform.SetParent(cellHighlightsParent.transform);

        Vector3 pos = GridToWorld(col, row);
        quad.transform.position = pos;
        quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        quad.transform.localScale = new Vector3(cellWidth * 0.9f, cellHeight * 0.9f, 1f);

        Destroy(quad.GetComponent<Collider>());

        Renderer renderer = quad.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Standard"));
        mat.SetFloat("_Mode", 3);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = 3000;
        renderer.material = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        return quad;
    }

    /// <summary>
    /// Highlight cells for placement preview
    /// </summary>
    public void HighlightCells(List<GridCell> cells, bool isValid)
    {
        ClearAllHighlights();

        Color color = isValid ? validPlacementColor : invalidPlacementColor;

        foreach (GridCell cell in cells)
        {
            Vector2Int key = new Vector2Int(cell.gridX, cell.gridZ);
            if (cellHighlightObjects.ContainsKey(key))
            {
                GameObject highlight = cellHighlightObjects[key];
                highlight.SetActive(true);
                highlight.GetComponent<Renderer>().material.color = color;
            }
        }
    }

    public void ClearAllHighlights()
    {
        foreach (GameObject highlight in cellHighlightObjects.Values)
        {
            highlight.SetActive(false);
        }
    }

    #endregion

    #region Debug

    private void OnDrawGizmos()
    {
        if (grid == null) return;

        // Draw grid bounds
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(gridBounds.center, gridBounds.size);
    }

    #endregion
}

#region Supporting Classes

[System.Serializable]
public class GridCell
{
    public int gridX;
    public int gridZ;
    public Vector3 worldPosition;
    public TeamSide allowedTeam; // Who can place here

    // Occupation data
    public bool isOccupied = false;
    public GameObject occupyingObject = null;
    public UnitType unitType = UnitType.Empty;
    public TeamSide team = TeamSide.Neutral;
    public bool isPartOfMultiCell = false;
    public GameObject multiCellParent = null;

    // Temporary flag for path validation
    public bool isTemporarilyBlocked = false;

    public GridCell(int x, int z, Vector3 position, TeamSide allowed)
    {
        gridX = x;
        gridZ = z;
        worldPosition = position;
        allowedTeam = allowed;
    }

    public bool IsAvailable()
    {
        return !isOccupied;
    }

    public bool IsOccupied()
    {
        return isOccupied;
    }

    public bool IsWalkable()
    {
        // Walkable if not occupied by blocking units (walls/towers)
        // Knights can walk through cells with other knights
        if (isTemporarilyBlocked) return false;
        if (!isOccupied) return true;

        // Knights don't block movement
        return unitType == UnitType.Knight;
    }

    public void Occupy(GameObject obj, UnitType type, TeamSide t, bool isMulti = false, GameObject parent = null)
    {
        isOccupied = true;
        occupyingObject = obj;
        unitType = type;
        team = t;
        isPartOfMultiCell = isMulti;
        multiCellParent = parent;
    }

    public void Clear()
    {
        isOccupied = false;
        occupyingObject = null;
        unitType = UnitType.Empty;
        team = TeamSide.Neutral;
        isPartOfMultiCell = false;
        multiCellParent = null;
    }
}

[System.Serializable]
public class UnitData
{
    public UnitType type;
    public TeamSide team;
    public GameObject unitObject;
    public Vector2Int gridPosition;
}

public enum TeamSide
{
    Neutral,
    Player,
    Bot
}

public enum UnitType
{
    Empty,
    Wall,
    Knight,
    ArcherTower
}

public enum RotationAngle
{
    Rotate0 = 0,
    Rotate90 = 90,
    Rotate180 = 180,
    Rotate270 = 270
}

#endregion