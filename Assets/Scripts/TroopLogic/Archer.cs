using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Archer - Stationary ranged unit
/// STRONG against: Knights (2x damage)
/// WEAK against: Bombers (takes 2x damage)
/// Targets bases, knights, and other units
/// </summary>
public class Archer : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float maxHealth = 60f;
    [SerializeField] private float attackDamage = 20f;
    [SerializeField] private float attackRange = 8f;
    [SerializeField] private float attackCooldown = 1.5f;

    [Header("Projectile")]
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private Transform arrowSpawnPoint;
    [SerializeField] private float arrowSpeed = 15f;

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

    // Combat multipliers
    private const float STRONG_MULTIPLIER = 2f;  // vs Knights
    private const float WEAK_MULTIPLIER = 2f;    // from Bombers

    private void Start()
    {
        currentHealth = maxHealth;

        if (grid == null)
            grid = FindObjectOfType<TacticalGrid>();

        UpdateGridPosition();

        // Setup arrow spawn point
        if (arrowSpawnPoint == null)
        {
            GameObject spawnObj = new GameObject("ArrowSpawn");
            spawnObj.transform.SetParent(transform);
            spawnObj.transform.localPosition = Vector3.up * 1.5f;
            arrowSpawnPoint = spawnObj.transform;
        }

        // Register with UnitManager
        if (UnitManager.Instance != null)
        {
            UnitManager.Instance.RegisterArcher(this);
        }

        StartCoroutine(AILoop());
    }

    private void OnDestroy()
    {
        // Unregister from UnitManager
        if (UnitManager.Instance != null)
        {
            UnitManager.Instance.UnregisterArcher(this);
        }
    }

    private void UpdateGridPosition()
    {
        if (grid != null)
        {
            gridPosition = grid.WorldToGrid(transform.position);
        }
    }

    /// <summary>
    /// Main AI loop - archers are stationary, just rotate and shoot
    /// </summary>
    private IEnumerator AILoop()
    {
        while (!isDead)
        {
            // Find nearest enemy in range using UnitManager
            currentTarget = FindNearestEnemyInRange();

            if (currentTarget != null)
            {
                // Rotate to face target
                FaceTarget(currentTarget.transform.position);

                // Attack if cooldown ready
                if (Time.time >= lastAttackTime + attackCooldown)
                {
                    ShootArrow(currentTarget);
                    lastAttackTime = Time.time;
                }
            }

            yield return new WaitForSeconds(0.2f);
        }
    }

    /// <summary>
    /// Find nearest enemy within attack range
    /// Prioritize: Knights > Bases > Other
    /// </summary>
    private GameObject FindNearestEnemyInRange()
    {
        if (UnitManager.Instance == null) return null;

        List<GameObject> enemies = UnitManager.Instance.GetEnemiesForTeam(team);

        GameObject nearestEnemy = null;
        float nearestDistance = Mathf.Infinity;
        int highestPriority = -1;

        foreach (GameObject enemy in enemies)
        {
            if (enemy == null) continue;

            // Check if in range
            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            if (distance > attackRange) continue;

            // Get priority
            int priority = GetTargetPriority(enemy);

            // Choose based on priority, then distance
            if (priority > highestPriority || (priority == highestPriority && distance < nearestDistance))
            {
                nearestEnemy = enemy;
                nearestDistance = distance;
                highestPriority = priority;
            }
        }

        return nearestEnemy;
    }

    private int GetTargetPriority(GameObject obj)
    {
        // Prioritize knights (STRONG against)
        if (obj.GetComponent<Knight>() != null) return 5;

        // Then bases (win condition)
        if (obj.GetComponent<BaseHealth>() != null) return 4;

        // Then other archers
        if (obj.GetComponent<Archer>() != null) return 2;

        // Walls/towers lowest
        return 1;
    }

    private void FaceTarget(Vector3 targetPos)
    {
        Vector3 direction = (targetPos - transform.position).normalized;
        direction.y = 0;

        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(direction),
                Time.deltaTime * 5f
            );
        }
    }

    /// <summary>
    /// Shoot arrow at target
    /// </summary>
    private void ShootArrow(GameObject target)
    {
        if (target == null) return;

        if (arrowPrefab != null)
        {
            // Spawn arrow projectile
            GameObject arrow = Instantiate(arrowPrefab, arrowSpawnPoint.position, arrowSpawnPoint.rotation);

            ArrowProjectile projectile = arrow.GetComponent<ArrowProjectile>();
            if (projectile == null)
            {
                projectile = arrow.AddComponent<ArrowProjectile>();
            }

            // Check if target is Knight for bonus damage
            Knight knight = target.GetComponent<Knight>();
            float finalDamage = attackDamage;
            if (knight != null)
            {
                finalDamage *= STRONG_MULTIPLIER;
                Debug.Log($"[Archer] STRONG shot vs Knight! {finalDamage} damage");
            }

            projectile.Initialize(target, finalDamage, arrowSpeed, team, this.gameObject);
        }
        else
        {
            // No arrow prefab - apply damage directly
            float finalDamage = attackDamage;

            Knight knight = target.GetComponent<Knight>();
            if (knight != null)
            {
                finalDamage *= STRONG_MULTIPLIER;
            }

            CombatUtils.ApplyDamage(target, finalDamage, this.gameObject);
        }

        Debug.Log($"[Archer] {team} archer shoots at {target.name}");
    }

    /// <summary>
    /// Take damage - check for weakness multiplier
    /// </summary>
    public void TakeDamage(float damage, GameObject attacker)
    {
        if (isDead) return;

        float finalDamage = damage;

        // Check if attacker is Bomber (WEAK against)
        // Note: You'll need to create Bomber script or remove this
        // Bomber bomber = attacker.GetComponent<Bomber>();
        // if (bomber != null)
        // {
        //     finalDamage *= WEAK_MULTIPLIER;
        //     Debug.Log($"[Archer] WEAK! Taking extra damage from Bomber! {finalDamage} total");
        // }

        currentHealth -= finalDamage;
        Debug.Log($"[Archer] {team} archer took {finalDamage} damage. HP: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;

        Debug.Log($"[Archer] {team} archer died at {gridPosition}");

        // Remove from grid
        if (grid != null)
        {
            grid.RemoveUnit(this.gameObject);
        }

        Destroy(gameObject, 0.1f);
    }

    // Debug visualization
    private void OnDrawGizmos()
    {
        if (isDead) return;

        // Draw attack range
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Draw line to target
        if (currentTarget != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, currentTarget.transform.position);
        }
    }
}

/// <summary>
/// Arrow projectile - flies towards target
/// </summary>
public class ArrowProjectile : MonoBehaviour
{
    private GameObject target;
    private float damage;
    private float speed;
    private TeamSide team;
    private GameObject shooter;

    public void Initialize(GameObject targetObj, float dmg, float spd, TeamSide t, GameObject shooterObj)
    {
        target = targetObj;
        damage = dmg;
        speed = spd;
        team = t;
        shooter = shooterObj;

        Destroy(gameObject, 5f); // Auto-destroy after 5 seconds
    }

    private void Update()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        // Move towards target
        Vector3 direction = (target.transform.position - transform.position).normalized;
        transform.position += direction * speed * Time.deltaTime;

        // Rotate to face direction
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }

        // Check if close enough to hit
        if (Vector3.Distance(transform.position, target.transform.position) < 0.5f)
        {
            HitTarget();
        }
    }

    private void HitTarget()
    {
        // Apply damage using utility function
        CombatUtils.ApplyDamage(target, damage, shooter);

        // Destroy arrow
        Destroy(gameObject);
    }
}