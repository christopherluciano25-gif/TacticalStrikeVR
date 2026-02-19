using UnityEngine;

public class WallHealth : MonoBehaviour
{
    [Header("Wall Stats")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private GameObject destroyEffectPrefab;
    
    [Header("Team")]
    public TeamSide owner = TeamSide.Player; // Changed from Owner to owner (lowercase!)
    
    private float currentHealth;
    private bool isDestroyed = false;
    
    // Add this property for backwards compatibility
    public TeamSide Owner => owner;
    
    private void Start()
    {
        currentHealth = maxHealth;
    }
    
    public void TakeDamage(float damage)
    {
        if (isDestroyed) return;
        
        currentHealth -= damage;
        
        Debug.Log($"[Wall] {owner} wall took {damage} damage. HP: {currentHealth}/{maxHealth}");
        
        if (currentHealth <= 0)
        {
            DestroyWall();
        }
    }
    
    private void DestroyWall()
    {
        if (isDestroyed) return;
        
        isDestroyed = true;
        
        Debug.Log($"[Wall] {owner} wall destroyed!");
        
        if (destroyEffectPrefab != null)
        {
            Instantiate(destroyEffectPrefab, transform.position, Quaternion.identity);
        }
        
        // Remove from grid
        TacticalGrid grid = FindObjectOfType<TacticalGrid>();
        if (grid != null)
        {
            grid.RemoveUnit(this.gameObject);
        }
        
        Destroy(gameObject);
    }
}