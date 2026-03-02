using UnityEngine;

public class AIManagement : MonoBehaviour
{
    [Header("Resources")]
    [SerializeField] private int maxResources = 5;
    [SerializeField] private float regenerationInterval = 5f; // Regenerate every 5 seconds
    [SerializeField] private int startingResources = 0;

    [Header("AI Integration")]
    [SerializeField] private TDEnemyPlayerAI enemyAI;
    [SerializeField] private bool triggerBuildOnResourceGain = true;

    private int currentResources = 0;
    private float regenerationTimer = 0f;

    private void Awake()
    {
        if (enemyAI == null)
        {
            enemyAI = FindObjectOfType<TDEnemyPlayerAI>();
        }

        currentResources = Mathf.Clamp(startingResources, 0, maxResources);
    }

    private void Start()
    {
        OnResourcesChanged();
        TryTriggerAIBuild();
    }

    private void Update()
    {
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
        if (currentResources >= maxResources)
        {
            return;
        }

        currentResources++;
        OnResourcesChanged();
        TryTriggerAIBuild();
    }

    /// <summary>
    /// Spend resources for placing buildings or troops
    /// </summary>
    public bool SpendResources(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        if (currentResources >= amount)
        {
            currentResources -= amount;
            OnResourcesChanged();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Refund/add resources (used when a placement attempt fails after spending)
    /// </summary>
    public void AddResources(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        currentResources = Mathf.Clamp(currentResources + amount, 0, maxResources);
        OnResourcesChanged();
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
        TryTriggerAIBuild();
    }

    private void TryTriggerAIBuild()
    {
        if (!triggerBuildOnResourceGain || enemyAI == null)
        {
            return;
        }

        enemyAI.TryBuildOneWithResources();
    }
}
