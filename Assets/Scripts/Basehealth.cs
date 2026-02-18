using UnityEngine;
using System;

public class BaseHealth : MonoBehaviour
{
    [Header("Base Stats")]
    [SerializeField] private float maxHealth = 500f;
    [SerializeField] private GameObject destroyEffectPrefab;
    
    [Header("Team")]
    public TeamSide owner = TeamSide.Player;
    
    // Static event for base destruction
    public static event Action<TeamSide> OnBaseDestroyed;
    
    private float currentHealth;
    private bool isDestroyed = false;
    
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    
    private void Start()
    {
        currentHealth = maxHealth;
        Debug.Log($"[BaseHealth] {owner} base initialized with {maxHealth} HP");
    }
    
    /// <summary>
    /// Take damage and potentially be destroyed
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (isDestroyed) return;
        
        currentHealth -= damage;
        Debug.Log($"[BaseHealth] {owner} base took {damage} damage. HP: {currentHealth}/{maxHealth}");
        
        if (currentHealth <= 0 && !isDestroyed)
        {
            DestroyBase();
        }
    }
    
    /// <summary>
    /// Destroy this base and trigger game end
    /// </summary>
    private void DestroyBase()
    {
        if (isDestroyed) return;
        
        isDestroyed = true;
        Debug.Log($"[BaseHealth] {owner} base DESTROYED!");
        
        // Trigger the game end event
        OnBaseDestroyed?.Invoke(owner);
        
        // Spawn destruction effect
        if (destroyEffectPrefab != null)
        {
            Instantiate(destroyEffectPrefab, transform.position, Quaternion.identity);
        }
        
        // Destroy the base object
        Destroy(gameObject);
    }
    
    /// <summary>
    /// Optional: For debugging purposes
    /// </summary>
    private void OnDrawGizmos()
    {
        // Visualize base bounds in editor
        Gizmos.color = owner == TeamSide.Player ? Color.blue : Color.red;
        Gizmos.DrawWireCube(transform.position, transform.localScale);
    }
}