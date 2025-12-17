using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class PreloaderController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Slider loadingSlider;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private TMP_Text loadingTipText;

    [Header("Settings")]
    [SerializeField] private string targetSceneName = "MainMenu";
    [SerializeField] private float minimumLoadTime = 2f;
    [SerializeField] private float tipChangeInterval = 3f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

    [Header("Loading Tips")]
    [SerializeField]
    private string[] loadingTips = {
    "POKER TIP: Position is power - play tighter early, looser late",
    "POKER TIP: Fold more hands than you play - patience wins",
    "POKER TIP: Watch opponent bet sizes - they reveal hand strength",
    "POKER TIP: Premium hands win small pots, bluffs win big ones",

    "POKER TIP: Your table image affects how others play against you",

    "POKER TIP: Bluff with hands that have some equity",
    "POKER TIP: Semi-bluff with draws is most profitable",
    "POKER TIP: Don't bluff calling stations",

    "POKER TIP: Poker is a marathon, not a sprint",
    "POKER TIP: Take breaks after big losses to avoid tilt",

    "POKER TIP: Poker is a game of people played with cards",
    "POKER TIP: Money saved is as important as money won",
    
    // Last 4 tips (always shown at the end)
    "Loading your poker chips...",
    "Shuffling the deck...",
    "Dealing your cards...",
    "Ante up! Game starting soon..."
};

    private AsyncOperation loadingOperation;
    private float loadProgress = 0f;
    private float timer = 0f;
    private float tipTimer = 0f;
    private bool isLoadingComplete = false;
    private List<string> shuffledTips = new List<string>();
    private int currentTipIndex = 0;
    private const int LAST_TIPS_COUNT = 4;

    void Start()
    {
        if (enableDebugLogs) Debug.Log("[Preloader] Starting preloader...");

        // First, check if our target scene exists
        if (!IsSceneInBuildSettings(targetSceneName))
        {
            Debug.LogError($"[Preloader] ERROR: Target scene '{targetSceneName}' not found in Build Settings!");
            ListAllScenesInBuildSettings();

            // Show error to player
            if (loadingTipText != null)
                loadingTipText.text = $"Error: Cannot load '{targetSceneName}'";
            return;
        }

        // Initialize tips
        PrepareTips();
        ShowNextTip();

        // Start loading the Main Menu scene
        StartCoroutine(LoadTargetScene());
    }

    void Update()
    {
        // Update tip rotation if we have multiple tips
        if (shuffledTips.Count > 1 && !isLoadingComplete)
        {
            tipTimer += Time.deltaTime;

            if (tipTimer >= tipChangeInterval)
            {
                tipTimer = 0f;
                ShowNextTip();
            }
        }
    }

    void PrepareTips()
    {
        if (enableDebugLogs) Debug.Log($"[Preloader] Preparing {loadingTips.Length} tips...");

        // Separate the tips
        if (loadingTips.Length <= LAST_TIPS_COUNT)
        {
            shuffledTips = new List<string>(loadingTips);
            return;
        }

        int randomTipsCount = loadingTips.Length - LAST_TIPS_COUNT;
        string[] randomTips = new string[randomTipsCount];
        for (int i = 0; i < randomTipsCount; i++)
        {
            randomTips[i] = loadingTips[i];
        }

        string[] lastTips = new string[LAST_TIPS_COUNT];
        for (int i = 0; i < LAST_TIPS_COUNT; i++)
        {
            lastTips[i] = loadingTips[loadingTips.Length - LAST_TIPS_COUNT + i];
        }

        ShuffleArray(randomTips);

        shuffledTips.Clear();
        shuffledTips.AddRange(randomTips);
        shuffledTips.AddRange(lastTips);

        if (enableDebugLogs) Debug.Log($"[Preloader] {shuffledTips.Count} tips prepared (last 4 fixed)");
    }

    void ShowNextTip()
    {
        if (loadingTipText != null && shuffledTips.Count > 0)
        {
            loadingTipText.text = shuffledTips[currentTipIndex];
            currentTipIndex = (currentTipIndex + 1) % shuffledTips.Count;

            StartCoroutine(FadeTipText());
        }
    }

    IEnumerator FadeTipText()
    {
        if (loadingTipText == null) yield break;

        Color originalColor = loadingTipText.color;
        float fadeDuration = 0.3f;
        float elapsedTime = 0f;

        // Fade out
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration);
            loadingTipText.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            yield return null;
        }

        // Fade in
        elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, elapsedTime / fadeDuration);
            loadingTipText.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
            yield return null;
        }

        loadingTipText.color = originalColor;
    }

    void ShuffleArray<T>(T[] array)
    {
        System.Random rng = new System.Random();
        int n = array.Length;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            T value = array[k];
            array[k] = array[n];
            array[n] = value;
        }
    }

    IEnumerator LoadTargetScene()
    {
        if (enableDebugLogs) Debug.Log($"[Preloader] Starting to load: {targetSceneName}");

        // Start loading the Main Menu scene asynchronously
        loadingOperation = SceneManager.LoadSceneAsync(targetSceneName);

        if (loadingOperation == null)
        {
            Debug.LogError("[Preloader] Failed to create AsyncOperation!");
            yield break;
        }

        // Don't allow scene activation immediately
        loadingOperation.allowSceneActivation = false;

        // Reset values
        loadProgress = 0f;
        timer = 0f;
        isLoadingComplete = false;

        if (enableDebugLogs) Debug.Log("[Preloader] Entering loading loop...");

        // Loading loop
        while (!isLoadingComplete)
        {
            timer += Time.deltaTime;

            // Calculate progress
            float operationProgress = Mathf.Clamp01(loadingOperation.progress / 0.9f);

            if (enableDebugLogs && Time.frameCount % 30 == 0)
                Debug.Log($"[Preloader] Progress: {operationProgress:F2}, Timer: {timer:F2}");

            // Smooth progress
            loadProgress = Mathf.Lerp(loadProgress, operationProgress, Time.deltaTime * 5f);

            // Ensure minimum load time
            if (timer >= minimumLoadTime && operationProgress >= 0.9f)
            {
                loadProgress = 1f;
                isLoadingComplete = true;
                if (enableDebugLogs) Debug.Log("[Preloader] Loading complete!");
            }

            // Update UI
            UpdateProgressUI();

            yield return null;
        }

        if (enableDebugLogs) Debug.Log("[Preloader] Starting fade out...");

        // Add a final fade out before switching scenes
        yield return StartCoroutine(FadeOutLoadingScreen());

        if (enableDebugLogs) Debug.Log("[Preloader] Activating scene...");

        // Allow scene activation - this will switch to Main Menu scene
        loadingOperation.allowSceneActivation = true;
    }

    void UpdateProgressUI()
    {
        if (loadingSlider != null)
        {
            loadingSlider.value = loadProgress;
        }

        if (progressText != null)
        {
            int percentage = Mathf.RoundToInt(loadProgress * 100);
            progressText.text = $"{percentage}%";
        }
    }

    IEnumerator FadeOutLoadingScreen()
    {
        // Get the CanvasGroup of the entire loading screen
        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // Fade out over 0.5 seconds
        float fadeTime = 0.5f;
        float elapsedTime = 0f;

        while (elapsedTime < fadeTime)
        {
            elapsedTime += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeTime);
            yield return null;
        }
    }

    bool IsSceneInBuildSettings(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string nameFromPath = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            if (nameFromPath == sceneName)
            {
                return true;
            }
        }
        return false;
    }

    void ListAllScenesInBuildSettings()
    {
        Debug.Log("=== Scenes in Build Settings ===");
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            Debug.Log($"[{i}] {sceneName} (Path: {scenePath})");
        }
        Debug.Log("===============================");
    }

    // FIXED OnValidate method
    void OnValidate()
    {
        Debug.Log("[Preloader OnValidate] Checking scenes...");

        // Ensure target scene exists in Build Settings
        if (!string.IsNullOrEmpty(targetSceneName))
        {
            bool sceneExists = false;
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                if (sceneName == targetSceneName)  // Using variable, NOT hardcoded
                {
                    sceneExists = true;
                    Debug.Log($"[Preloader OnValidate] ✓ Found scene: {targetSceneName}");
                    break;
                }
            }

            if (!sceneExists)
            {
                // USING THE VARIABLE, not hardcoded "MainMenu"
                Debug.LogWarning($"[Preloader OnValidate] Scene '{targetSceneName}' not found in Build Settings!");
                ListAllScenesInBuildSettings();
            }
        }
        else
        {
            Debug.LogWarning("[Preloader OnValidate] targetSceneName is empty!");
        }
    }
}