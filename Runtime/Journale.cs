// Assets/JournaleClient/Runtime/Journale.cs
using System.Threading.Tasks;
using UnityEngine;

namespace JournaleClient
{
    /// <summary>
    /// Public one-liner API.
    /// Usage:
    ///   Journale.Initialize(myConfigAsset);
    ///   var reply = await Journale.ChatToNpcAsync("myLocalId", "Hello!");
    /// </summary>
    public static class Journale
    {
        static SessionConfig _config;
        static GameObject _host;
        static bool _initialized;

        /// <summary>Call once at boot with your SessionConfig asset (or put one in Resources/SessionConfig).</summary>
        public static void Initialize(SessionConfig config)
        {
            if (_initialized) return;
            _config = config;

            _host = new GameObject("_JournaleRuntime");
            Object.DontDestroyOnLoad(_host);

            var sessionMgr = _host.AddComponent<SessionManager>();
            sessionMgr.config = _config;

            var svc = _host.AddComponent<JournaleService>();
            svc.config = _config;

            _initialized = true;
        }

        /// <summary>
        /// Send a message to an NPC.
        /// localId: your own unique per-NPC identifier used for local history.
        /// characterDescription/characterId: optional context for your server.
        /// playerDescriptionOverride: optional override for config default.
        /// Returns: just the NPC reply string.
        /// </summary>
        public static async Task<string> ChatToNpcAsync(
            string localId,
            string message,
            string characterDescription = null,
            string characterId = null,
            string playerDescriptionOverride = null)
        {
            EnsureInitialized();
            return await JournaleService.Instance.SendAsync(
                localId,
                message,
                characterDescription,
                characterId,
                playerDescriptionOverride
            );
        }

        static void EnsureInitialized()
        {
            if (_initialized) return;
            var cfg = _config ?? Resources.Load<SessionConfig>("SessionConfig");
            if (cfg == null)
                throw new System.SystemException("Journale.Initialize(config) not called and no Resources/SessionConfig found.");
            Initialize(cfg);
        }
    }
}
