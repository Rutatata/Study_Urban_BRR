// Entry point: the whole game is assembled from code, so any scene (even an empty one) can run it.
using UnityEngine;

namespace Tobe.Core
{
    public static class GameBootstrap
    {
        static bool booted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (booted) return;
            booted = true;
            Application.targetFrameRate = 144;
            QualitySettings.vSyncCount = 1;
            Application.runInBackground = true;   // online match keeps simulating while alt-tabbed

            LoadProfile();

            // remove default scene camera/light if present: the view layer owns rendering
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) Object.Destroy(cam.gameObject);
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) Object.Destroy(l.gameObject);

            var root = new GameObject("TOBE");
            Object.DontDestroyOnLoad(root);
            Tobe.Net.NetSession.Create(root);
            Tobe.View.ViewRoot.Create();
            Tobe.UI.AudioManager.Create();
            Tobe.UI.UiRoot.Create();
        }

        static void LoadProfile()
        {
            var p = GameHub.LocalProfile;
            p.nick = PlayerPrefs.GetString("tobe.nick", "Игрок" + Random.Range(100, 999));
            p.style = (PlayStyle)Mathf.Clamp(PlayerPrefs.GetInt("tobe.style", (int)PlayStyle.Middle), 0, 4);
            p.model = (byte)PlayerPrefs.GetInt("tobe.model", 0);
            p.hair = (byte)PlayerPrefs.GetInt("tobe.hair", 0);
            p.number = (byte)Mathf.Clamp(PlayerPrefs.GetInt("tobe.number", 10), 1, 99);
            GameHub.LocalProfile = p;
        }
    }
}
