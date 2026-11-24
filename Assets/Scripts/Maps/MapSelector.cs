using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runtime map switcher. Auto-created (DontDestroyOnLoad) after the first scene loads, so no scene edits
/// are needed. Only active while the current scene is a catalog map (TestRange / Shipment / Dome).
/// F5/F6/F7 load a specific map, PageUp/PageDown cycle. Shows a small current-map label.
/// </summary>
[DefaultExecutionOrder(-40)]
public class MapSelector : MonoBehaviour
{
    private const float StatusSeconds = 4f;

    public static MapSelector Instance { get; private set; }

    private string statusMessage;
    private float statusUntil;
    private bool loading;
    private GUIStyle labelStyle;
    private GUIStyle shadowStyle;

    public bool IsLoading => loading;
    public string StatusMessage => Time.unscaledTime < statusUntil ? statusMessage : null;

    public static string CurrentSceneName => SceneManager.GetActiveScene().name;
    public static int CurrentMapIndex => MapCatalog.IndexOfScene(CurrentSceneName);
    public static bool IsActiveInCurrentScene => CurrentMapIndex >= 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static MapSelector EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        var existing = FindAnyObjectByType<MapSelector>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        var go = new GameObject("MapSelector");
        DontDestroyOnLoad(go);
        return go.AddComponent<MapSelector>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void Update()
    {
        if (!IsActiveInCurrentScene || loading)
            return;

        for (int i = 0; i < MapCatalog.Count; i++)
        {
            if (Input.GetKeyDown(MapCatalog.Maps[i].HotKey))
            {
                TryLoadMap(i);
                return;
            }
        }

        if (Input.GetKeyDown(KeyCode.PageDown))
            CycleMap(1);
        else if (Input.GetKeyDown(KeyCode.PageUp))
            CycleMap(-1);
    }

    public bool IsMapAvailable(int index)
    {
        return index >= 0 && index < MapCatalog.Count && MapCatalog.IsInBuild(MapCatalog.Maps[index].SceneName);
    }

    public bool CycleMap(int delta)
    {
        int next = MapCatalog.Cycle(CurrentMapIndex, delta, MapCatalog.Count, IsMapAvailable);
        if (next < 0)
        {
            SetStatus("No other map is in Build Settings.");
            return false;
        }
        return TryLoadMap(next);
    }

    public bool TryLoadMap(int index)
    {
        if (index < 0 || index >= MapCatalog.Count)
            return false;
        return TryLoadMap(MapCatalog.Maps[index].SceneName);
    }

    public bool TryLoadMap(string sceneName)
    {
        if (loading)
            return false;

        int index = MapCatalog.IndexOfScene(sceneName);
        string display = index >= 0 ? MapCatalog.Maps[index].DisplayName : sceneName;

        if (!MapCatalog.IsInBuild(sceneName))
        {
            SetStatus($"{display} is not in Build Settings (generate it via Tools > Maps).");
            return false;
        }

        if (index >= 0 && index == CurrentMapIndex)
        {
            SetStatus($"Already in {display}.");
            return false;
        }

        loading = true;
        RestorePlayState();
        SetStatus($"Loading {display}...");
        SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        return true;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!loading)
            return;
        RestorePlayState();
        StartCoroutine(FinishLoad());
    }

    private IEnumerator FinishLoad()
    {
        // Player components run Start (which locks the cursor) after sceneLoaded; re-apply once afterwards.
        yield return null;
        RestorePlayState();
        loading = false;
        statusMessage = null;
    }

    public static void RestorePlayState()
    {
        Time.timeScale = 1f;
        var cam = FindAnyObjectByType<FirstPersonCamera>();
        if (cam != null)
        {
            cam.SetCursorLock(true);
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void SetStatus(string message)
    {
        statusMessage = message;
        statusUntil = Time.unscaledTime + StatusSeconds;
        Debug.Log("[MapSelector] " + message);
    }

    private void OnGUI()
    {
        if (!IsActiveInCurrentScene)
            return;

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.UpperRight
            };
            labelStyle.normal.textColor = new Color(0.9f, 0.95f, 1f);
            shadowStyle = new GUIStyle(labelStyle);
            shadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
        }

        int index = CurrentMapIndex;
        string text = $"Map: {MapCatalog.Maps[index].DisplayName}   [F5 TestRange · F6 Shipment · F7 Dome · PgUp/PgDn]";
        string status = StatusMessage;
        if (!string.IsNullOrEmpty(status))
            text += "\n" + status;

        var rect = new Rect(Screen.width - 520f, 6f, 510f, 40f);
        GUI.depth = 10;
        GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, shadowStyle);
        GUI.Label(rect, text, labelStyle);
    }
}
