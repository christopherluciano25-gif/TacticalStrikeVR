using UnityEngine;

public class AIManagement : MonoBehaviour
{
    [SerializeField] private int maxResources = 5;
    [SerializeField] private float regenerationInterval = 5f; // Regenerate every 5 seconds
    
    private int currentResources = 0;
    private float regenerationTimer = 0f;

    private void Update()
    {
        // Update regeneration timer in real-time during gameplay
        regenerationTimer += Time.deltaTime;

        if (regenerationTimer >= regenerationInterval)
        {
            RegenerateResource();
            regenerationTimer = 0f;
        }
    }

    /// <summary>
    /// Regenerate one resource unit if below max
    /// </summary>
    private void RegenerateResource()
    {
        if (currentResources < maxResources)
        {
            currentResources++;
            OnResourcesChanged();
        }
    }

    /// <summary>
    /// Spend resources for placing buildings or troops
    /// </summary>
    public bool SpendResources(int amount)
    {
        if (currentResources >= amount)
        {
            currentResources -= amount;
            OnResourcesChanged();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Get current resources
    /// </summary>
    public int GetCurrentResources()
    {
        return currentResources;
    }

    /// <summary>
    /// Get max resources
    /// </summary>
    public int GetMaxResources()
    {
        return maxResources;
    }

    /// <summary>
    /// Called whenever resources change - use this to update UI
    /// </summary>
    private void OnResourcesChanged()
    {
        Debug.Log($"Resources updated: {currentResources}/{maxResources}");
        // TODO: Update UI here (resource bar visualization)
    }

    /// <summary>
    /// Force set resources (for testing)
    /// </summary>
    public void SetResources(int amount)
    {
        currentResources = Mathf.Clamp(amount, 0, maxResources);
        OnResourcesChanged();
    }
}
