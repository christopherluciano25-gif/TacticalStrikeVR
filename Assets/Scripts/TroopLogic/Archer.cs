using UnityEngine;
using System.Collections;

/// <summary>
/// Archer - Stationary ranged unit
/// STRONG against: Knights (2x damage)
/// WEAK against: Bombers (takes 2x damage)
/// NEUTRAL against: Archers, Walls, Bases
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
    
    // Combat multipliers (Archers > Knights > Bombers > Archers)
    private const float STRONG_MULTIPLIER = 2f;  // vs Knights
    private const float WEAK_MULTIPLIER = 2f;    // from Bombers
    
    private void Start()
    {
        currentHealth = maxHealth;
        
        if (grid == null)
            grid = FindObjectOfType<TacticalGrid>();
        
        UpdateGridPosition();
        
        // Setup arrow spawn point if not assigned
        if (arrowSpawnPoint == null)
        {
            GameObject spawnObj = new GameObject("ArrowSpawn");
            spawnObj.transform.SetParent(transform);
            spawnObj.transform.localPosition = Vector3.up * 1.5f; // Shoot from height
            arrowSpawnPoint = spawnObj.transform;
        }
        
        StartCoroutine(AILoop());
    }
    
    private void UpdateGridPosition()
    {
        if (grid != null)
        {
            gridPosition = grid.WorldToGrid(transform.position);
        }
    }
    
    /// <summary>
    /// Main AI loop - find target and shoot (archers don't move!)
    /// </summary>
    private IEnumerator AILoop()
    {
        while (!isDead)
        {
            // Find nearest enemy in range
            currentTarget = FindNearestEnemyInRange();
            
            if (currentTarget != null)
            {
                // Rotate to face target
                FaceTarget(currentTarget.transform.position);
                
                // Attack if cooldown is ready
                if (Time.time >= lastAttackTime + attackCooldown)
                {
                    ShootArrow(currentTarget);
                    lastAttackTime = Time.time;
                }
            }
            
            yield return new WaitForSeconds(0.2f); // Update 5 times per second
        }
    }
    
    /// <summary>
    /// Find nearest enemy within attack range
    /// Prioritize: Knights > Bombers > Bases > Other
    /// </summary>
    private GameObject FindNearestEnemyInRange()
    {
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        GameObject nearestEnemy = null;
        float nearestDistance = Mathf.Infinity;
        int highestPriority = -1;
        
        foreach (GameObject obj in allObjects)
        {
            // Check if it's an enemy
            TeamSide objTeam = GetObjectTeam(obj);
            if (objTeam == TeamSide.Neutral || objTeam == team)
                continue;
            
            // Check if in range
            float distance = Vector3.Distance(transform.position, obj.transform.position);
            if (distance > attackRange)
                continue;
            
            // Get priority
            int priority = GetTargetPriority(obj);
            
            // Choose target based on priority, then distance
            if (priority > highestPriority || (priority == highestPriority && distance < nearestDistance))
            {
                nearestEnemy = obj;
                nearestDistance = distance;
                highestPriority = priority;
            }
        }
        
        return nearestEnemy;
    }
    
    private int GetTargetPriority(GameObject obj)
    {
        // Archers prioritize knights (strong against)
        if (obj.GetComponent<Knight>() != null) return 5; // Highest priority
        
        // Then bombers (even though weak, need to defend)
        if (obj.GetComponent<Bomber>() != null) return 4;
        
        // Then bases
        if (obj.GetComponent<BaseHealth>() != null) return 3;
        
        // Then other archers
        if (obj.GetComponent<Archer>() != null) return 2;
        
        // Walls/towers lowest
        return 1;
    }
    
    private TeamSide GetObjectTeam(GameObject obj)
    {
        Knight knight = obj.GetComponent<Knight>();
        if (knight != null) return knight.team;
        
        Archer archer = obj.GetComponent<Archer>();
        if (archer != null) return archer.team;
        
        Bomber bomber = obj.GetComponent<Bomber>();
        if (bomber != null) return bomber.team;
        
        BaseHealth baseHealth = obj.GetComponent<BaseHealth>();
        if (baseHealth != null) return baseHealth.Owner;
        
        return TeamSide.Neutral;
    }
    
    /// <summary>
    /// Rotate to face target
    /// </summary>
    private void FaceTarget(Vector3 targetPos)
    {
        Vector3 direction = (targetPos - transform.position).normalized;
        direction.y = 0; // Keep upright
        
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }
    
    /// <summary>
    /// Shoot arrow at target
    /// </summary>
    private void ShootArrow(GameObject target)
    {
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
            
            projectile.Initialize(target, finalDamage, arrowSpeed, team);
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
            
            ApplyDamageToTarget(target, finalDamage);
        }
        
        Debug.Log($"[Archer] {team} archer shoots at {target.name}");
    }
    
    private void ApplyDamageToTarget(GameObject target, float damage)
    {
        Knight knight = target.GetComponent<Knight>();
        if (knight != null)
        {
            knight.TakeDamage(damage, this.gameObject);
            return;
        }
        
        Archer archer = target.GetComponent<Archer>();
        if (archer != null)
        {
            archer.TakeDamage(damage, this.gameObject);
            return;
        }
        
        Bomber bomber = target.GetComponent<Bomber>();
        if (bomber != null)
        {
            bomber.TakeDamage(damage, this.gameObject);
            return;
        }
        
        BaseHealth baseHealth = target.GetComponent<BaseHealth>();
        if (baseHealth != null)
        {
            baseHealth.TakeDamage(damage);
            return;
        }
        
        WallHealth wallHealth = target.GetComponent<WallHealth>();
        if (wallHealth != null)
        {
            wallHealth.TakeDamage(damage);
            return;
        }
    }
    
    /// <summary>
    /// Take damage - check if from Bomber for weakness
    /// </summary>
    public void TakeDamage(float damage, GameObject attacker)
    {
        if (isDead) return;
        
        float finalDamage = damage;
        
        // Check if attacker is Bomber (we're weak against bombers)
        Bomber bomber = attacker.GetComponent<Bomber>();
        if (bomber != null)
        {
            finalDamage *= WEAK_MULTIPLIER;
            Debug.Log($"[Archer] WEAK! Taking extra damage from Bomber! {finalDamage} total");
        }
        
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
        
        // Draw line to current target
        if (currentTarget != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, currentTarget.transform.position);
        }
    }
}

/// <summary>
/// Arrow projectile - flies towards target and deals damage on hit
/// </summary>
public class ArrowProjectile : MonoBehaviour
{
    private GameObject target;
    private float damage;
    private float speed;
    private TeamSide team;
    
    public void Initialize(GameObject targetObj, float dmg, float spd, TeamSide t)
    {
        target = targetObj;
        damage = dmg;
        speed = spd;
        team = t;
        
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
        // Apply damage
        Knight knight = target.GetComponent<Knight>();
        if (knight != null)
        {
            knight.TakeDamage(damage, this.gameObject);
        }
        
        Archer archer = target.GetComponent<Archer>();
        if (archer != null)
        {
            archer.TakeDamage(damage, this.gameObject);
        }
        
        Bomber bomber = target.GetComponent<Bomber>();
        if (bomber != null)
        {
            bomber.TakeDamage(damage, this.gameObject);
        }
        
        BaseHealth baseHealth = target.GetComponent<BaseHealth>();
        if (baseHealth != null)
        {
            baseHealth.TakeDamage(damage);
        }
        
        // Destroy arrow
        Destroy(gameObject);
    }
}