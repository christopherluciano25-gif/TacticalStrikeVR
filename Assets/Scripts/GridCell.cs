using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Data structure for individual grid cell
/// Tracks position, state, and occupancy information
/// </summary>
[System.Serializable]
public class GridCell
{
    // Grid coordinates (0-based indices)
    public int gridX;
    public int gridZ;
    
    // World position (center of cell)
    public Vector3 worldPosition;
    
    // Cell state
    public CellState state = CellState.Available;
    
    // Reference to occupying unit (if any)
    public GameObject occupyingUnit = null;
    
    // Multi-cell unit tracking
    public bool isPartOfMultiCellUnit = false;
    public GameObject multiCellUnitRoot = null;
    
    // Placement validity flags
    public bool isWalkable = true;
    public bool allowsPlayerUnits = true;
    public bool allowsEnemyUnits = true;
    public bool allowsWalls = true;
    
    // Constructor
    public GridCell(int x, int z, Vector3 worldPos)
    {
        gridX = x;
        gridZ = z;
        worldPosition = worldPos;
    }
    
    /// <summary>
    /// Check if cell is available for placement
    /// </summary>
    public bool IsAvailable()
    {
        return state == CellState.Available && occupyingUnit == null;
    }
    
    /// <summary>
    /// Occupy this cell with a unit
    /// </summary>
    public void Occupy(GameObject unit, bool isMultiCell = false, GameObject rootUnit = null)
    {
        occupyingUnit = unit;
        state = CellState.Occupied;
        isPartOfMultiCellUnit = isMultiCell;
        multiCellUnitRoot = rootUnit ?? unit;
    }
    
    /// <summary>
    /// Clear this cell (remove unit)
    /// </summary>
    public void Clear()
    {
        occupyingUnit = null;
        state = CellState.Available;
        isPartOfMultiCellUnit = false;
        multiCellUnitRoot = null;
    }
    
    /// <summary>
    /// Set cell as restricted
    /// </summary>
    public void SetRestricted(bool restricted)
    {
        state = restricted ? CellState.Restricted : CellState.Available;
    }
}

/// <summary>
/// Cell state enum
/// </summary>
public enum CellState
{
    Available,      // Empty and ready for placement
    Occupied,       // Has a unit on it
    Restricted,     // Cannot place units here
    Hovered         // Currently being hovered by raycast
}

/// <summary>
/// Grid mode enum
/// </summary>
public enum GridMode
{
    Fixed,          // Predefined dimensions
    AreaBased       // Draw area and divide into cells
}