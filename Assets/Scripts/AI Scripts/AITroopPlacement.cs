using System;
using System.Collections.Generic;
using UnityEngine;

public class AITroopPlacement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TacticalGrid tacticalGrid;
    [SerializeField] private AIManagement aiManagement;
    [SerializeField] private GameObject troopPrefab;

    [Header("Troop Placement")]
    [SerializeField] private UnitType troopUnitType = UnitType.Knight;
    [SerializeField] private int troopWidth = 1;
    [SerializeField] private int troopHeight = 1;
    [SerializeField] private int troopCost = 2;
    [SerializeField] private int maxTroopsToPlace = 30;
    [SerializeField] private float decisionInterval = 1.5f;

    [Header("Decision Tuning")]
    [SerializeField] private int threatDistanceFromFrontline = 3;
    [SerializeField] private int interceptThreatThreshold = 4;
    [SerializeField] private int topCandidatesToRandomPick = 3;

    private readonly System.Random rng = new System.Random();

    private int totalTroopsPlaced;
    private float decisionTimer;

    private struct PlacementCandidate
    {
        public int col;
        public int row;
        public int score;

        public PlacementCandidate(int c, int r, int s)
        {
            col = c;
            row = r;
            score = s;
        }
    }

    private enum PlacementMode
    {
        Advance,
        Intercept
    }

    private void Awake()
    {
        if (tacticalGrid == null)
        {
            tacticalGrid = FindFirstObjectByType<TacticalGrid>();
        }

        if (aiManagement == null)
        {
            aiManagement = FindFirstObjectByType<AIManagement>();
        }

        troopWidth = Mathf.Max(1, troopWidth);
        troopHeight = Mathf.Max(1, troopHeight);
        troopCost = Mathf.Max(1, troopCost);
        topCandidatesToRandomPick = Mathf.Max(1, topCandidatesToRandomPick);
        threatDistanceFromFrontline = Mathf.Max(1, threatDistanceFromFrontline);
    }

    private void Update()
    {
        decisionTimer += Time.deltaTime;

        if (decisionTimer >= decisionInterval)
        {
            decisionTimer = 0f;
            TryPlaceTroopWithResources();
        }
    }

    public bool TryPlaceTroopWithResources()
    {
        if (!ValidateReferences())
        {
            return false;
        }

        if (totalTroopsPlaced >= maxTroopsToPlace)
        {
            return false;
        }

        if (aiManagement.GetCurrentResources() < troopCost)
        {
            return false;
        }

        List<UnitData> playerUnits = tacticalGrid.GetAllUnits(TeamSide.Player);
        PlacementMode mode = DetermineMode(playerUnits);

        List<PlacementCandidate> candidates = BuildCandidates(mode, playerUnits);
        if (candidates.Count == 0)
        {
            if (mode == PlacementMode.Intercept)
            {
                candidates = BuildCandidates(PlacementMode.Advance, playerUnits);
            }

            if (candidates.Count == 0)
            {
                return false;
            }
        }

        PlacementCandidate chosen = PickCandidate(candidates);

        if (!aiManagement.SpendResources(troopCost))
        {
            return false;
        }

        bool placed = TryPlace(chosen.col, chosen.row);
        if (!placed)
        {
            aiManagement.AddResources(troopCost);
            return false;
        }

        totalTroopsPlaced++;
        Debug.Log($"[AITroopPlacement] Placed troop #{totalTroopsPlaced} in {mode} mode at ({chosen.col}, {chosen.row})");
        return true;
    }

    private bool ValidateReferences()
    {
        if (tacticalGrid == null)
        {
            Debug.LogError("[AITroopPlacement] TacticalGrid not assigned.");
            return false;
        }

        if (aiManagement == null)
        {
            Debug.LogError("[AITroopPlacement] AIManagement not assigned.");
            return false;
        }

        if (troopPrefab == null)
        {
            Debug.LogError("[AITroopPlacement] Troop prefab not assigned.");
            return false;
        }

        return true;
    }

    private PlacementMode DetermineMode(List<UnitData> playerUnits)
    {
        if (playerUnits == null || playerUnits.Count == 0)
        {
            return PlacementMode.Advance;
        }

        int botFrontRow = GetBotFrontRow();
        int threatScore = 0;

        foreach (UnitData enemy in playerUnits)
        {
            if (enemy == null)
            {
                continue;
            }

            int distanceToFront = Mathf.Abs(enemy.gridPosition.y - botFrontRow);
            if (distanceToFront > threatDistanceFromFrontline)
            {
                continue;
            }

            threatScore += GetThreatWeight(enemy.type);
        }

        return threatScore >= interceptThreatThreshold
            ? PlacementMode.Intercept
            : PlacementMode.Advance;
    }

    private List<PlacementCandidate> BuildCandidates(PlacementMode mode, List<UnitData> playerUnits)
    {
        var candidates = new List<PlacementCandidate>();
        int centerCol = tacticalGrid.Columns / 2;
        int botFrontRow = GetBotFrontRow();
        int botBackRow = GetBotBackRow();

        for (int row = 0; row < tacticalGrid.Rows; row++)
        {
            for (int col = 0; col < tacticalGrid.Columns; col++)
            {
                if (!IsInBotPlacementArea(col, row))
                {
                    continue;
                }

                if (!tacticalGrid.ValidatePlacement(col, row, troopWidth, troopHeight, TeamSide.Bot, troopUnitType, RotationAngle.Rotate0))
                {
                    continue;
                }

                int score = mode == PlacementMode.Intercept
                    ? ScoreIntercept(col, row, botFrontRow, centerCol, playerUnits)
                    : ScoreAdvance(col, row, botFrontRow, botBackRow, centerCol);

                score += rng.Next(0, 4);
                candidates.Add(new PlacementCandidate(col, row, score));
            }
        }

        return candidates;
    }

    private int ScoreAdvance(int col, int row, int botFrontRow, int botBackRow, int centerCol)
    {
        int forwardProgress = botBackRow - row;
        int centerPreference = 10 - Mathf.Abs(col - centerCol);
        int frontlineBias = 8 - Mathf.Abs(row - botFrontRow);

        return (forwardProgress * 12) + (centerPreference * 2) + frontlineBias;
    }

    private int ScoreIntercept(int col, int row, int botFrontRow, int centerCol, List<UnitData> playerUnits)
    {
        int nearestThreat = 0;

        if (playerUnits != null)
        {
            foreach (UnitData enemy in playerUnits)
            {
                if (enemy == null)
                {
                    continue;
                }

                int distance = Mathf.Abs(enemy.gridPosition.x - col) + Mathf.Abs(enemy.gridPosition.y - row);
                int weight = GetThreatWeight(enemy.type);
                int influence = Mathf.Max(0, (8 - distance)) * weight;

                if (influence > nearestThreat)
                {
                    nearestThreat = influence;
                }
            }
        }

        int frontGuard = 10 - Mathf.Abs(row - botFrontRow);
        int centerPreference = 8 - Mathf.Abs(col - centerCol);

        return (nearestThreat * 4) + (frontGuard * 3) + centerPreference;
    }

    private int GetThreatWeight(UnitType unitType)
    {
        switch (unitType)
        {
            case UnitType.Knight:
                return 3;
            case UnitType.ArcherTower:
                return 2;
            case UnitType.Wall:
                return 1;
            default:
                return 1;
        }
    }

    private PlacementCandidate PickCandidate(List<PlacementCandidate> candidates)
    {
        candidates.Sort((a, b) => b.score.CompareTo(a.score));

        int topN = Mathf.Min(topCandidatesToRandomPick, candidates.Count);
        int index = rng.Next(0, topN);
        return candidates[index];
    }

    private bool TryPlace(int col, int row)
    {
        Vector3 spawnPos = ComputePlacementWorldPosition(col, row);
        Quaternion spawnRot = Quaternion.identity;

        GameObject instance = Instantiate(troopPrefab, spawnPos, spawnRot);
        instance.name = $"Bot_{troopUnitType}_{col}_{row}";

        SetTeamToBot(instance);

        bool success = tacticalGrid.PlaceUnit(
            col,
            row,
            troopWidth,
            troopHeight,
            TeamSide.Bot,
            troopUnitType,
            instance,
            RotationAngle.Rotate0
        );

        if (!success)
        {
            Destroy(instance);
            return false;
        }

        return true;
    }

    private Vector3 ComputePlacementWorldPosition(int col, int row)
    {
        GridCell anchorCell = tacticalGrid.GetCell(col, row);
        if (anchorCell == null)
        {
            return Vector3.zero;
        }

        float offsetX = 0f;
        float offsetZ = 0f;

        if (troopWidth > 1 || troopHeight > 1)
        {
            offsetX = (troopWidth - 1) * tacticalGrid.CellWidth * 0.5f;
            offsetZ = (troopHeight - 1) * tacticalGrid.CellHeight * 0.5f;
        }

        return anchorCell.worldPosition + new Vector3(offsetX, 0f, offsetZ);
    }

    private void SetTeamToBot(GameObject unit)
    {
        if (unit == null)
        {
            return;
        }

        Knight knight = unit.GetComponent<Knight>();
        if (knight != null)
        {
            knight.team = TeamSide.Bot;
        }

        Archer archer = unit.GetComponent<Archer>();
        if (archer != null)
        {
            archer.team = TeamSide.Bot;
        }

        Bomber bomber = unit.GetComponent<Bomber>();
        if (bomber != null)
        {
            bomber.team = TeamSide.Bot;
        }

        WallHealth wall = unit.GetComponent<WallHealth>();
        if (wall != null)
        {
            wall.owner = TeamSide.Bot;
        }
    }

    private bool IsInBotPlacementArea(int col, int row)
    {
        List<GridCell> cells = tacticalGrid.GetCellsForPlacement(col, row, troopWidth, troopHeight, RotationAngle.Rotate0);

        if (cells.Count != troopWidth * troopHeight)
        {
            return false;
        }

        foreach (GridCell cell in cells)
        {
            if (cell == null || cell.allowedTeam != TeamSide.Bot)
            {
                return false;
            }
        }

        return true;
    }

    private int GetBotFrontRow()
    {
        int front = int.MaxValue;

        for (int row = 0; row < tacticalGrid.Rows; row++)
        {
            for (int col = 0; col < tacticalGrid.Columns; col++)
            {
                GridCell cell = tacticalGrid.GetCell(col, row);
                if (cell != null && cell.allowedTeam == TeamSide.Bot)
                {
                    front = Mathf.Min(front, row);
                }
            }
        }

        return front == int.MaxValue ? 0 : front;
    }

    private int GetBotBackRow()
    {
        int back = int.MinValue;

        for (int row = 0; row < tacticalGrid.Rows; row++)
        {
            for (int col = 0; col < tacticalGrid.Columns; col++)
            {
                GridCell cell = tacticalGrid.GetCell(col, row);
                if (cell != null && cell.allowedTeam == TeamSide.Bot)
                {
                    back = Mathf.Max(back, row);
                }
            }
        }

        return back == int.MinValue ? 0 : back;
    }
}
