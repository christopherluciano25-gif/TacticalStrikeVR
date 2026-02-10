using UnityEngine;

public class WallHealth : MonoBehaviour
{
    [Header("Wall Stats")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private GameObject destroyEffectPrefab;
    
    [Header("Team")]
    public TeamSide Owner = TeamSide.Player;
    
    private float currentHealth;
    private bool isDestroyed = false;
    
    private void Start()
    {
        currentHealth = maxHealth;
    }
    
    /// <summary>
    /// Take damage and potentially be destroyed
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (isDestroyed) return;
        
        currentHealth -= damage;
        
        Debug.Log($"[Wall] {Owner} wall took {damage} damage. HP: {currentHealth}/{maxHealth}");
        
        if (currentHealth <= 0)
        {
            DestroyWall();
        }
    }
    
    /// <summary>
    /// Destroy this wall
    /// </summary>
    private void DestroyWall()
    {
        if (isDestroyed) return;
        
        isDestroyed = true;
        
        Debug.Log($"[Wall] {Owner} wall destroyed!");
        
        // Spawn destruction effect
        if (destroyEffectPrefab != null)
        {
            Instantiate(destroyEffectPrefab, transform.position, Quaternion.identity);
        }
        
        // Destroy the wall object
        Destroy(gameObject);
    }
    
    /// <summary>
    /// Optional: For debugging purposes
    /// </summary>
    private void OnDrawGizmos()
    {
        // Visualize wall health in editor
        Gizmos.color = Color.gray;
        Gizmos.DrawWireCube(transform.position, transform.localScale);
    }
}