// -----------------------------------------------------------------------------
// ZellSimulation – 3D-Zell-Evolution mit Immunsystem
// Copyright © 2026 Michael Bogen. Alle Rechte vorbehalten.
// Datei: SceneFlow.cs
// -----------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZellSimulation.Flow
{
    /// <summary>
    /// Szenenablauf: ZS_Loading → ZS_MainMenu → ZS_Loading → ZS_Simulation.
    /// Jeder Wechsel läuft über den Loading-Screen, der die Zielszene asynchron lädt.
    /// </summary>
    public static class SceneFlow
    {
        public const string LoadingScene = "ZS_Loading";
        public const string MainMenuScene = "ZS_MainMenu";
        public const string SimulationScene = "ZS_Simulation";

        static string s_PendingTarget;

        /// <summary>True, solange der allererste Ladevorgang (App-Start) noch nicht abgeschlossen ist.</summary>
        public static bool IsFirstLoad { get; private set; } = true;

        // Statischen Zustand auch bei deaktiviertem Domain-Reload (Enter Play Mode Options) zurücksetzen.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_PendingTarget = null;
            IsFirstLoad = true;
        }

        /// <summary>Lädt <paramref name="targetScene"/> über den Loading-Screen.</summary>
        public static void LoadWithLoadingScreen(string targetScene)
        {
            Time.timeScale = 1f;
            s_PendingTarget = targetScene;
            if (CanLoad(LoadingScene)) SceneManager.LoadScene(LoadingScene);
            else if (CanLoad(targetScene)) SceneManager.LoadScene(targetScene);
            else Debug.LogError($"[ZellSimulation] Szene '{targetScene}' ist nicht in den Build Settings. " +
                                "Bitte Tools > ZellSimulation > 3. Loading-Screen & Hauptmenü einrichten ausführen.");
        }

        /// <summary>Liefert das Ziel des aktuellen Ladevorgangs (oder den Fallback) und setzt es zurück.</summary>
        public static string ConsumePendingTarget(string fallback)
        {
            string t = string.IsNullOrEmpty(s_PendingTarget) ? fallback : s_PendingTarget;
            s_PendingTarget = null;
            return t;
        }

        internal static void MarkLoaded() => IsFirstLoad = false;

        /// <summary>Ist die Szene in den Build Settings eingetragen?</summary>
        public static bool CanLoad(string sceneName) =>
            !string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);

        /// <summary>Beendet die Anwendung (im Editor: Play-Mode verlassen).</summary>
        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
