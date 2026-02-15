using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages game end conditions and victory/defeat screens
/// Listens for base destruction events
/// </summary>
public class GameEndManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject victoryScreen;
    [SerializeField] private GameObject defeatScreen;
    [SerializeField] private TextMeshProUGUI victoryText;
    [SerializeField] private TextMeshProUGUI defeatText;
    
    [Header("Settings")]
    [SerializeField] private float endGameDelay = 2f;
    [SerializeField] private bool pauseOnGameEnd = true;
    
    // State
    private bool gameEnded = false;
    
    private void OnEnable()
    {
        // Subscribe to base destruction event
        BaseHealth.OnBaseDestroyed += HandleBaseDestroyed;
    }
    
    private void OnDisable()
    {
        // Unsubscribe
        BaseHealth.OnBaseDestroyed -= HandleBaseDestroyed;
    }
    
    private void Start()
    {
        // Hide end screens at start
        if (victoryScreen != null) victoryScreen.SetActive(false);
        if (defeatScreen != null) defeatScreen.SetActive(false);
    }
    
    /// <summary>
    /// Handle base destruction - determine winner and show end screen
    /// </summary>
    private void HandleBaseDestroyed(TeamSide destroyedBase)
    {
        if (gameEnded) return;
        
        gameEnded = true;
        
        Debug.Log($"[GameEndManager] Base destroyed: {destroyedBase}");
        
        // Determine winner (opposite team wins)
        TeamSide winner = destroyedBase == TeamSide.Player ? TeamSide.Bot : TeamSide.Player;
        
        // Determine if player won or lost
        bool playerWon = (winner == TeamSide.Player);
        
        if (playerWon)
        {
            Debug.Log("[GameEndManager] PLAYER VICTORY!");
            Invoke(nameof(ShowVictoryScreen), endGameDelay);
        }
        else
        {
            Debug.Log("[GameEndManager] PLAYER DEFEAT!");
            Invoke(nameof(ShowDefeatScreen), endGameDelay);
        }
        
        // Optionally pause the game
        if (pauseOnGameEnd)
        {
            Invoke(nameof(PauseGame), endGameDelay);
        }
    }
    
    /// <summary>
    /// Show victory screen
    /// </summary>
    private void ShowVictoryScreen()
    {
        if (victoryScreen != null)
        {
            victoryScreen.SetActive(true);
            
            if (victoryText != null)
            {
                victoryText.text = "VICTORY!\nEnemy Base Destroyed!";
            }
        }
        
        if (defeatScreen != null)
            defeatScreen.SetActive(false);
    }
    
    /// <summary>
    /// Show defeat screen
    /// </summary>
    private void ShowDefeatScreen()
    {
        if (defeatScreen != null)
        {
            defeatScreen.SetActive(true);
            
            if (defeatText != null)
            {
                defeatText.text = "DEFEAT\nYour Base Was Destroyed";
            }
        }
        
        if (victoryScreen != null)
            victoryScreen.SetActive(false);
    }
    
    /// <summary>
    /// Pause the game
    /// </summary>
    private void PauseGame()
    {
        Time.timeScale = 0f;
    }
    
    /// <summary>
    /// Restart the game (call from UI button)
    /// </summary>
    public void RestartGame()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
        );
    }
    
    /// <summary>
    /// Quit to menu (call from UI button)
    /// </summary>
    public void QuitToMenu()
    {
        Time.timeScale = 1f;
        // Load your menu scene here
        // UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        Debug.Log("[GameEndManager] Quit to menu");
    }
}