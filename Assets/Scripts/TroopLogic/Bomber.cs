using UnityEngine;
using System.Collections;

/// <summary>
/// Bomber - Fast suicide unit
/// STRONG against: Archers (2x damage)
/// WEAK against: Knights (takes 2x damage)
/// Targets: Bases > Walls > Archers (ignores other units)
/// Explodes on contact, dealing AOE damage
/// </summary>
public class Bomber : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float maxHealth = 40f;
    [SerializeField] private float moveSpeed = 4f; // Fixed typo: Changed SerializfeField to SerializeField
    [SerializeField] private float explosionDamage = 50f;
    [SerializeField] private float explosionRadius = 2f;
    
    [Header("Team")]
    public TeamSide team = TeamSide.Player;
    
    [Header("Explosion Effect")]
    [SerializeField] private GameObject explosionEffectPrefab;
    
    [Header("References")]
    [SerializeField] private TacticalGrid grid;
    
    // State
    private float currentHealth;
    private bool isDead = false;
    private bool hasExploded = false;
    private GameObject currentTarget = null;
    private Vector2Int gridPosition;
    
    // Combat multipliers (Archers > Knights > Bombers > Archers)
    private const float STRONG_MULTIPLIER = 2f;  // vs Archers
    private const float WEAK_MULTIPLIER = 2f;    // from Knights
    
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
    /// Main AI loop - find priority target and rush it
    /// </summary>
    private IEnumerator AILoop()
    {
        while (!isDead && !hasExploded)
        {
            // Find target (prioritize: Bases > Walls > Archers)
            currentTarget = FindPriorityTarget();
            
            if (currentTarget != null)
            {
                float distance = Vector3.Distance(transform.position, currentTarget.transform.position);
                
                if (distance <= 1f) // Close enough to explode
                {
                    Explode();
                }
                else
                {
                    // Rush towards target (bombers move fast!)
                    MoveTowards(currentTarget.transform.position);
                }
            }
            
            yield return new WaitForSeconds(0.05f); // Update 20 times per second (fast!)
        }
    }
    
    /// <summary>
    /// Find priority target: Bases > Walls > Archers
    /// Bombers IGNORE knights and other bombers
    /// </summary>
    private GameObject FindPriorityTarget()
    {
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        GameObject bestTarget = null;
        float nearestDistance = Mathf.Infinity;
        int highestPriority = -1;
        
        foreach (GameObject obj in allObjects)
        {
            // Check if it's an enemy
            TeamSide objTeam = GetObjectTeam(obj);
            if (objTeam == TeamSide.Neutral || objTeam == team)
                continue;
            
            // Get priority
            int priority = GetTargetPriority(obj);
            if (priority == 0) continue; // Ignore this type
            
            float distance = Vector3.Distance(transform.position, obj.transform.position);
            
            // Choose target based on priority, then distance
            if (priority > highestPriority || (priority == highestPriority && distance < nearestDistance))
            {
                bestTarget = obj;
                nearestDistance = distance;
                highestPriority = priority;
            }
        }
        
        return bestTarget;
    }
    
    private int GetTargetPriority(GameObject obj)
    {
        // PRIORITY 1: Enemy Base (win condition!)
        if (obj.GetComponent<BaseHealth>() != null) return 5;
        
        // PRIORITY 2: Walls (bombers destroy walls)
        if (obj.GetComponent<WallHealth>() != null) return 4;
        
        // PRIORITY 3: Archers (we're strong against them)
        if (obj.GetComponent<Archer>() != null) return 3;
        
        // IGNORE: Knights (they counter us)
        if (obj.GetComponent<Knight>() != null) return 0;
        
        // IGNORE: Other bombers
        if (obj.GetComponent<Bomber>() != null) return 0;
        
        return 1; // Low priority for anything else
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
        
        WallHealth wallHealth = obj.GetComponent<WallHealth>();
        if (wallHealth != null) return wallHealth.Owner;
        
        return TeamSide.Neutral;
    }
    
    /// <summary>
    /// Move towards target (fast!)
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
        
        UpdateGridPosition();
    }
    
    /// <summary>
    /// EXPLODE! Deal AOE damage and die
    /// </summary>
    private void Explode()
    {
        if (hasExploded) return;
        
        hasExploded = true;
        
        Debug.Log($"[Bomber] {team} bomber EXPLODED at {transform.position}!");
        
        // Spawn explosion effect
        if (explosionEffectPrefab != null)
        {
            Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
        }
        
        // Find all objects in explosion radius
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, explosionRadius);
        
        foreach (Collider col in hitColliders)
        {
            GameObject obj = col.gameObject;
            
            // Don't damage self or allies
            TeamSide objTeam = GetObjectTeam(obj);
            if (objTeam == team) continue;
            
            // Calculate damage (closer = more damage)
            float distance = Vector3.Distance(transform.position, obj.transform.position);
            float damageFalloff = 1f - (distance / explosionRadius);
            float finalDamage = explosionDamage * damageFalloff;
            
            // Apply combat multiplier if target is Archer
            Archer archer = obj.GetComponent<Archer>();
            if (archer != null)
            {
                finalDamage *= STRONG_MULTIPLIER;
                Debug.Log($"[Bomber] STRONG explosion vs Archer! {finalDamage} damage");
            }
            
            // Deal damage
            ApplyDamageToTarget(obj, finalDamage);
        }
        
        // Bomber dies from explosion
        Die();
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
    /// Take damage - WEAK against Knights
    /// </summary>
    public void TakeDamage(float damage, GameObject attacker)
    {
        if (isDead || hasExploded) return;
        
        float finalDamage = damage;
        
        // Check if attacker is Knight (we're weak against knights)
        Knight knight = attacker.GetComponent<Knight>();
        if (knight != null)
        {
            finalDamage *= WEAK_MULTIPLIER;
            Debug.Log($"[Bomber] WEAK! Taking extra damage from Knight! {finalDamage} total");
        }
        
        currentHealth -= finalDamage;
        Debug.Log($"[Bomber] {team} bomber took {finalDamage} damage. HP: {currentHealth}/{maxHealth}");
        
        if (currentHealth <= 0)
        {
            // Die but DON'T explode (killed before reaching target)
            Die();
        }
    }
    
    private void Die()
    {
        if (isDead) return;
        
        isDead = true;
        
        Debug.Log($"[Bomber] {team} bomber died at {gridPosition}");
        
        // Remove from grid
        if (grid != null)
        {
            grid.RemoveUnit(this.gameObject);
        }
        
        Destroy(gameObject, 0.1f);
    }
}