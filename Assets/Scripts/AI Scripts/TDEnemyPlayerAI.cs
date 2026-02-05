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
        int placedCount = 0;
        for (int r = 0; r < 9; r++)
        {
            for (int c = 0; c < 9; c++)
            {
                if (board[r, c] != 0)
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
                    GameObject prefab = board[r, c] == 1 ? archerTowerBlockPrefab : wallBlockPrefab;
                    GameObject instance = Instantiate(prefab, cell.worldPosition, Quaternion.identity);
                    cell.Occupy(instance);
                    placedCount++;
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

        Placement choice = ChooseNextPlacementLCV(spawn, goal);

        if (choice.Row == -1)
        {
            Debug.Log("AI: No valid placement found.");
            return;
        }

        ApplyPlacement(aiBoard, choice);
        IncrementPlacementCount(choice);

        Debug.Log($"AI placed: {choice} | Towers: {totalArcherTowersPlaced}/{MAX_ARCHER_TOWERS}, Walls: {totalWallsPlaced}/{MAX_WALLS}");
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

            // Benefit scoring
            int benefit = 0;

            if (cand.Type == PlacementType.WallH || cand.Type == PlacementType.WallV)
            {
                // Reward LCV: how many options are left after this move
                benefit = futureCount * 5;
            }
            if (cand.Type == PlacementType.ArcherTower)
            {
                // Reward shooting coverage: how much of the path is in range 3
                benefit = CountPathTilesCoveredByTower(path, cand.Row, cand.Col, range: 3) * 10;
            }
            else
            {
                // Reward detours: longer path is better (but only if path still exists)
                int newLen = path.Count;
                benefit = Math.Max(0, newLen - baseLen) * 8;
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
        // - walls block movement
        // - towers do NOT block movement (typical tower defense)
        // If you want towers to block too, change to: board[r,c] == 0
        bool Walkable(int r, int c) => board[r, c] != 2;

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
