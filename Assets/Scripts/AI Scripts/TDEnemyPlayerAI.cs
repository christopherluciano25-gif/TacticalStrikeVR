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
        bool[,] processed = new bool[9, 9];  // Track processed cells to avoid double-placing towers
        int placedCount = 0;
        
        for (int r = 0; r < 9; r++)
        {
            for (int c = 0; c < 9; c++)
            {
                if (board[r, c] == 0 || processed[r, c])
                    continue;

                // Handle archer towers (only place at top-left corner of 2x2)
                if (board[r, c] == 1)
                {
                    // Check if this is the top-left corner of a complete 2x2 tower
                    if (r + 1 < 9 && c + 1 < 9 && 
                        board[r, c] == 1 && board[r, c+1] == 1 && 
                        board[r+1, c] == 1 && board[r+1, c+1] == 1)
                    {
                        // Only instantiate at the top-left corner
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
                        
                        GameObject instance = Instantiate(archerTowerBlockPrefab, cell.worldPosition, Quaternion.identity);
                        cell.Occupy(instance);
                        placedCount++;
                        
                        // Mark all 4 cells as processed
                        processed[r, c] = true;
                        processed[r, c+1] = true;
                        processed[r+1, c] = true;
                        processed[r+1, c+1] = true;
                    }
                    continue;
                }

                // Handle walls (place one per cell)
                if (board[r, c] == 2)
                {
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
                    
                    GameObject instance = Instantiate(wallBlockPrefab, cell.worldPosition, Quaternion.identity);
                    cell.Occupy(instance);
                    placedCount++;
                    processed[r, c] = true;
                }
            }
        }
        Debug.Log($"AI placed {placedCount} blocks on the grid");
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
    // PUBLIC: Run one AI turn (varies per turn)
    // =========================================
    // To force different layouts, the enemy spawn row (east edge)
    // and goal row (west edge) are randomized each turn (wave).
    public void DoAITurn()
    {
        int spawnRow = rng.Next(0, BOARD_SIZE);
        int goalRow = rng.Next(0, BOARD_SIZE);

        var spawn = (r: spawnRow, c: BOARD_SIZE - 1);
        var goal = (r: goalRow, c: 0);

        // Place all towers and walls in a single turn
        while (totalArcherTowersPlaced < MAX_ARCHER_TOWERS || totalWallsPlaced < MAX_WALLS)
        {
            Placement choice = ChooseNextPlacementLCV(spawn, goal);

            if (choice.Row == -1)
            {
                Debug.Log("AI: No valid placement found.");
                break;
            }

            ApplyPlacement(aiBoard, choice);
            IncrementPlacementCount(choice);

            Debug.Log($"AI placed: {choice} | Towers: {totalArcherTowersPlaced}/{MAX_ARCHER_TOWERS}, Walls: {totalWallsPlaced}/{MAX_WALLS}");
        }
    }

    // =========================================
    // LCV + usefulness + variety (works)
    // =========================================
    public Placement ChooseNextPlacementLCV((int r, int c) enemySpawn, (int r, int c) baseGoal)
    {
        double epsilon = 0.25;  // 25% pick from topK (more variety)
        int topK = 5;           // consider top 5 moves for exploration
        int jitterRange = 3;    // breaks near-ties without overriding scoring

        List<Placement> candidates = GenerateAllLegalPlacements();
        if (candidates.Count == 0)
            return new Placement(PlacementType.WallH, -1, -1);

        // Shuffle to avoid scan-order bias
        Shuffle(candidates);

        // Baseline: how constrained is the current board?
        int baseFutureCount = CountFutureOptions(aiBoard);

        // Baseline: how many independent right-edge -> left-edge paths exist (fewer is better)
        int baseSpawnPaths = CountSpawnPaths(aiBoard);

        // Baseline: how many tower cells are reachable from the right edge (fewer => better protected)
        int baseTowerReach = CountTowerReachableFromRight(aiBoard);

        // Baseline path length (for wall detour benefit)
        var basePath = FindShortestPath(aiBoard, enemySpawn, baseGoal);
        int baseLen = basePath?.Count ?? 9999;

        List<(Placement move, int score)> scored = new List<(Placement, int)>();

        foreach (var cand in candidates)
        {
            int[,] trial = CopyBoard(aiBoard);
            if (!ApplyPlacement(trial, cand)) continue;

            // Must keep a valid path: do not completely block
            var path = FindShortestPath(trial, enemySpawn, baseGoal);
            if (path == null) continue;

            // LCV cost: how many future options does this remove?
            int futureCount = CountFutureOptions(trial);
            int constraintCost = baseFutureCount - futureCount; // lower is better

            // How many spawn paths remain after this placement
            int newSpawnPaths = CountSpawnPaths(trial);
            int spawnPathReduction = baseSpawnPaths - newSpawnPaths;

            // How many tower cells are reachable from the right after this placement
            int newTowerReach = CountTowerReachableFromRight(trial);
            int towerReachReduction = baseTowerReach - newTowerReach;

            // Benefit scoring
            int benefit = 0;

            if (cand.Type == PlacementType.ArcherTower)
            {
                // Reward tower placement: attract enemies by blocking their path
                int pathTilesCovered = CountPathTilesCoveredByTower(path, cand.Row, cand.Col, range: 3);
                benefit = pathTilesCovered * 15;

                // Bonus: towers closer to the goal are more valuable
                int distToGoal = Math.Min(Math.Abs(cand.Row - baseGoal.r), Math.Abs(cand.Col - baseGoal.c));
                benefit += Math.Max(0, (5 - distToGoal) * 5);
            }
            else
            {
                // Wall placement: prefer walls that actually affect enemy movement

                // Use the trial board (with the wall placed) when evaluating adjacency
                int adjacentTowers = CountAdjacentTowers(trial, cand.Row, cand.Col, cand.Type);

                // How much does this wall increase path length (detour benefit)?
                int newLen = path.Count;
                int detourBenefit = Math.Max(0, newLen - baseLen) * 12;

                // How many path tiles does the wall overlap (or sit on)?
                int pathOverlap = 0;
                var pathSet = new HashSet<(int r, int c)>(path);
                if (cand.Type == PlacementType.WallH)
                {
                    for (int cc = cand.Col; cc < cand.Col + WALL_LEN; cc++)
                        if (pathSet.Contains((cand.Row, cc))) pathOverlap++;
                }
                else
                {
                    for (int rr = cand.Row; rr < cand.Row + WALL_LEN; rr++)
                        if (pathSet.Contains((rr, cand.Col))) pathOverlap++;
                }

                // Combine heuristics: protect towers, overlap path, and increase detour
                benefit = adjacentTowers * 18 + pathOverlap * 30 + detourBenefit + futureCount * 2;
            }

            int totalScore = benefit - (constraintCost * 3);

            // Tiny jitter to create variety among near-equal moves
            int totalScoreJittered = totalScore * 10 + rng.Next(-jitterRange, jitterRange + 1);

            scored.Add((cand, totalScoreJittered));
        }

        if (scored.Count == 0)
            return new Placement(PlacementType.WallH, -1, -1);

        // Sort by score desc
        scored.Sort((a, b) => b.score.CompareTo(a.score));

        // Explore: randomly pick from the top K
        if (rng.NextDouble() < epsilon)
        {
            int k = Math.Min(topK, scored.Count);
            return scored[rng.Next(k)].move;
        }

        // Exploit: pick randomly among the best-score ties
        int bestScore = scored[0].score;
        List<Placement> bestTies = new List<Placement>();
        for (int i = 0; i < scored.Count; i++)
        {
            if (scored[i].score == bestScore) bestTies.Add(scored[i].move);
            else break;
        }

        return bestTies[rng.Next(bestTies.Count)];
    }

    // =========================================
    // Candidate generation
    // =========================================
    private List<Placement> GenerateAllLegalPlacements()
    {
        var list = new List<Placement>();

        bool canPlaceMoreTowers = totalArcherTowersPlaced < MAX_ARCHER_TOWERS;
        bool canPlaceMoreWalls = totalWallsPlaced < MAX_WALLS;
        // Towers 2x2
        if (canPlaceMoreTowers)
        {
            for (int r = 0; r <= BOARD_SIZE - ARCHER_TOWER_SIZE; r++)
            {
                for (int c = 0; c <= BOARD_SIZE - ARCHER_TOWER_SIZE; c++)
                {
                    if (CanPlaceArcherTower(aiBoard, r, c))
                        list.Add(new Placement(PlacementType.ArcherTower, r, c));
                }
            }
        }

        // Walls 1x4 and 4x1
        if (canPlaceMoreWalls)
        {
            for (int r = 0; r < BOARD_SIZE; r++)
            {
                for (int c = 0; c < BOARD_SIZE; c++)
                {
                    if (CanPlaceWall(aiBoard, r, c, isHorizontal: true))
                        list.Add(new Placement(PlacementType.WallH, r, c));
                    if (CanPlaceWall(aiBoard, r, c, isHorizontal: false))
                        list.Add(new Placement(PlacementType.WallV, r, c));
                }
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

        // Tower placements
        for (int r = 0; r <= BOARD_SIZE - ARCHER_TOWER_SIZE; r++)
            for (int c = 0; c <= BOARD_SIZE - ARCHER_TOWER_SIZE; c++)
                if (CanPlaceArcherTower(board, r, c)) count++;

        // Wall placements
        for (int r = 0; r < BOARD_SIZE; r++)
            for (int c = 0; c < BOARD_SIZE; c++)
            {
                if (CanPlaceWall(board, r, c, true)) count++;
                if (CanPlaceWall(board, r, c, false)) count++;
            }

        return count;
    }

    // =========================================
    // Pathfinding (BFS shortest path)
    // =========================================
    private List<(int r, int c)> FindShortestPath(int[,] board, (int r, int c) start, (int r, int c) goal)
    {
        bool InBounds(int r, int c) => r >= 0 && r < BOARD_SIZE && c >= 0 && c < BOARD_SIZE;

        // Movement rule:
        // - walls block movement (value 2)
        // - towers block movement (value 1) - enemies must navigate around them
        // - only empty cells are walkable
        bool Walkable(int r, int c) => board[r, c] == 0;

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
    // Count adjacent towers (for wall protection scoring)
    // =========================================
    private int CountAdjacentTowers(int[,] board, int wallRow, int wallCol, PlacementType wallType)
    {
        int count = 0;
        var adjacentCells = new List<(int r, int c)>();
        
        if (wallType == PlacementType.WallH)
        {
            // Horizontal wall: check cells above and below
            for (int c = wallCol; c < wallCol + WALL_LEN; c++)
            {
                if (wallRow > 0) adjacentCells.Add((wallRow - 1, c));
                if (wallRow < BOARD_SIZE - 1) adjacentCells.Add((wallRow + 1, c));
            }
        }
        else
        {
            // Vertical wall: check cells left and right
            for (int r = wallRow; r < wallRow + WALL_LEN; r++)
            {
                if (wallCol > 0) adjacentCells.Add((r, wallCol - 1));
                if (wallCol < BOARD_SIZE - 1) adjacentCells.Add((r, wallCol + 1));
            }
        }
        
        foreach (var (r, c) in adjacentCells)
        {
            if (board[r, c] == 1) count++;
        }
        
        return count;
    }

    // =========================================
    // Tower range benefit (range=3)
    // Chebyshev distance around 2x2 footprint
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
    // Count how many distinct right-edge spawn rows have a path to the left edge
    // Fewer such paths means fewer ways for enemies to reach the left (good)
    // =========================================
    private int CountSpawnPaths(int[,] board)
    {
        int count = 0;
        for (int r = 0; r < BOARD_SIZE; r++)
        {
            var start = (r, BOARD_SIZE - 1);
            bool found = false;
            for (int targetRow = 0; targetRow < BOARD_SIZE; targetRow++)
            {
                var goal = (targetRow, 0);
                if (FindShortestPath(board, start, goal) != null)
                {
                    found = true; break;
                }
            }
            if (found) count++;
        }
        return count;
    }

    // =========================================
    // Count how many tower cells (any 1s) are reachable from any right-edge spawn
    // Lower is better (walls protecting towers)
    // =========================================
    private int CountTowerReachableFromRight(int[,] board)
    {
        var reachable = new HashSet<(int r, int c)>();

        for (int r = 0; r < BOARD_SIZE; r++)
        {
            var start = (r, BOARD_SIZE - 1);
            for (int tr = 0; tr < BOARD_SIZE; tr++)
                for (int tc = 0; tc < BOARD_SIZE; tc++)
                {
                    if (board[tr, tc] != 1) continue;
                    if (FindShortestPath(board, start, (tr, tc)) != null)
                        reachable.Add((tr, tc));
                }
        }

        return reachable.Count;
    }

    // =========================================
    // Placement checks / apply
    // =========================================
    private bool ApplyPlacement(int[,] board, Placement p)
    {
        return p.Type switch
        {
            PlacementType.ArcherTower => PlaceArcherTowerOn(board, p.Row, p.Col),
            PlacementType.WallH => PlaceWallOn(board, p.Row, p.Col, isHorizontal: true),
            PlacementType.WallV => PlaceWallOn(board, p.Row, p.Col, isHorizontal: false),
            _ => false
        };
    }
    private void IncrementPlacementCount(Placement p)
    {
        if (p.Type == PlacementType.ArcherTower)
            totalArcherTowersPlaced++;
        else
            totalWallsPlaced++; // WallH or WallV
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
        {
            for (int c = col; c < col + WALL_LEN; c++)
                board[row, c] = 2;
        }
        else
        {
            for (int r = row; r < row + WALL_LEN; r++)
                board[r, col] = 2;
        }

        return true;
    }

    // =========================================
    // Manual placement (optional)
    // =========================================
    public bool PlaceArcherTower(int row, int col) => PlaceArcherTowerOn(aiBoard, row, col);
    public bool PlaceWall(int row, int col, bool isHorizontal) => PlaceWallOn(aiBoard, row, col, isHorizontal);

    // =========================================
    // Printing
    // =========================================
    public void PrintBoards()
    {
        Console.WriteLine("\n" + new string(' ', 5) + "AI BOARD" + new string(' ', 5) + "DEADZONE" + new string(' ', 5) + "USER BOARD");
        Console.WriteLine("  0 1 2 3 4 5 6 7 8" + new string(' ', 5) + "  0 1 2 3 4 5 6 7 8");

        for (int r = 0; r < BOARD_SIZE; r++)
        {
            Console.Write(r + " ");
            for (int c = 0; c < BOARD_SIZE; c++)
            {
                char symbol = aiBoard[r, c] switch
                {
                    0 => '.',
                    1 => 'A',
                    2 => 'W',
                    _ => '?'
                };
                Console.Write(symbol + " ");
            }

            Console.Write(new string(' ', DEADZONE_WIDTH * 2 + 1));

            Console.Write(r + " ");
            for (int c = 0; c < BOARD_SIZE; c++)
            {
                char symbol = userBoard[r, c] switch
                {
                    0 => '.',
                    1 => 'A',
                    2 => 'W',
                    _ => '?'
                };
                Console.Write(symbol + " ");
            }
            Console.WriteLine();
        }
    }

    // =========================================
    // Utilities
    // =========================================
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
}
