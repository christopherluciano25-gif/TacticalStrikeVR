using UnityEngine;

/// <summary>
/// Manages base health - game ends when base is destroyed
/// Attach to main base prefab (Player Base / Bot Base)
/// </summary>
public class BaseHealth : MonoBehaviour
{
    [Header("Base Settings")]
    [SerializeField] private float maxHealth = 500f;
    public TeamSide owner = TeamSide.Player; // Public so troops can check team
    
    [Header("Visual Feedback")]
    [SerializeField] private bool showHealthBar = true;
    [SerializeField] private GameObject healthBarPrefab; // Optional
    
    [Header("Game Over")]
    [SerializeField] private bool triggersGameOver = true;
    
    // Current state
    private float currentHealth;
    
    // Events
    public delegate void BaseDamagedDelegate(float currentHealth, float maxHealth, TeamSide owner);
    public static event BaseDamagedDelegate OnBaseDamaged;
    
    public delegate void BaseDestroyedDelegate(TeamSide owner);
    public static event BaseDestroyedDelegate OnBaseDestroyed;
    
    // Properties
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public TeamSide Owner => owner;
    public bool IsDestroyed => currentHealth <= 0;
    
    private void Start()
    {
        currentHealth = maxHealth;
        Debug.Log($"[BaseHealth] {owner} base initialized with {maxHealth} HP");
    }
    
    /// <summary>
    /// Take damage to the base
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (IsDestroyed) return;
        
        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);
        
        Debug.Log($"[BaseHealth] {owner} base took {damage} damage! HP: {currentHealth}/{maxHealth}");
        
        // Invoke damage event
        OnBaseDamaged?.Invoke(currentHealth, maxHealth, owner);
        
        // Check if destroyed
        if (currentHealth <= 0)
        {
            HandleDestruction();
        }
    }
    
    /// <summary>
    /// Heal the base
    /// </summary>
    public void Heal(float amount)
    {
        if (IsDestroyed) return;
        
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        Debug.Log($"[BaseHealth] {owner} base healed {amount}! HP: {currentHealth}/{maxHealth}");
        
        OnBaseDamaged?.Invoke(currentHealth, maxHealth, owner);
    }
    
    /// <summary>
    /// Handle base destruction
    /// </summary>
    private void HandleDestruction()
    {
        Debug.Log($"[BaseHealth] {owner} BASE DESTROYED! Game Over!");
        
        // Invoke destroyed event
        OnBaseDestroyed?.Invoke(owner);
        
        // Trigger game over
        if (triggersGameOver)
        {
            TriggerGameOver();
        }
        
        // Visual feedback
        DestroyBase();
    }
    
    /// <summary>
    /// Trigger game over (GameEndManager will listen for OnBaseDestroyed event)
    /// </summary>
    private void TriggerGameOver()
    {
        // GameEndManager listens to OnBaseDestroyed event and handles victory/defeat
        Debug.Log($"[BaseHealth] Game Over! {owner} base destroyed");
    }
    
    /// <summary>
    /// Destroy the base visually
    /// </summary>
    private void DestroyBase()
    {
        // Remove from grid
        TacticalGrid grid = FindObjectOfType<TacticalGrid>();
        if (grid != null)
        {
            grid.RemoveUnit(this.gameObject);
        }
        
        // Destroy immediately
        Destroy(gameObject);
    }
    
    /// <summary>
    /// Get health as percentage (0-1)
    /// </summary>
    public float GetHealthPercentage()
    {
        return currentHealth / maxHealth;
    }
    
    // Visual debug
    private void OnGUI()
    {
        if (!showHealthBar || IsDestroyed) return;
        
        // Only show in editor for debugging
        if (!Application.isEditor) return;
        
        // Calculate screen position
        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 3f);
        
        if (screenPos.z > 0) // In front of camera
        {
            // Draw health bar
            float barWidth = 150f;
            float barHeight = 15f;
            float healthPercent = GetHealthPercentage();
            
            Rect bgRect = new Rect(screenPos.x - barWidth / 2f, Screen.height - screenPos.y, barWidth, barHeight);
            Rect healthRect = new Rect(screenPos.x - barWidth / 2f, Screen.height - screenPos.y, barWidth * healthPercent, barHeight);
            
            GUI.color = Color.black;
            GUI.DrawTexture(bgRect, Texture2D.whiteTexture);
            
            GUI.color = healthPercent > 0.5f ? Color.green : (healthPercent > 0.25f ? Color.yellow : Color.red);
            GUI.DrawTexture(healthRect, Texture2D.whiteTexture);
            
            GUI.color = Color.white;
            GUI.Label(new Rect(screenPos.x - barWidth / 2f, Screen.height - screenPos.y - 20f, barWidth, 20f), 
                      $"{owner} Base: {currentHealth:F0}/{maxHealth:F0}");
        }
    }
}

// For when troop base damage is implemented - troops already have this code!
// BaseHealth base = target.GetComponent<BaseHealth>();
// base.TakeDamage(10f); // Base loses 10 HP