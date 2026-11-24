namespace HKMP.Rounds.Client
{
    internal static class PhantomMapDiagnostics
    {
        private const string PhantomRoomPrefix = "Hkmp.Rounds_Mantis_";
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            Modding.Logger.Log("[HKMP.Rounds] Phantom map diagnostics initialized.");
        }

        public static bool ShouldTraceScene(string sceneName = null)
        {
            if (IsRelevantScene(sceneName))
            {
                return true;
            }

            if (PhantomMapClientController.CurrentPhantomMapId > 0)
            {
                return true;
            }

            try
            {
                return IsRelevantScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            }
            catch
            {
                return false;
            }
        }

        public static bool IsRelevantScene(string sceneName = null)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return false;
            }

            string normalized = sceneName.Trim();
            return string.Equals(normalized, "Fungus2_15", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "Fungus2_31", System.StringComparison.OrdinalIgnoreCase) ||
                   normalized.StartsWith(PhantomRoomPrefix, System.StringComparison.OrdinalIgnoreCase);
        }

        public static void Record(string eventName, string details = null, bool important = false)
        {
            if (!important && !ShouldTraceScene())
            {
                return;
            }

            string name = string.IsNullOrWhiteSpace(eventName) ? "unspecified" : eventName.Trim();
            string message = "[HKMP.Rounds] PhantomMap " + name;
            if (!string.IsNullOrWhiteSpace(details))
            {
                message += ": " + details.Trim();
            }

            Modding.Logger.Log(message);
        }
    }
}
