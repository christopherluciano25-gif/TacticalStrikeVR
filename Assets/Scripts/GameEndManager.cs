using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// Manages game end conditions and victory/defeat screens
/// Listens for base destruction events
/// </summary>
public class GameEndManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float endGameDelay = 2f;
    [SerializeField] private bool pauseOnGameEnd = true;
    
    [Header("VR Settings")]
    [SerializeField] private Transform playerHead; // Optional, will find if not set
    [SerializeField] private float distanceFromPlayer = 18f;
    [SerializeField] private float heightOffset = 0.5f;
    [SerializeField] private bool followPlayer = true;
    
    // UI References (created at runtime)
    private GameObject victoryScreen;
    private GameObject defeatScreen;
    private TextMeshProUGUI victoryText;
    private TextMeshProUGUI defeatText;
    
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
        // Find player head if not assigned
        if (playerHead == null)
        {
            // Try to find UnityXRCameraRig by name
            GameObject cameraRig = GameObject.Find("UnityXRCameraRig");
            if (cameraRig != null)
            {
                // Look for a child camera (usually called "Camera" or "Main Camera" or "CenterEyeAnchor")
                Camera cam = cameraRig.GetComponentInChildren<Camera>();
                if (cam != null)
                {
                    playerHead = cam.transform;
                }
                else
                {
                    // If no camera found, use the rig itself
                    playerHead = cameraRig.transform;
                }
            }
            else
            {
                // Fallback to main camera
                playerHead = Camera.main?.transform;
                
                // If still null, try to find any camera
                if (playerHead == null)
                {
                    Camera cam = FindFirstObjectByType<Camera>();
                    if (cam != null)
                        playerHead = cam.transform;
                }
            }
            
            if (playerHead != null)
            {
                Debug.Log($"[GameEndManager] Found player head: {playerHead.name} at position: {playerHead.position}");
            }
            else
            {
                Debug.LogWarning("[GameEndManager] No player head found!");
            }
        }
        
        // Log the distanceFromPlayer value at start
        Debug.Log($"[GameEndManager] START - distanceFromPlayer value = {distanceFromPlayer}");
        
        // Create UI screens programmatically
        CreateEndGameScreens();
        
        // DEBUG: Log the initial position of the screens
        if (victoryScreen != null)
        {
            Debug.Log($"[GameEndManager] INITIAL POSITION - Victory screen at: {victoryScreen.transform.position}");
        }
        if (defeatScreen != null)
        {
            Debug.Log($"[GameEndManager] INITIAL POSITION - Defeat screen at: {defeatScreen.transform.position}");
        }
    }
    
    private void Update()
    {
        // DEBUG KEYS - Press V for victory, D for defeat, R for restart
        if (Input.GetKeyDown(KeyCode.V))
        {
            Debug.Log("[GameEndManager] DEBUG: Manual victory screen");
            ShowVictoryScreen();
        }
        if (Input.GetKeyDown(KeyCode.D))
        {
            Debug.Log("[GameEndManager] DEBUG: Manual defeat screen");
            ShowDefeatScreen();
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log("[GameEndManager] DEBUG: Manual restart");
            RestartGame();
        }
        
        // Make screens follow player if enabled
        if (followPlayer && gameEnded && playerHead != null)
        {
            if (victoryScreen != null && victoryScreen.activeSelf)
            {
                PositionScreenInFrontOfPlayer(victoryScreen);
            }
            
            if (defeatScreen != null && defeatScreen.activeSelf)
            {
                PositionScreenInFrontOfPlayer(defeatScreen);
            }
        }
    }
    
    /// <summary>
    /// Create all end game UI elements programmatically
    /// </summary>
    private void CreateEndGameScreens()
    {
        // Create a parent object for UI
        GameObject uiParent = new GameObject("EndGameUI");
        uiParent.transform.SetParent(transform);
        
        // Create victory screen
        victoryScreen = CreateScreen(uiParent.transform, "VictoryScreen", new Color32(0, 100, 0, 200));
        victoryText = CreateText(victoryScreen.transform, "VictoryText", "VICTORY!\nEnemy Base Destroyed!", Color.yellow);
        CreateRestartButton(victoryScreen.transform, "RestartButton");
        
        // Create defeat screen
        defeatScreen = CreateScreen(uiParent.transform, "DefeatScreen", new Color32(100, 0, 0, 200));
        defeatText = CreateText(defeatScreen.transform, "DefeatText", "DEFEAT\nYour Base Was Destroyed", Color.white);
        CreateRestartButton(defeatScreen.transform, "RestartButton");
        
        // Hide both screens initially
        victoryScreen.SetActive(false);
        defeatScreen.SetActive(false);
        
        Debug.Log("[GameEndManager] End game screens created");
    }
    
    /// <summary>
    /// Create a screen canvas - ADJUSTED SIZE FOR VR DISTANCE
    /// </summary>
    private GameObject CreateScreen(Transform parent, string name, Color backgroundColor)
    {
        GameObject screen = new GameObject(name);
        screen.transform.SetParent(parent);
        
        // Add Canvas
        Canvas canvas = screen.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        
        // Add Canvas Scaler
        CanvasScaler scaler = screen.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10;
        scaler.referencePixelsPerUnit = 100;
        
        // Add Graphic Raycaster for button interaction
        screen.AddComponent<GraphicRaycaster>();
        
        // Setup RectTransform - OPTIMAL SIZE for 18m distance
        RectTransform rect = screen.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(40, 20); // 40 units wide, 20 units tall
        rect.localScale = Vector3.one;
        
        // Add background image
        Image background = screen.AddComponent<Image>();
        background.color = backgroundColor;
        
        // Ensure EventSystem exists for UI interactions
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }
        
        return screen;
    }

    /// <summary>
    /// Create restart button on a screen - Positioned in bottom portion
    /// </summary>
    private void CreateRestartButton(Transform parent, string name)
    {
        GameObject buttonObj = new GameObject(name);
        buttonObj.transform.SetParent(parent);
        
        // Add Button component
        Button button = buttonObj.AddComponent<Button>();
        
        // Add Image for button background
        Image image = buttonObj.AddComponent<Image>();
        image.color = new Color32(50, 50, 50, 255);
        
        // Add child text for button
        GameObject textObj = new GameObject("RestartText");
        textObj.transform.SetParent(buttonObj.transform);
        
        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "RESTART";
        text.fontSize = 3f; // Optimized for 40x20 canvas at 18m distance
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.fontStyle = FontStyles.Bold;
        
        // Setup button RectTransform - BOTTOM PORTION
        RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.2f, 0.1f);
        buttonRect.anchorMax = new Vector2(0.8f, 0.25f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;
        
        // Ensure local scale is 1
        buttonObj.transform.localScale = Vector3.one;
        
        // Setup text RectTransform to fill button
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        textRect.localScale = Vector3.one;
        
        // Add click listener
        button.onClick.AddListener(RestartGame);
        
        // Add navigation settings
        Navigation nav = new Navigation();
        nav.mode = Navigation.Mode.Automatic;
        button.navigation = nav;
    }

    /// <summary>
    /// Create text on a screen - Positioned in top portion
    /// </summary>
    private TextMeshProUGUI CreateText(Transform parent, string name, string content, Color color)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent);
        
        // Add TextMeshPro component
        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = 4f; // Optimized for 40x20 canvas at 18m distance
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.fontStyle = FontStyles.Bold;
        text.enableWordWrapping = true;
        
        // Setup RectTransform - TOP PORTION
        RectTransform rect = textObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.1f, 0.4f);
        rect.anchorMax = new Vector2(0.9f, 0.9f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        
        return text;
    }
    
    /// <summary>
    /// Position screen in front of player at specified distance with detailed debugging
    /// </summary>
    private void PositionScreenInFrontOfPlayer(GameObject screen)
    {
        if (playerHead == null)
        {
            Debug.LogError("[GameEndManager] Cannot position screen - playerHead is null!");
            return;
        }
        
        // Log all relevant values at the start of positioning
        Debug.Log("=== GAMEENDMANAGER POSITION DEBUG ===");
        Debug.Log($"[GameEndManager] distanceFromPlayer field value: {distanceFromPlayer}");
        Debug.Log($"[GameEndManager] Player head position: {playerHead.position}");
        Debug.Log($"[GameEndManager] Player head forward: {playerHead.forward}");
        Debug.Log($"[GameEndManager] Height offset: {heightOffset}");
        
        // Check if the serialized field is being overridden
        float distanceToUse = distanceFromPlayer;
        Debug.Log($"[GameEndManager] Using distance: {distanceToUse}m");
        
        // Position in front of player
        Vector3 forward = playerHead.forward;
        forward.y = 0; // Keep level
        forward.Normalize();
        
        Debug.Log($"[GameEndManager] Normalized forward (y=0): {forward}");
        
        Vector3 targetPosition = playerHead.position + forward * distanceToUse;
        targetPosition.y = playerHead.position.y + heightOffset;
        
        Debug.Log($"[GameEndManager] Calculated target position: {targetPosition}");
        Debug.Log($"[GameEndManager] Target Z should be: {playerHead.position.z + (forward.z * distanceToUse)}");
        
        // Apply the position
        Vector3 oldPosition = screen.transform.position;
        screen.transform.position = targetPosition;
        
        Debug.Log($"[GameEndManager] Screen old position: {oldPosition}");
        Debug.Log($"[GameEndManager] Screen new position: {screen.transform.position}");
        
        // Always face the player
        screen.transform.LookAt(new Vector3(playerHead.position.x, screen.transform.position.y, playerHead.position.z));
        screen.transform.Rotate(0, 180, 0); // Flip to face camera properly
        
        // Final distance check
        float actualDistance = Vector3.Distance(playerHead.position, screen.transform.position);
        Debug.Log($"[GameEndManager] FINAL - Screen positioned at {actualDistance:F1}m from player (target was {distanceToUse}m)");
        
        if (Mathf.Abs(actualDistance - distanceToUse) > 0.1f)
        {
            Debug.LogWarning($"[GameEndManager] DISTANCE MISMATCH! Target: {distanceToUse}m, Actual: {actualDistance}m");
        }
        
        Debug.Log("=== END POSITION DEBUG ===");
    }
    
    /// <summary>
    /// Handle base destruction - determine winner and show end screen
    /// </summary>
    private void HandleBaseDestroyed(TeamSide destroyedBase)
    {
        if (gameEnded) return;
        
        gameEnded = true;
        
        Debug.Log($"[GameEndManager] EVENT TRIGGERED: Base destroyed: {destroyedBase}");
        
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
        Debug.Log("[GameEndManager] Showing victory screen");
        
        if (victoryScreen != null)
        {
            PositionScreenInFrontOfPlayer(victoryScreen);
            victoryScreen.SetActive(true);
        }
        else
        {
            Debug.LogError("[GameEndManager] Victory screen is null!");
        }
        
        if (defeatScreen != null)
            defeatScreen.SetActive(false);
    }
    
    /// <summary>
    /// Show defeat screen
    /// </summary>
    private void ShowDefeatScreen()
    {
        Debug.Log("[GameEndManager] Showing defeat screen");
        
        if (defeatScreen != null)
        {
            PositionScreenInFrontOfPlayer(defeatScreen);
            defeatScreen.SetActive(true);
        }
        else
        {
            Debug.LogError("[GameEndManager] Defeat screen is null!");
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
        Debug.Log("[GameEndManager] Game paused");
    }
    
    /// <summary>
    /// Restart the game (call from UI button)
    /// </summary>
    public void RestartGame()
    {
        Debug.Log("[GameEndManager] Restarting game...");
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