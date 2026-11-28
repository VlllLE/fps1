using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FpsTactical
{
    /// <summary>
    /// Starts the tactical match automatically in a scene that has plant zones and team yards (Bazaar). Disabled under the
    /// test runner and by <c>FPS_TACTICAL=off</c>. Practice maps (TestRange, Shipment) never get it.
    /// </summary>
    public static class TacticalBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureForScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureForScene();
        }

        public static bool IsDisabledExternally()
        {
            string value = Environment.GetEnvironmentVariable(TacticalMatch.DisableEnvironmentVariable);
            return value == "0" || string.Equals(value, "off", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>True when the current scene is a tactical map: it holds at least one plant zone.</summary>
        public static bool SceneHasTacticalLayout()
        {
            foreach (MonoBehaviour behaviour in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (behaviour is ITacticalZone zone && zone.ZoneKind == TacticalZoneKind.Plant)
                    return true;
            }

            return false;
        }

        private static void EnsureForScene()
        {
            if (TacticalMatch.Instance != null || !BotManager.AutomaticBootstrap || IsDisabledExternally()
                || BotManager.IsRunningUnderTestRunner())
                return;
            if (UnityEngine.Object.FindFirstObjectByType<MovementController>() == null || !SceneHasTacticalLayout())
                return;

            BotManager manager = BotManager.Create();
            manager.AutoSpawnCount = 0;
            manager.KeysEnabled = false;
            TacticalMatch.Create();
        }
    }
}
