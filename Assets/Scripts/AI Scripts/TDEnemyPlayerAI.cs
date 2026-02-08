using UnityEngine;
using System;
using System.Collections.Generic;

public class TDEnemyPlayerAI : MonoBehaviour
{
    [SerializeField] private GridSystem gridSystem;
    [SerializeField] private GameObject archerTowerBlockPrefab;
    [SerializeField] private GameObject wallBlockPrefab;
    [SerializeField] private bool runOnStart = false;

    private TowerDefenceAI ai;

    void Awake()
    {
        ai = new TowerDefenceAI();
    }

    void Start()
    {
        if (runOnStart)
        {
            DoAITurn();
        }
    }

    public void DoAITurn()
    {
        ai.DoAITurn();
        DisplayBoard();
    }

    private void OnDrawGizmos()
    {
        // Editor-only visualization: draw viable enemy lanes
        if (ai == null) return;

        var (viableLanes, board) = ai.GetViableLanesInfo();
        if (viableLanes.Count == 0) return;

        // Draw incoming lanes from right edge (spawn) in green
        foreach (var laneRow in viableLanes)
        {
            Vector3 spawnPoint = gridSystem != null && laneRow < gridSystem.Rows 
                ? gridSystem.Cells[gridSystem.Columns - 1, laneRow].worldPosition 
                : Vector3.zero;
            
            Vector3 goalPoint = gridSystem != null && laneRow < gridSystem.Rows 
                ? gridSystem.Cells[0, laneRow].worldPosition 
                : new Vector3(-9, 0, 0);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(spawnPoint, goalPoint);
            Gizmos.DrawSphere(spawnPoint, 0.3f); // marker at spawn
        }
    }

    private void DisplayBoard()
    {
        if (gridSystem == null)
        {
            Debug.LogError("GridSystem not assigned");
            return;
        }
        if (archerTowerBlockPrefab == null)
        {
            Debug.LogError("Archer Tower Block Prefab not assigned");
            return;
        }
        if (wallBlockPrefab == null)
        {
            Debug.LogError("Wall Block Prefab not assigned");
            return;
        }

        int[,] board = ai.GetAIBoard();
        bool[,] processed = new bool[9, 9];
        int placedCount = 0;

        // First pass: Place archer towers (2x2 blocks with single center instance)
        for (int r = 0; r < 9; r++)
        {
            for (int c = 0; c < 9; c++)
            {
                if (processed[r, c]) continue;

                // Check for 2x2 archer tower pattern
                if (board[r, c] == 1 && r + 1 < 9 && c + 1 < 9 &&
                    board[r, c] == 1 && board[r, c + 1] == 1 &&
                    board[r + 1, c] == 1 && board[r + 1, c + 1] == 1)
                {
                    // Place single tower at center of 2x2 (between the 4 cells)
                    // Center position is at (r+0.5, c+0.5) in board space
                    // Get center cell coordinates
                    int centerR = r;
                    int centerC = c;

                    if (centerC >= gridSystem.Columns || centerR >= gridSystem.Rows)
                    {
                        Debug.LogWarning($"Tower at ({centerR},{centerC}) out of grid bounds");
                    }
                    else
                    {
                        GridCell cell = gridSystem.Cells[centerC, centerR];
                        if (cell != null && cell.IsAvailable())
                        {
                            Vector3 towerPos = cell.worldPosition;
                            GameObject instance = Instantiate(archerTowerBlockPrefab, towerPos, Quaternion.identity);
                            cell.Occupy(instance);
                            placedCount++;
                            Debug.Log($"Placed archer tower at ({centerR},{centerC})");
                        }
                    }

                    // Mark all 4 cells as processed
                    processed[r, c] = true;
                    processed[r, c + 1] = true;
                    processed[r + 1, c] = true;
                    processed[r + 1, c + 1] = true;
                }
            }
        }

        // Second pass: Place walls (1x4 or 4x1 blocks, cell by cell)
        for (int r = 0; r < 9; r++)
        {
            for (int c = 0; c < 9; c++)
            {
                if (processed[r, c]) continue;
                if (board[r, c] != 2) continue;

                if (c >= gridSystem.Columns || r >= gridSystem.Rows)
                {
                    Debug.LogWarning($"Board position ({r},{c}) out of grid bounds ({gridSystem.Rows},{gridSystem.Columns})");
                    continue;
                }

                GridCell cell = gridSystem.Cells[c, r];
                if (cell == null)
                {
                    Debug.LogError($"Cell at ({c},{r}) is null");
                    continue;
                }
                if (!cell.IsAvailable())
                {
                    Debug.LogWarning($"Cell at ({c},{r}) is not available for placement");
                    continue;
                }

                // Determine wall orientation - check adjacent cells
                bool hasHorizontalNeighbor = (c + 1 < 9 && board[r, c + 1] == 2) || (c - 1 >= 0 && board[r, c - 1] == 2);
                bool hasVerticalNeighbor = (r + 1 < 9 && board[r + 1, c] == 2) || (r - 1 >= 0 && board[r - 1, c] == 2);
                
                GameObject instance = Instantiate(wallBlockPrefab, cell.worldPosition, Quaternion.identity);
                
                // Rotate horizontal walls 90 degrees, leave vertical walls at default rotation
                if (hasHorizontalNeighbor && !hasVerticalNeighbor)
                {
                    instance.transform.rotation = Quaternion.Euler(0, 90, 0);
                }
                
                cell.Occupy(instance);
                placedCount++;
                processed[r, c] = true;
            }
        }

        Debug.Log($"AI placed {placedCount} blocks on the grid");
    }
}

public class TowerDefenceAI
{
    // 0 = empty, 1 = archer tower, 2 = wall
    private int[,] aiBoard;
    private int[,] userBoard;

    private const int BOARD_SIZE = 9;
    private const int ARCHER_TOWER_SIZE = 2;     // 2x2
    private const int WALL_LEN = 4;              // 1x4 or 4x1
    private const int DEADZONE_WIDTH = 2;

    // Random generator
    private readonly System.Random rng = new System.Random(); //Random used to shuffle board placements

    // Last predicted enemy spawn/goal (updated each turn)
    private (int r, int c) lastPredictedSpawn = (-1, -1);
    private (int r, int c) lastPredictedGoal = (-1, -1);

    // Placement Limits
    private int totalArcherTowersPlaced = 0;
    private int totalWallsPlaced = 0;
    private const int MAX_ARCHER_TOWERS = 3;
    private const int MAX_WALLS = 3;

    // ====== AI placement types ======
    public enum PlacementType { ArcherTower, WallH, WallV }

    public struct Placement
    {
        public PlacementType Type;
        public int Row, Col;

        public Placement(PlacementType type, int row, int col)
        {
            Type = type;
            Row = row;
            Col = col;
        }

        public override string ToString() => $"{Type} at ({Row},{Col})";
    }

    public TowerDefenceAI()
    {
        aiBoard = new int[BOARD_SIZE, BOARD_SIZE];
        userBoard = new int[BOARD_SIZE, BOARD_SIZE];
        InitializeBoards();
    }

    public int[,] GetAIBoard() => aiBoard;

    // Get viable lanes for editor visualization
    public (List<int> viableLanes, int[,] board) GetViableLanesInfo()
    {
        var lanes = new List<int>();
        for (int spawnRow = 0; spawnRow < BOARD_SIZE; spawnRow++)
        {
            var spawn = (r: spawnRow, c: BOARD_SIZE - 1);
            bool canReachGoal = false;

            for (int goalRow = 0; goalRow < BOARD_SIZE; goalRow++)
            {
                var goal = (r: goalRow, c: 0);
                if (FindShortestPath(aiBoard, spawn, goal) != null)
                {
                    canReachGoal = true;
                    break;
                }
            }

            if (canReachGoal)
                lanes.Add(spawnRow);
        }

        return (lanes, aiBoard);
    }

    private void InitializeBoards()
    {
        for (int r = 0; r < BOARD_SIZE; r++)
            for (int c = 0; c < BOARD_SIZE; c++)
            {
                aiBoard[r, c] = 0;
                userBoard[r, c] = 0;
            }
    }

    // =========================================
    // PUBLIC: Run one AI turn (places all structures in prep phase)
    // =========================================
    // =========================================
    // PUBLIC: Run one AI turn (places all structures in prep phase)
    // =========================================
    public void DoAITurn()
    {
        int initialLanes = CountViableLanes(aiBoard);
        Debug.Log($"AI turn start: {initialLanes} viable enemy lanes");

        // Dummy spawn/goal for any remaining logic (no longer used for path prediction)
        var spawn = (r: 0, c: BOARD_SIZE - 1);
        var goal = (r: 0, c: 0);

        // Phase 1: Prioritize wall placement to funnel enemies into 1 lane
        while (totalWallsPlaced < MAX_WALLS)
        {
            Placement choice = ChooseNextPlacementLCV(spawn, goal, p => p.Type != PlacementType.ArcherTower);
            
            if (choice.Row == -1) break;

            if (!ApplyPlacement(aiBoard, choice)) break;

            IncrementPlacementCount(choice);
            int lanesNow = CountViableLanes(aiBoard);
            Debug.Log($"  Placed wall: {choice} | Lanes now: {lanesNow}");
        }

        // Phase 2: Place towers along the remaining lane(s)
        while (totalArcherTowersPlaced < MAX_ARCHER_TOWERS)
        {
            Placement choice = ChooseNextPlacementLCV(spawn, goal, p => p.Type == PlacementType.ArcherTower);
            
            if (choice.Row == -1) break;

            if (!ApplyPlacement(aiBoard, choice)) break;

            IncrementPlacementCount(choice);
            Debug.Log($"  Placed tower: {choice}");
        }

        int finalLanes = CountViableLanes(aiBoard);
        Debug.Log($"AI turn complete: {initialLanes} → {finalLanes} lanes | Towers: {totalArcherTowersPlaced}/{MAX_ARCHER_TOWERS}, Walls: {totalWallsPlaced}/{MAX_WALLS}");
    }

    // Find the most accessible enemy path (most paths available from right to left)
    private ((int r, int c), (int r, int c)) FindMostLikelyEnemyPath()
    {
        int bestSpawnRow = 0;
        int bestGoalRow = 0;
        int maxPathsFromSpawn = 0;

        // For each possible spawn row, count how many different goal rows are reachable
        for (int spawnRow = 0; spawnRow < BOARD_SIZE; spawnRow++)
        {
            var spawn = (r: spawnRow, c: BOARD_SIZE - 1);
            int pathsAvailable = 0;

            for (int goalRow = 0; goalRow < BOARD_SIZE; goalRow++)
            {
                var goal = (r: goalRow, c: 0);
                if (FindShortestPath(aiBoard, spawn, goal) != null)
                    pathsAvailable++;
            }

            if (pathsAvailable > maxPathsFromSpawn)
            {
                maxPathsFromSpawn = pathsAvailable;
                bestSpawnRow = spawnRow;
            }
        }

        // Find best goal row from the best spawn row
        var bestSpawn = (r: bestSpawnRow, c: BOARD_SIZE - 1);
        int shortestDistance = int.MaxValue;
        for (int goalRow = 0; goalRow < BOARD_SIZE; goalRow++)
        {
            var goal = (r: goalRow, c: 0);
            var path = FindShortestPath(aiBoard, bestSpawn, goal);
            if (path != null && path.Count < shortestDistance)
            {
                shortestDistance = path.Count;
                bestGoalRow = goalRow;
            }
        }

        var bestGoal = (r: bestGoalRow, c: 0);
        return (bestSpawn, bestGoal);
    }

    // =========================================
    // LCV + Corridor Strategy
    // =========================================
    public Placement ChooseNextPlacementLCV(
        (int r, int c) enemySpawn,
        (int r, int c) baseGoal,
        Func<Placement, bool> filter // lets you force "only towers" or "only walls"
    )
    {
        // Only consider placements that match the requested type (tower phase vs wall phase)
        List<Placement> candidates = GenerateAllLegalPlacements().FindAll(p => filter(p));
        if (candidates.Count == 0)
            return new Placement(PlacementType.WallH, -1, -1);

        Shuffle(candidates);

        // True LCV core: base number of future legal placements (we prefer candidates
        // that leave MORE future options = least constraining value). Keep the
        // corridor strategy by using viable lane count as a secondary objective.
        int baseFutureOptions = CountFutureOptions(aiBoard);
        int baseLanes = CountViableLanes(aiBoard);

        // Collect (candidate, futureOptions, laneCount, benefit)
        var scored = new List<(Placement cand, int futureOptions, int laneCount, int benefit)>();

        foreach (var cand in candidates)
        {
            int[,] trial = CopyBoard(aiBoard);
            if (!ApplyPlacement(trial, cand))
                continue;

            int laneCount = CountViableLanes(trial); // Corridor metric: how many lanes remain?
            int futureOptions = CountFutureOptions(trial); // True LCV metric

            // Compute benefit heuristic
            int benefit = 0;

            // Walls: bonus for reducing lanes
            if (cand.Type == PlacementType.WallH || cand.Type == PlacementType.WallV)
            {
                // Enforce constraint: always leave at least 2 viable lanes open
                if (laneCount < 2)
                    continue;  // Skip placements that reduce lanes below 2

                int laneReduction = baseLanes - laneCount;
                benefit = laneReduction * 100;  // strong bonus for squeezing lanes

                // Bonus for being on right side (near spawn)
                int wallMidCol = cand.Type == PlacementType.WallH ? cand.Col + WALL_LEN / 2 : cand.Col;
                if (wallMidCol >= BOARD_SIZE / 2)
                    benefit += 50;
            }
            // Towers: place them to defend the remaining lane(s)
            else if (cand.Type == PlacementType.ArcherTower)
            {
                // Find ALL viable paths through the remaining lanes
                var allPaths = new List<List<(int r, int c)>>();
                for (int spawnRow = 0; spawnRow < BOARD_SIZE; spawnRow++)
                {
                    var spawn = (r: spawnRow, c: BOARD_SIZE - 1);
                    for (int goalRow = 0; goalRow < BOARD_SIZE; goalRow++)
                    {
                        var goal = (r: goalRow, c: 0);
                        var p = FindShortestPath(trial, spawn, goal);
                        if (p != null)
                        {
                            allPaths.Add(p);
                            break;  // One path per spawn row is enough
                        }
                    }
                }

                if (allPaths.Count > 0)
                {
                    // Find distance to CLOSEST path
                    int minDistToAnyPath = int.MaxValue;
                    foreach (var path in allPaths)
                    {
                        int dist = FindMinDistanceToPath(path, cand.Row, cand.Col);
                        if (dist < minDistToAnyPath)
                            minDistToAnyPath = dist;
                    }
                    
                    int distToPath = minDistToAnyPath;
                    
                    // PRIMARY: Distance to nearest viable path
                    if (distToPath == 1 || distToPath == 2) benefit = 400;  // Optimal distance
                    else if (distToPath == 0) benefit = 250;  // On path
                    else if (distToPath == 3) benefit = 150;  // Still close
                    else benefit = 30;                        // Far from path
                    
                    // SECONDARY: Path tile coverage - check coverage across ALL paths
                    int totalTilesCovered = 0;
                    int pathsWithCoverage = 0;
                    foreach (var path in allPaths)
                    {
                        int tilesInRange = CountPathTilesCoveredByTower(path, cand.Row, cand.Col, range: 3);
                        if (tilesInRange > 0)
                        {
                            pathsWithCoverage++;
                            totalTilesCovered += tilesInRange;
                        }
                    }
                    
                    if (totalTilesCovered >= 3) benefit += 120;
                    else if (totalTilesCovered >= 2) benefit += 60;
                    else if (totalTilesCovered >= 1) benefit += 30;
                    else benefit -= 100;  // Penalize towers that cover no paths
                    
                    // BONUS: If this tower covers a DIFFERENT path than others, reward it for diversity
                    if (allPaths.Count > 1 && pathsWithCoverage > 0)
                    {
                        benefit += 150;  // Encourages multi-path defense
                    }
                    
                    // If only 1 lane exists, place ALL towers along it
                    if (laneCount == 1)
                    {
                        if (distToPath <= 2) benefit += 300;
                    }
                    
                    // TERTIARY: Right-side bonus (encourages towers toward spawn)
                    if (cand.Col >= BOARD_SIZE / 2) benefit += 40;
                }
                else
                {
                    benefit = -100;  // tower is useless if no paths remain
                }
            }

            scored.Add((cand, futureOptions, laneCount, benefit));
        }

        if (scored.Count == 0)
            return new Placement(PlacementType.WallH, -1, -1);

        // Lexicographic: PRIMARY = futureOptions (maximize = least constraining),
        // SECONDARY = laneCount (minimize to keep corridor strategy),
        // TERTIARY = benefit (maximize)
        scored.Sort((a, b) =>
        {
            int cmp = b.futureOptions.CompareTo(a.futureOptions); // prefer MORE future options
            if (cmp != 0) return cmp;
            cmp = a.laneCount.CompareTo(b.laneCount); // prefer FEWER lanes
            if (cmp != 0) return cmp;
            return b.benefit.CompareTo(a.benefit); // tie-break: prefer higher benefit
        });

        // Pick from top 3 for variety
        int topPickK = Math.Min(3, scored.Count);
        int pickIdx = rng.Next(topPickK);
        
        return scored[pickIdx].cand;
    }

    // Count archer towers adjacent to a wall position
    private int CountAdjacentArcherTowers(int[,] board, int row, int col, PlacementType wallType)
    {
        int count = 0;
        
        // Check all positions within 1-2 tiles of the wall
        int minR = Math.Max(0, row - 2);
        int maxR = Math.Min(BOARD_SIZE - 1, row + WALL_LEN + 1);
        int minC = Math.Max(0, col - 2);
        int maxC = Math.Min(BOARD_SIZE - 1, col + WALL_LEN + 1);

        for (int r = minR; r <= maxR; r++)
        {
            for (int c = minC; c <= maxC; c++)
            {
                // Check for archer tower (2x2 block of 1s)
                if (r + 1 < BOARD_SIZE && c + 1 < BOARD_SIZE &&
                    board[r, c] == 1 && board[r, c + 1] == 1 &&
                    board[r + 1, c] == 1 && board[r + 1, c + 1] == 1)
                {
                    count++;
                }
            }
        }

        return count;
    }

    // =========================================
    // Candidate generation (all legal placements)
    // =========================================
    private List<Placement> GenerateAllLegalPlacements()
    {
        var list = new List<Placement>();

        bool canPlaceMoreTowers = totalArcherTowersPlaced < MAX_ARCHER_TOWERS;
        bool canPlaceMoreWalls = totalWallsPlaced < MAX_WALLS;

        if (canPlaceMoreTowers)
        {
            for (int r = 0; r <= BOARD_SIZE - ARCHER_TOWER_SIZE; r++)
                for (int c = 0; c <= BOARD_SIZE - ARCHER_TOWER_SIZE; c++)
                    if (CanPlaceArcherTower(aiBoard, r, c))
                        list.Add(new Placement(PlacementType.ArcherTower, r, c));
        }

        if (canPlaceMoreWalls)
        {
            for (int r = 0; r < BOARD_SIZE; r++)
                for (int c = 0; c < BOARD_SIZE; c++)
                {
                    if (CanPlaceWall(aiBoard, r, c, true))
                        list.Add(new Placement(PlacementType.WallH, r, c));
                    if (CanPlaceWall(aiBoard, r, c, false))
                        list.Add(new Placement(PlacementType.WallV, r, c));
                }
        }

        return list;
    }

    // =========================================
    // LCV: count future legal placements
    // =========================================
    private int CountFutureOptions(int[,] board)
    {
        int count = 0;

        for (int r = 0; r <= BOARD_SIZE - ARCHER_TOWER_SIZE; r++)
            for (int c = 0; c <= BOARD_SIZE - ARCHER_TOWER_SIZE; c++)
                if (CanPlaceArcherTower(board, r, c)) count++;

        for (int r = 0; r < BOARD_SIZE; r++)
            for (int c = 0; c < BOARD_SIZE; c++)
            {
                if (CanPlaceWall(board, r, c, true)) count++;
                if (CanPlaceWall(board, r, c, false)) count++;
            }

        return count;
    }

    // =========================================
    // Corridor LCV: count viable enemy lanes
    // =========================================
    private int CountViableLanes(int[,] board)
    {
        // Count how many spawn rows can reach at least one goal row
        int viableLanes = 0;

        for (int spawnRow = 0; spawnRow < BOARD_SIZE; spawnRow++)
        {
            var spawn = (r: spawnRow, c: BOARD_SIZE - 1);
            bool canReachGoal = false;

            for (int goalRow = 0; goalRow < BOARD_SIZE; goalRow++)
            {
                var goal = (r: goalRow, c: 0);
                if (FindShortestPath(board, spawn, goal) != null)
                {
                    canReachGoal = true;
                    break;
                }
            }

            if (canReachGoal)
                viableLanes++;
        }

        return viableLanes;
    }

    // =========================================
    // Pathfinding (BFS shortest path)
    // =========================================
    private List<(int r, int c)> FindShortestPath(int[,] board, (int r, int c) start, (int r, int c) goal)
    {
        bool InBounds(int r, int c) => r >= 0 && r < BOARD_SIZE && c >= 0 && c < BOARD_SIZE;
        bool Walkable(int r, int c) => board[r, c] != 2;  // Only walls (2) block paths, not towers (1)

        if (!InBounds(start.r, start.c) || !InBounds(goal.r, goal.c)) return null;
        if (!Walkable(start.r, start.c) || !Walkable(goal.r, goal.c)) return null;

        var prev = new (int r, int c)?[BOARD_SIZE, BOARD_SIZE];
        var q = new Queue<(int r, int c)>();

        q.Enqueue(start);
        prev[start.r, start.c] = start;

        int[] dr = { -1, 1, 0, 0 };
        int[] dc = { 0, 0, -1, 1 };

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            if (cur == goal) break;

            for (int k = 0; k < 4; k++)
            {
                int nr = cur.r + dr[k], nc = cur.c + dc[k];
                if (!InBounds(nr, nc) || !Walkable(nr, nc)) continue;
                if (prev[nr, nc].HasValue) continue;

                prev[nr, nc] = cur;
                q.Enqueue((nr, nc));
            }
        }

        if (!prev[goal.r, goal.c].HasValue) return null;

        var path = new List<(int r, int c)>();
        var p = goal;
        while (true)
        {
            path.Add(p);
            var pr = prev[p.r, p.c].Value;
            if (pr == p) break;
            p = pr;
        }
        path.Reverse();
        return path;
    }

    // =========================================
    // Tower range benefit
    // =========================================
    private int CountPathTilesCoveredByTower(List<(int r, int c)> path, int towerRow, int towerCol, int range)
    {
        int covered = 0;

        foreach (var tile in path)
        {
            int best = int.MaxValue;

            for (int r = towerRow; r < towerRow + 2; r++)
                for (int c = towerCol; c < towerCol + 2; c++)
                {
                    int cheb = Math.Max(Math.Abs(tile.r - r), Math.Abs(tile.c - c));
                    if (cheb < best) best = cheb;
                }

            if (best <= range) covered++;
        }

        return covered;
    }

    // =========================================
    // Placement checks / apply
    // =========================================
    private bool ApplyPlacement(int[,] board, Placement p)
    {
        return p.Type switch
        {
            PlacementType.ArcherTower => PlaceArcherTowerOn(board, p.Row, p.Col),
            PlacementType.WallH => PlaceWallOn(board, p.Row, p.Col, true),
            PlacementType.WallV => PlaceWallOn(board, p.Row, p.Col, false),
            _ => false
        };
    }

    private void IncrementPlacementCount(Placement p)
    {
        if (p.Type == PlacementType.ArcherTower)
            totalArcherTowersPlaced++;
        else
            totalWallsPlaced++;
    }

    private bool CanPlaceArcherTower(int[,] board, int row, int col)
    {
        if (row + ARCHER_TOWER_SIZE > BOARD_SIZE || col + ARCHER_TOWER_SIZE > BOARD_SIZE)
            return false;

        for (int r = row; r < row + ARCHER_TOWER_SIZE; r++)
            for (int c = col; c < col + ARCHER_TOWER_SIZE; c++)
                if (board[r, c] != 0) return false;

        return true;
    }

    private bool PlaceArcherTowerOn(int[,] board, int row, int col)
    {
        if (!CanPlaceArcherTower(board, row, col)) return false;

        for (int r = row; r < row + ARCHER_TOWER_SIZE; r++)
            for (int c = col; c < col + ARCHER_TOWER_SIZE; c++)
                board[r, c] = 1;

        return true;
    }

    private bool CanPlaceWall(int[,] board, int row, int col, bool isHorizontal)
    {
        if (isHorizontal)
        {
            if (row >= BOARD_SIZE || col + WALL_LEN > BOARD_SIZE) return false;
            for (int c = col; c < col + WALL_LEN; c++)
                if (board[row, c] != 0) return false;
            return true;
        }
        else
        {
            if (row + WALL_LEN > BOARD_SIZE || col >= BOARD_SIZE) return false;
            for (int r = row; r < row + WALL_LEN; r++)
                if (board[r, col] != 0) return false;
            return true;
        }
    }

    private bool PlaceWallOn(int[,] board, int row, int col, bool isHorizontal)
    {
        if (!CanPlaceWall(board, row, col, isHorizontal)) return false;

        if (isHorizontal)
            for (int c = col; c < col + WALL_LEN; c++)
                board[row, c] = 2;
        else
            for (int r = row; r < row + WALL_LEN; r++)
                board[r, col] = 2;

        return true;
    }

    // Find minimum distance from candidate tower to any tile on a given path
    private int FindMinDistanceToPath(List<(int r, int c)> path, int towerRow, int towerCol)
    {
        int minDist = int.MaxValue;

        // For each tile in the path, calculate Chebyshev distance to tower
        foreach (var (pathR, pathC) in path)
        {
            // Check distance to any cell of the 2x2 tower
            for (int r = towerRow; r < towerRow + 2; r++)
            {
                for (int c = towerCol; c < towerCol + 2; c++)
                {
                    int cheb = Math.Max(Math.Abs(pathR - r), Math.Abs(pathC - c));
                    if (cheb < minDist)
                        minDist = cheb;
                }
            }
        }

        return minDist == int.MaxValue ? 100 : minDist;  // Return high distance if no path
    }

    // Find minimum distance from candidate tower to any existing tower
    private int FindMinDistanceToExistingTower(int[,] board, int newTowerRow, int newTowerCol)
    {
        int minDist = int.MaxValue;

        // Scan board for existing towers (2x2 blocks of 1s)
        for (int r = 0; r < BOARD_SIZE - 1; r++)
        {
            for (int c = 0; c < BOARD_SIZE - 1; c++)
            {
                if (board[r, c] == 1 && board[r, c + 1] == 1 &&
                    board[r + 1, c] == 1 && board[r + 1, c + 1] == 1)
                {
                    // Found existing tower at (r, c) - calculate distance to new tower
                    int existingTowerRow = r;
                    int existingTowerCol = c;
                    
                    int dist = Math.Max(
                        Math.Abs(newTowerRow - existingTowerRow),
                        Math.Abs(newTowerCol - existingTowerCol)
                    );
                    
                    if (dist < minDist)
                        minDist = dist;
                }
            }
        }

        return minDist;
    }

    // Public getter for last predicted spawn/goal
    public ((int r, int c) spawn, (int r, int c) goal) GetLastPredictedPath()
    {
        return (lastPredictedSpawn, lastPredictedGoal);
    }

    private int[,] CopyBoard(int[,] src)
    {
        int[,] dst = new int[BOARD_SIZE, BOARD_SIZE];
        for (int r = 0; r < BOARD_SIZE; r++)
            for (int c = 0; c < BOARD_SIZE; c++)
                dst[r, c] = src[r, c];
        return dst;
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
