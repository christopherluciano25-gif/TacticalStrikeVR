using UnityEngine;

/// <summary>
/// Menu controller for selecting units to place
/// Attached to your existing hand menu
/// </summary>
public class PlacementMenu : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private UnitPlacer unitPlacer;

    [Header("Unit Prefabs")]
    [SerializeField] private GameObject wallPrefab;
    [SerializeField] private GameObject knightPrefab;
    [SerializeField] private GameObject archerTowerPrefab;

    // Called by your wall button
    public void SelectWall()
    {
        if (unitPlacer == null)
        {
            Debug.LogError("[Menu] UnitPlacer is not assigned!");
            return;
        }

        unitPlacer.StartPlacement(wallPrefab, UnitType.Wall, 1, 3);
        Debug.Log("[Menu] Selected Wall (1x3)");
    }

    // Called by your knight button
    public void SelectKnight()
    {
        if (unitPlacer == null)
        {
            Debug.LogError("[Menu] UnitPlacer is not assigned!");
            return;
        }

        unitPlacer.StartPlacement(knightPrefab, UnitType.Knight, 1, 1);
        Debug.Log("[Menu] Selected Knight (1x1)");
    }

    // Called by your archer tower button
    public void SelectArcherTower()
    {
        if (unitPlacer == null)
        {
            Debug.LogError("[Menu] UnitPlacer is not assigned!");
            return;
        }

        unitPlacer.StartPlacement(archerTowerPrefab, UnitType.ArcherTower, 2, 2);
        Debug.Log("[Menu] Selected Archer Tower (2x2)");
    }

    // Optional: cancel button
    public void CancelPlacement()
    {
        if (unitPlacer == null)
        {
            Debug.LogError("[Menu] UnitPlacer is not assigned!");
            return;
        }

        unitPlacer.CancelPlacement();
        Debug.Log("[Menu] Cancelled placement");
    }
}