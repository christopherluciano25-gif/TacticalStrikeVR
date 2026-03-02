using System;
using System.Collections.Generic;
using UnityEngine;

public class TDEnemyPlayerAI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TacticalGrid tacticalGrid;
    [SerializeField] private GameObject archerTowerPrefab;
    [SerializeField] private GameObject wallPrefab;

    [Header("Placement Limits")]
    [SerializeField] private int maxArcherTowers = 3;
    [SerializeField] private int maxWalls = 3;

    [Header("Execution")]
    [SerializeField] private bool runOnStart = false;

    // Optional: if true, treat the player's invalid area as bot placement area.
    [SerializeField] private bool usePlayersInvalidArea = true;

    private readonly System.Random rng = new System.Random();

    private const int WALL_WIDTH = 1;
    private const int WALL_HEIGHT = 3;
    private const int TOWER_WIDTH = 2;
    private const int TOWER_HEIGHT = 2;

    private int wallsPlacedThisTurn;
    private int towersPlacedThisTurn;

    private struct PlacementCandidate
    {
        public int col;
        public int row;
        public RotationAngle rotation;
        public int score;

        public PlacementCandidate(int c, int r, RotationAngle rot, int s)
        {
            col = c;
            row = r;
            rotation = rot;
            score = s;
        }
    }

    private void Awake()
    {
        if (tacticalGrid == null)
        {
            tacticalGrid = FindObjectOfType<TacticalGrid>();
        }
    }

    private void Start()
    {
        if (runOnStart)
        {
            DoAITurn();
        }
    }

    public void DoAITurn()
    {
        if (!ValidateReferences())
        {
            return;
        }

        wallsPlacedThisTurn = 0;
        towersPlacedThisTurn = 0;

        PlaceWalls();
        PlaceTowers();

        Debug.Log($"[EnemyAI] Turn complete. Walls: {wallsPlacedThisTurn}/{maxWalls}, Towers: {towersPlacedThisTurn}/{maxArcherTowers}");
    }

    private bool ValidateReferences()
    {
        if (tacticalGrid == null)
        {
            Debug.LogError("[EnemyAI] TacticalGrid not assigned.");
            return false;
        }

        if (wallPrefab == null)
        {
            Debug.LogError("[EnemyAI] Wall prefab not assigned.");
            return false;
        }

        if (archerTowerPrefab == null)
        {
            Debug.LogError("[EnemyAI] Archer tower prefab not assigned.");
            return false;
        }

        return true;
    }

    private void PlaceWalls()
    {
        while (wallsPlacedThisTurn < maxWalls)
        {
            var candidates = BuildCandidates(UnitType.Wall, WALL_WIDTH, WALL_HEIGHT, includeWallRotations: true);
            if (candidates.Count == 0)
            {
                break;
            }

            PlacementCandidate choice = PickBestCandidate(candidates);
            if (!TryPlace(UnitType.Wall, wallPrefab, choice.col, choice.row, WALL_WIDTH, WALL_HEIGHT, choice.rotation))
            {
                break;
            }

            wallsPlacedThisTurn++;
        }
    }

    private void PlaceTowers()
    {
        while (towersPlacedThisTurn < maxArcherTowers)
        {
            var candidates = BuildCandidates(UnitType.ArcherTower, TOWER_WIDTH, TOWER_HEIGHT, includeWallRotations: false);
            if (candidates.Count == 0)
            {
                break;
            }

            PlacementCandidate choice = PickBestCandidate(candidates);
            if (!TryPlace(UnitType.ArcherTower, archerTowerPrefab, choice.col, choice.row, TOWER_WIDTH, TOWER_HEIGHT, choice.rotation))
            {
                break;
            }

            towersPlacedThisTurn++;
        }
    }

    private List<PlacementCandidate> BuildCandidates(UnitType unitType, int width, int height, bool includeWallRotations)
    {
        var list = new List<PlacementCandidate>();

        RotationAngle[] rotations = includeWallRotations
            ? new[] { RotationAngle.Rotate0, RotationAngle.Rotate90 }
            : new[] { RotationAngle.Rotate0 };

        for (int row = 0; row < tacticalGrid.Rows; row++)
        {
            for (int col = 0; col < tacticalGrid.Columns; col++)
            {
                foreach (RotationAngle rotation in rotations)
                {
                    if (!IsInBotPlacementArea(col, row, width, height, rotation))
                    {
                        continue;
                    }

                    if (!tacticalGrid.ValidatePlacement(col, row, width, height, TeamSide.Bot, unitType, rotation))
                    {
                        continue;
                    }

                    // Favor rows closest to no man's land (row 4) so bot defenses are forward.
                    int distanceToCenterLine = Mathf.Abs(row - 4);
                    int score = 100 - (distanceToCenterLine * 10) + rng.Next(0, 6);
                    list.Add(new PlacementCandidate(col, row, rotation, score));
                }
            }
        }

        return list;
    }

    private bool IsInBotPlacementArea(int col, int row, int width, int height, RotationAngle rotation)
    {
        List<GridCell> cells = tacticalGrid.GetCellsForPlacement(col, row, width, height, rotation);
        if (cells.Count != width * height)
        {
            return false;
        }

        foreach (GridCell cell in cells)
        {
            if (cell == null)
            {
                return false;
            }

            if (usePlayersInvalidArea)
            {
                // "Invalid for player" but still placeable for bot means Bot zone cells.
                if (cell.allowedTeam != TeamSide.Bot)
                {
                    return false;
                }
            }
            else
            {
                if (cell.allowedTeam != TeamSide.Bot)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private PlacementCandidate PickBestCandidate(List<PlacementCandidate> candidates)
    {
        candidates.Sort((a, b) => b.score.CompareTo(a.score));

        int topK = Mathf.Min(3, candidates.Count);
        int idx = rng.Next(topK);
        return candidates[idx];
    }

    private bool TryPlace(UnitType unitType, GameObject prefab, int col, int row, int width, int height, RotationAngle rotation)
    {
        Vector3 worldPos = ComputePlacementWorldPosition(col, row, width, height, unitType, rotation);
        Quaternion worldRot = ComputePlacementRotation(unitType, rotation);

        GameObject instance = Instantiate(prefab, worldPos, worldRot);
        instance.name = $"Bot_{unitType}_{col}_{row}";

        bool success = tacticalGrid.PlaceUnit(col, row, width, height, TeamSide.Bot, unitType, instance, rotation);
        if (!success)
        {
            Destroy(instance);
            return false;
        }

        Debug.Log($"[EnemyAI] Placed {unitType} at ({col}, {row}) rot={rotation}");
        return true;
    }

    private Vector3 ComputePlacementWorldPosition(int col, int row, int width, int height, UnitType unitType, RotationAngle rotation)
    {
        GridCell anchorCell = tacticalGrid.GetCell(col, row);
        if (anchorCell == null)
        {
            return Vector3.zero;
        }

        int rotatedWidth = width;
        int rotatedHeight = height;

        if (rotation == RotationAngle.Rotate90 || rotation == RotationAngle.Rotate270)
        {
            rotatedWidth = height;
            rotatedHeight = width;
        }

        float offsetX = 0f;
        float offsetZ = 0f;

        if (unitType == UnitType.ArcherTower)
        {
            offsetX = (rotatedWidth - 1) * tacticalGrid.CellWidth * 0.5f;
            offsetZ = (rotatedHeight - 1) * tacticalGrid.CellHeight * 0.5f;
        }

        return anchorCell.worldPosition + new Vector3(offsetX, 0f, offsetZ);
    }

    private Quaternion ComputePlacementRotation(UnitType unitType, RotationAngle rotation)
    {
        float baseRotation = unitType == UnitType.Wall ? 90f : 0f;
        float finalY = (baseRotation + (float)rotation) % 360f;
        return Quaternion.Euler(0f, finalY, 0f);
    }
}
