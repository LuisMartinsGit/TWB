// Assets/Scripts/MainMenu/MenuBootstrap.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using TheWaningBorder.Menu;

/// <summary>
/// Bootstraps the menu scene by ensuring camera and menu manager exist.
/// </summary>
public static class MenuBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureMenuSceneSetup()
    {
        var sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == "Game") return; // Game scene handled by GameBootstrap

        EnsureMenuCamera();

        // Use new MainMenuManager instead of old MainMenu
        if (Object.FindObjectOfType<MainMenuManager>() == null)
        {
            var go = new GameObject("MainMenuManager");
            go.AddComponent<MainMenuManager>();
        }
    }

    public static void EnsureMenuCamera()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            var camGO = new GameObject("MenuCamera");
            camGO.tag = "MainCamera";
            cam = camGO.AddComponent<Camera>();
        }

        cam.enabled = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.08f, 0.10f, 1f);
        cam.orthographic = false;
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane  = 2000f;

        if (cam.GetComponent<AudioListener>() == null)
            cam.gameObject.AddComponent<AudioListener>();

        foreach (var l in Object.FindObjectsOfType<AudioListener>())
            if (l != null && l.gameObject != cam.gameObject)
                Object.Destroy(l);

        cam.transform.position = new Vector3(0, 10, -10);
        cam.transform.rotation = Quaternion.Euler(20, 0, 0);
    }
}
