using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Knight - Melee unit that moves and attacks
/// STRONG against: Bombers (2x damage)
/// WEAK against: Archers (takes 2x damage)
/// Uses A* pathfinding to navigate around walls
/// </summary>
public class Knight : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float attackDamage = 15f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackCooldown = 1f;

    [Header("Team")]
    public TeamSide team = TeamSide.Player;

    [Header("References")]
    [SerializeField] private TacticalGrid grid;

    // State
    private float currentHealth;
    private bool isDead = false;
    private GameObject currentTarget = null;
    private float lastAttackTime = 0f;
    private Vector2Int gridPosition;

    // Pathfinding
    private List<Vector2Int> currentPath = new List<Vector2Int>();
    private int pathIndex = 0;
    private float pathfindingCooldown = 0.5f;
    private float lastPathfindTime = 0f;

    // Combat multipliers
    private const float STRONG_MULTIPLIER = 2f;
    private const float WEAK_MULTIPLIER = 2f;

    private void Start()
    {
        Debug.Log("=== KNIGHT START ===");
        currentHealth = maxHealth;

        if (grid == null)
        {
            grid = FindObjectOfType<TacticalGrid>();
            Debug.Log($"[Knight] {team} Auto-found grid: {(grid != null ? "SUCCESS" : "FAILED")}");
        }
        else
        {
            Debug.Log($"[Knight] {team} Grid already assigned");
        }

        UpdateGridPosition();
        Debug.Log($"[Knight] {team} Initial grid position: {gridPosition}");

        // Register with UnitManager
        if (UnitManager.Instance != null)
        {
            UnitManager.Instance.RegisterKnight(this);
            Debug.Log($"[Knight] {team} Registered with UnitManager");
        }
        else
        {
            Debug.LogError($"[Knight] {team} UnitManager.Instance is NULL! Create UnitManager GameObject!");
        }

        Debug.Log($"[Knight] {team} Starting AI Loop...");
        StartCoroutine(AILoop());
    }

    private void OnDestroy()
    {
        if (UnitManager.Instance != null)
        {
            UnitManager.Instance.UnregisterKnight(this);
        }
    }

    private void UpdateGridPosition()
    {
        if (grid != null)
        {
            Vector2Int newPos = grid.WorldToGrid(transform.position);

            if (newPos != gridPosition && grid.IsValidGridPosition(newPos))
            {
                GridCell newCell = grid.GetCell(newPos);
                if (newCell != null && newCell.IsWalkable())
                {
                    grid.MoveUnit(this.gameObject, gridPosition, newPos);
                    gridPosition = newPos;
                }
            }
        }
    }

    /// <summary>
    /// Main AI loop
    /// </summary>
    private IEnumerator AILoop()
    {
        Debug.Log($"[Knight] {team} AI LOOP STARTED at position {transform.position}!");

        while (!isDead)
        {
            Debug.Log($"[Knight] {team} AI tick - Current HP: {currentHealth}/{maxHealth}");

            // Find nearest enemy by PATHFINDING distance
            currentTarget = FindNearestEnemyByPathfinding();

            if (currentTarget != null)
            {
                Debug.Log($"[Knight] {team} FOUND TARGET: {currentTarget.name} at {currentTarget.transform.position}");

                float distance = Vector3.Distance(transform.position, currentTarget.transform.position);
                Debug.Log($"[Knight] {team} Distance to target: {distance:F2} (attack range: {attackRange})");

                if (distance <= attackRange)
                {
                    currentPath.Clear();
                    Debug.Log($"[Knight] {team} IN ATTACK RANGE! Attacking...");

                    if (Time.time >= lastAttackTime + attackCooldown)
                    {
                        Attack(currentTarget);
                        lastAttackTime = Time.time;
                        Debug.Log($"[Knight] {team} ATTACKED {currentTarget.name}!");
                    }
                    else
                    {
                        Debug.Log($"[Knight] {team} Attack on cooldown ({Time.time - lastAttackTime:F2}s / {attackCooldown}s)");
                    }
                }
                else
                {
                    Debug.Log($"[Knight] {team} TOO FAR - Moving along path (path length: {currentPath.Count})");
                    MoveAlongPath();
                }
            }
            else
            {
                Debug.Log($"[Knight] {team} NO TARGET FOUND - Moving towards enemy base");
                MoveTowardsEnemyBase();
            }

            yield return new WaitForSeconds(0.1f);
        }

        Debug.Log($"[Knight] {team} AI LOOP ENDED (dead)");
    }

    /// <summary>
    /// Find nearest enemy using pathfinding distance
    /// </summary>
    private GameObject FindNearestEnemyByPathfinding()
    {
        Debug.Log($"[Knight] {team} === FINDING ENEMIES ===");

        if (UnitManager.Instance == null)
        {
            Debug.LogError($"[Knight] {team} UnitManager.Instance is NULL!");
            return null;
        }

        if (grid == null)
        {
            Debug.LogError($"[Knight] {team} Grid is NULL!");
            return null;
        }

        List<GameObject> enemies = UnitManager.Instance.GetEnemiesForTeam(team);
        Debug.Log($"[Knight] {team} UnitManager returned {enemies.Count} enemies for team {team}");

        if (enemies.Count == 0)
        {
            Debug.LogWarning($"[Knight] {team} No enemies in UnitManager!");
            return null;
        }

        GameObject nearestEnemy = null;
        int shortestPathLength = int.MaxValue;

        foreach (GameObject enemy in enemies)
        {
            if (enemy == null)
            {
                Debug.LogWarning($"[Knight] {team} Found NULL enemy in list");
                continue;
            }

            Debug.Log($"[Knight] {team} Checking enemy: {enemy.name} at {enemy.transform.position}");

            Vector2Int enemyGridPos = grid.WorldToGrid(enemy.transform.position);
            Debug.Log($"[Knight] {team} Enemy grid pos: {enemyGridPos}");

            List<Vector2Int> path = FindPath(gridPosition, enemyGridPos);

            if (path != null && path.Count > 0)
            {
                Debug.Log($"[Knight] {team} Path to {enemy.name}: {path.Count} cells");

                if (path.Count < shortestPathLength)
                {
                    nearestEnemy = enemy;
                    shortestPathLength = path.Count;

                    if (Time.time >= lastPathfindTime + pathfindingCooldown)
                    {
                        currentPath = path;
                        pathIndex = 0;
                        lastPathfindTime = Time.time;
                        Debug.Log($"[Knight] {team} Stored new path to {enemy.name}");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[Knight] {team} No path found to {enemy.name}");
            }
        }

        if (nearestEnemy != null)
        {
            Debug.Log($"[Knight] {team} Nearest enemy: {nearestEnemy.name} (path length: {shortestPathLength})");
        }

        return nearestEnemy;
    }

    /// <summary>
    /// A* Pathfinding
    /// </summary>
    private List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
    {
        if (grid == null) return null;

        Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        Dictionary<Vector2Int, float> gScore = new Dictionary<Vector2Int, float>();
        Dictionary<Vector2Int, float> fScore = new Dictionary<Vector2Int, float>();

        List<Vector2Int> openSet = new List<Vector2Int>();
        HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();

        openSet.Add(start);
        gScore[start] = 0;
        fScore[start] = Heuristic(start, goal);

        Vector2Int[] directions = new Vector2Int[]
        {
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0)
        };

        int maxIterations = 500;
        int iterations = 0;

        while (openSet.Count > 0 && iterations < maxIterations)
        {
            iterations++;

            Vector2Int current = openSet[0];
            float lowestF = fScore.ContainsKey(current) ? fScore[current] : float.MaxValue;

            foreach (Vector2Int node in openSet)
            {
                float nodeF = fScore.ContainsKey(node) ? fScore[node] : float.MaxValue;
                if (nodeF < lowestF)
                {
                    current = node;
                    lowestF = nodeF;
                }
            }

            if (current == goal)
            {
                return ReconstructPath(cameFrom, current);
            }

            openSet.Remove(current);
            closedSet.Add(current);

            foreach (Vector2Int dir in directions)
            {
                Vector2Int neighbor = current + dir;

                if (closedSet.Contains(neighbor)) continue;
                if (!grid.IsValidGridPosition(neighbor)) continue;

                GridCell cell = grid.GetCell(neighbor);
                if (cell == null || !cell.IsWalkable()) continue;

                float tentativeGScore = gScore[current] + 1;

                if (!openSet.Contains(neighbor))
                {
                    openSet.Add(neighbor);
                }
                else if (gScore.ContainsKey(neighbor) && tentativeGScore >= gScore[neighbor])
                {
                    continue;
                }

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeGScore;
                fScore[neighbor] = gScore[neighbor] + Heuristic(neighbor, goal);
            }
        }

        return null;
    }

    private float Heuristic(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        List<Vector2Int> path = new List<Vector2Int> { current };

        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, current);
        }

        return path;
    }

    /// <summary>
    /// Move along calculated path
    /// </summary>
    private void MoveAlongPath()
    {
        if (currentPath == null || currentPath.Count == 0)
        {
            Debug.LogWarning($"[Knight] {team} MoveAlongPath called but path is empty!");
            return;
        }

        if (pathIndex >= currentPath.Count)
        {
            Debug.Log($"[Knight] {team} Reached end of path");
            currentPath.Clear();
            return;
        }

        Vector2Int targetGridPos = currentPath[pathIndex];
        Vector3 targetWorldPos = grid.GridToWorld(targetGridPos.x, targetGridPos.y);
        targetWorldPos.y = transform.position.y;

        Vector3 direction = (targetWorldPos - transform.position).normalized;

        Debug.Log($"[Knight] {team} Moving to waypoint {pathIndex}/{currentPath.Count}: {targetGridPos} (world: {targetWorldPos})");

        transform.position += direction * moveSpeed * Time.deltaTime;

        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(direction),
                Time.deltaTime * 5f
            );
        }

        float distToWaypoint = Vector3.Distance(transform.position, targetWorldPos);
        Debug.Log($"[Knight] {team} Distance to waypoint: {distToWaypoint:F2}");

        if (distToWaypoint < 0.5f)
        {
            pathIndex++;
            Debug.Log($"[Knight] {team} Reached waypoint! Moving to next ({pathIndex}/{currentPath.Count})");
        }

        UpdateGridPosition();
    }

    /// <summary>
    /// Move towards enemy base when no targets
    /// </summary>
    private void MoveTowardsEnemyBase()
    {
        Debug.Log($"[Knight] {team} === MOVING TO ENEMY BASE ===");

        if (UnitManager.Instance == null)
        {
            Debug.LogError($"[Knight] {team} UnitManager is NULL!");
            return;
        }

        Debug.Log($"[Knight] {team} Total bases in UnitManager: {UnitManager.Instance.allBases.Count}");

        BaseHealth enemyBase = null;
        foreach (BaseHealth baseHealth in UnitManager.Instance.allBases)
        {
            if (baseHealth == null)
            {
                Debug.LogWarning($"[Knight] {team} Found NULL base in list");
                continue;
            }

            Debug.Log($"[Knight] {team} Checking base: {baseHealth.name} owner: {baseHealth.owner} (my team: {team})");

            if (baseHealth.owner != team && baseHealth.owner != TeamSide.Neutral)
            {
                enemyBase = baseHealth;
                Debug.Log($"[Knight] {team} FOUND ENEMY BASE: {enemyBase.name}!");
                break;
            }
        }

        if (enemyBase == null)
        {
            Debug.LogError($"[Knight] {team} NO ENEMY BASE FOUND! Bases: {UnitManager.Instance.allBases.Count}");
            return;
        }

        Vector2Int baseGridPos = grid.WorldToGrid(enemyBase.transform.position);
        Debug.Log($"[Knight] {team} Enemy base grid position: {baseGridPos}");

        if (Time.time >= lastPathfindTime + pathfindingCooldown)
        {
            Debug.Log($"[Knight] {team} Calculating path to base from {gridPosition} to {baseGridPos}...");
            currentPath = FindPath(gridPosition, baseGridPos);

            if (currentPath != null && currentPath.Count > 0)
            {
                Debug.Log($"[Knight] {team} Path to base found! Length: {currentPath.Count}");
                pathIndex = 0;
                lastPathfindTime = Time.time;
            }
            else
            {
                Debug.LogError($"[Knight] {team} NO PATH TO BASE FOUND!");
            }
        }

        MoveAlongPath();
    }

    /// <summary>
    /// Attack target
    /// </summary>
    private void Attack(GameObject target)
    {
        if (target == null) return;

        float finalDamage = attackDamage;
        CombatUtils.ApplyDamage(target, finalDamage, this.gameObject);
        Debug.Log($"[Knight] {team} attacked {target.name} for {finalDamage} damage");
    }

    /// <summary>
    /// Take damage
    /// </summary>
    public void TakeDamage(float damage, GameObject attacker)
    {
        if (isDead) return;

        float finalDamage = damage;

        Archer archer = attacker.GetComponent<Archer>();
        if (archer != null)
        {
            finalDamage *= WEAK_MULTIPLIER;
            Debug.Log($"[Knight] WEAK! Taking extra damage from Archer! {finalDamage} total");
        }

        currentHealth -= finalDamage;
        Debug.Log($"[Knight] {team} took {finalDamage} damage. HP: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;
        Debug.Log($"[Knight] {team} died at {gridPosition}");

        if (grid != null)
        {
            grid.RemoveUnit(this.gameObject);
        }

        Destroy(gameObject, 0.1f);
    }

    private void OnDrawGizmos()
    {
        if (isDead) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (currentPath != null && currentPath.Count > 0 && grid != null)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < currentPath.Count - 1; i++)
            {
                Vector3 start = grid.GridToWorld(currentPath[i].x, currentPath[i].y);
                Vector3 end = grid.GridToWorld(currentPath[i + 1].x, currentPath[i + 1].y);
                Gizmos.DrawLine(start, end);
            }
        }

        if (currentTarget != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, currentTarget.transform.position);
        }
    }
}