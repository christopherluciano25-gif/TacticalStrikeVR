using UnityEngine;

/// <summary>
/// Manages wall health - game ends when wall is destroyed
/// Attach to wall prefab
/// </summary>
public class WallHealth : MonoBehaviour
{
    [Header("Wall Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private WallOwner owner = WallOwner.Player;
    
    [Header("Visual Feedback")]
    [SerializeField] private bool showHealthBar = true;
    [SerializeField] private GameObject healthBarPrefab; // Optional
    
    [Header("Game Over")]
    [SerializeField] private bool triggersGameOver = true;
    
    // Current state
    private float currentHealth;
    
    // Events
    public delegate void WallDamagedDelegate(float currentHealth, float maxHealth, WallOwner owner);
    public static event WallDamagedDelegate OnWallDamaged;
    
    public delegate void WallDestroyedDelegate(WallOwner owner);
    public static event WallDestroyedDelegate OnWallDestroyed;
    
    // Properties
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public WallOwner Owner => owner;
    public bool IsDestroyed => currentHealth <= 0;
    
    private void Start()
    {
        currentHealth = maxHealth;
        Debug.Log($"[WallHealth] {owner} wall initialized with {maxHealth} HP");
    }
    
    /// <summary>
    /// Take damage to the wall
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (IsDestroyed) return;
        
        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);
        
        Debug.Log($"[WallHealth] {owner} wall took {damage} damage! HP: {currentHealth}/{maxHealth}");
        
        // Invoke damage event
        OnWallDamaged?.Invoke(currentHealth, maxHealth, owner);
        
        // Check if destroyed
        if (currentHealth <= 0)
        {
            HandleDestruction();
        }
    }
    
    /// <summary>
    /// Heal the wall
    /// </summary>
    public void Heal(float amount)
    {
        if (IsDestroyed) return;
        
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        Debug.Log($"[WallHealth] {owner} wall healed {amount}! HP: {currentHealth}/{maxHealth}");
        
        OnWallDamaged?.Invoke(currentHealth, maxHealth, owner);
    }
    
    /// <summary>
    /// Handle wall destruction
    /// </summary>
    private void HandleDestruction()
    {
        Debug.Log($"[WallHealth] {owner} WALL DESTROYED! Game Over!");
        
        // Invoke destroyed event
        OnWallDestroyed?.Invoke(owner);
        
        // Trigger game over
        if (triggersGameOver)
        {
            TriggerGameOver();
        }
        
        // Visual feedback
        DestroyWall();
    }
    
    /// <summary>
    /// Trigger game over
    /// </summary>
    private void TriggerGameOver()
    {
        // Find GameManager if you have one
        GameManager gameManager = FindObjectOfType<GameManager>();
        
        if (gameManager != null)
        {
            // Determine winner (opposite of wall owner)
            GameTeam winner = owner == WallOwner.Player ? GameTeam.AI : GameTeam.Player;
            
            // You'll need to add this method to your GameManager:
            // gameManager.EndGame(winner);
            
            Debug.Log($"[WallHealth] Game Over! Winner: {winner}");
        }
        else
        {
            Debug.LogWarning("[WallHealth] No GameManager found! Cannot trigger game over.");
        }
    }
    
    /// <summary>
    /// Destroy the wall visually
    /// </summary>
    private void DestroyWall()
    {
        // Option 1: Destroy immediately
        Destroy(gameObject);
        
        // Option 2: Play death animation first (if you have one)
        // Animator animator = GetComponent<Animator>();
        // if (animator != null)
        // {
        //     animator.SetTrigger("Destroy");
        //     Destroy(gameObject, 2f); // Destroy after 2 seconds
        // }
        // else
        // {
        //     Destroy(gameObject);
        // }
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
        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2f);
        
        if (screenPos.z > 0) // In front of camera
        {
            // Draw health bar
            float barWidth = 100f;
            float barHeight = 10f;
            float healthPercent = GetHealthPercentage();
            
            Rect bgRect = new Rect(screenPos.x - barWidth / 2f, Screen.height - screenPos.y, barWidth, barHeight);
            Rect healthRect = new Rect(screenPos.x - barWidth / 2f, Screen.height - screenPos.y, barWidth * healthPercent, barHeight);
            
            GUI.color = Color.black;
            GUI.DrawTexture(bgRect, Texture2D.whiteTexture);
            
            GUI.color = healthPercent > 0.5f ? Color.green : (healthPercent > 0.25f ? Color.yellow : Color.red);
            GUI.DrawTexture(healthRect, Texture2D.whiteTexture);
            
            GUI.color = Color.white;
            GUI.Label(new Rect(screenPos.x - barWidth / 2f, Screen.height - screenPos.y - 20f, barWidth, 20f), 
                      $"{owner} Wall: {currentHealth:F0}/{maxHealth:F0}");
        }
    }
}

/// <summary>
/// Which team owns this wall
/// </summary>
public enum WallOwner
{
    Player,
    AI
}

//For when troop wall dmg is implemented
//WallHealth wall = target.GetComponent<WallHealth>();
//wall.TakeDamage(10f); // Wall loses 10 HP