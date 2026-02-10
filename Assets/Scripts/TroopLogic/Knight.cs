using UnityEngine;
using System.Collections;

/// <summary>
/// Knight - Melee unit that moves and attacks
/// STRONG against: Bombers (2x damage)
/// WEAK against: Archers (takes 2x damage)
/// NEUTRAL against: Knights, Walls, Bases
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
    [SerializeField] private TeamSide team = TeamSide.Player;
    
    [Header("References")]
    [SerializeField] private TacticalGrid grid;
    
    // State
    private float currentHealth;
    private bool isDead = false;
    private GameObject currentTarget = null;
    private float lastAttackTime = 0f;
    private Vector2Int gridPosition;
    
    // Combat multipliers (Archers > Knights > Bombers > Archers)
    private const float STRONG_MULTIPLIER = 2f;  // vs Bombers
    private const float WEAK_MULTIPLIER = 2f;    // from Archers
    
    private void Start()
    {
        currentHealth = maxHealth;
        
        if (grid == null)
            grid = FindObjectOfType<TacticalGrid>();
        
        UpdateGridPosition();
        
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
    /// Main AI loop - find target, move, attack
    /// </summary>
    private IEnumerator AILoop()
    {
        while (!isDead)
        {
            // Find nearest enemy
            currentTarget = FindNearestEnemy();
            
            if (currentTarget != null)
            {
                float distance = Vector3.Distance(transform.position, currentTarget.transform.position);
                
                if (distance <= attackRange)
                {
                    // In range - attack
                    if (Time.time >= lastAttackTime + attackCooldown)
                    {
                        Attack(currentTarget);
                        lastAttackTime = Time.time;
                    }
                }
                else
                {
                    // Too far - move closer
                    MoveTowards(currentTarget.transform.position);
                }
            }
            
            yield return new WaitForSeconds(0.1f); // Update 10 times per second
        }
    }
    
    /// <summary>
    /// Find nearest enemy (prioritize: Bombers > Bases > Knights > Archers > Walls)
    /// </summary>
    private GameObject FindNearestEnemy()
    {
        GameObject[] allObjects = FindObjectsOfType<GameObject>();
        GameObject nearestEnemy = null;
        float nearestDistance = Mathf.Infinity;
        int highestPriority = -1;
        
        foreach (GameObject obj in allObjects)
        {
            // Check if it's an enemy
            TeamSide objTeam = GetObjectTeam(obj);
            if (objTeam == TeamSide.Neutral || objTeam == team)
                continue;
            
            // Get priority (higher = more important target)
            int priority = GetTargetPriority(obj);
            float distance = Vector3.Distance(transform.position, obj.transform.position);
            
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
        // Knights prioritize bombers (strong against)
        if (obj.GetComponent<Bomber>() != null) return 5; // Highest priority
        
        // Then bases (win condition)
        if (obj.GetComponent<BaseHealth>() != null) return 4;
        
        // Then other knights
        if (obj.GetComponent<Knight>() != null) return 3;
        
        // Then archers (we're weak against them, but still attack)
        if (obj.GetComponent<Archer>() != null) return 2;
        
        // Walls/towers lowest priority
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
    /// Move towards target position
    /// </summary>
    private void MoveTowards(Vector3 targetPos)
    {
        Vector3 direction = (targetPos - transform.position).normalized;
        transform.position += direction * moveSpeed * Time.deltaTime;
        
        // Face movement direction
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
        
        // Update grid position
        UpdateGridPosition();
    }
    
    /// <summary>
    /// Attack target - apply damage with combat multipliers
    /// </summary>
    private void Attack(GameObject target)
    {
        float finalDamage = attackDamage;
        
        // Check rock-paper-scissors logic
        Bomber bomber = target.GetComponent<Bomber>();
        if (bomber != null)
        {
            // STRONG vs Bombers
            finalDamage *= STRONG_MULTIPLIER;
            Debug.Log($"[Knight] STRONG attack vs Bomber! {finalDamage} damage");
        }
        
        // Apply damage to target
        ApplyDamageToTarget(target, finalDamage);
        
        Debug.Log($"[Knight] {team} knight attacks {target.name} for {finalDamage} damage");
    }
    
    private void ApplyDamageToTarget(GameObject target, float damage)
    {
        // Try different health components
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
    /// Take damage - check if from Archer for weakness multiplier
    /// </summary>
    public void TakeDamage(float damage, GameObject attacker)
    {
        if (isDead) return;
        
        float finalDamage = damage;
        
        // Check if attacker is Archer (we're weak against archers)
        Archer archer = attacker.GetComponent<Archer>();
        if (archer != null)
        {
            // WEAK vs Archers - take extra damage
            finalDamage *= WEAK_MULTIPLIER;
            Debug.Log($"[Knight] WEAK! Taking extra damage from Archer! {finalDamage} total");
        }
        
        currentHealth -= finalDamage;
        Debug.Log($"[Knight] {team} knight took {finalDamage} damage. HP: {currentHealth}/{maxHealth}");
        
        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    /// <summary>
    /// Handle death
    /// </summary>
    private void Die()
    {
        if (isDead) return;
        
        isDead = true;
        
        Debug.Log($"[Knight] {team} knight died at {gridPosition}");
        
        // Remove from grid
        if (grid != null)
        {
            grid.RemoveUnit(this.gameObject);
        }
        
        // Destroy object
        Destroy(gameObject, 0.1f);
    }
    
    // Debug visualization
    private void OnDrawGizmos()
    {
        if (isDead) return;
        
        // Draw attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        
        // Draw line to current target
        if (currentTarget != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, currentTarget.transform.position);
        }
    }
}