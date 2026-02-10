using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages elixir/resource display in VR
/// Shows current amount and generation rate
/// </summary>
public class ElixirDisplay : MonoBehaviour
{
    [Header("Elixir Settings")]
    [SerializeField] private float startingElixir = 100f;
    [SerializeField] private float maxElixir = 999f;
    [SerializeField] private float elixirPerSecond = 5f;
    [SerializeField] private bool autoGenerate = true;
    
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI elixirText;
    [SerializeField] private Image elixirFillBar;
    [SerializeField] private TextMeshProUGUI generationRateText;
    
    [Header("Visual Settings")]
    [SerializeField] private Color fullColor = Color.cyan;
    [SerializeField] private Color emptyColor = Color.gray;
    [SerializeField] private bool showDecimalPlaces = false;
    
    // Current state
    private float currentElixir;
    
    // Events
    public delegate void ElixirChangedDelegate(float current, float max);
    public event ElixirChangedDelegate OnElixirChanged;
    
    // Properties
    public float CurrentElixir => currentElixir;
    public float MaxElixir => maxElixir;
    public float ElixirPerSecond => elixirPerSecond;
    
    private void Start()
    {
        currentElixir = startingElixir;
        UpdateDisplay();
    }
    
    private void Update()
    {
        if (autoGenerate)
        {
            AddElixir(elixirPerSecond * Time.deltaTime);
        }
    }
    
    /// <summary>
    /// Add elixir (can be positive or negative)
    /// </summary>
    public void AddElixir(float amount)
    {
        currentElixir = Mathf.Clamp(currentElixir + amount, 0f, maxElixir);
        UpdateDisplay();
        OnElixirChanged?.Invoke(currentElixir, maxElixir);
    }
    
    /// <summary>
    /// Try to spend elixir - returns true if successful
    /// </summary>
    public bool TrySpendElixir(float amount)
    {
        if (currentElixir >= amount)
        {
            AddElixir(-amount);
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Check if player has enough elixir
    /// </summary>
    public bool HasEnoughElixir(float amount)
    {
        return currentElixir >= amount;
    }
    
    /// <summary>
    /// Set elixir to specific amount
    /// </summary>
    public void SetElixir(float amount)
    {
        currentElixir = Mathf.Clamp(amount, 0f, maxElixir);
        UpdateDisplay();
        OnElixirChanged?.Invoke(currentElixir, maxElixir);
    }
    
    /// <summary>
    /// Update all UI elements
    /// </summary>
    private void UpdateDisplay()
    {
        // Update text
        if (elixirText != null)
        {
            if (showDecimalPlaces)
            {
                elixirText.text = $"{currentElixir:F1}/{maxElixir:F0}";
            }
            else
            {
                elixirText.text = $"{Mathf.FloorToInt(currentElixir)}/{Mathf.FloorToInt(maxElixir)}";
            }
        }
        
        // Update fill bar
        if (elixirFillBar != null)
        {
            float fillAmount = currentElixir / maxElixir;
            elixirFillBar.fillAmount = fillAmount;
            elixirFillBar.color = Color.Lerp(emptyColor, fullColor, fillAmount);
        }
        
        // Update generation rate text
        if (generationRateText != null)
        {
            generationRateText.text = $"+{elixirPerSecond:F1}/s";
        }
    }
    
    /// <summary>
    /// Set generation rate
    /// </summary>
    public void SetGenerationRate(float ratePerSecond)
    {
        elixirPerSecond = ratePerSecond;
        UpdateDisplay();
    }
    
    /// <summary>
    /// Toggle auto generation
    /// </summary>
    public void SetAutoGeneration(bool enabled)
    {
        autoGenerate = enabled;
    }
}