// Assets/JournaleSDK/Runtime/SessionConfig.cs
using UnityEngine;

namespace JournaleSDK
{
    public enum AuthPlatform { Guest, Steam }

    [CreateAssetMenu(fileName="SessionConfig", menuName="JournaleSDK/Session Config", order=0)]
    public class SessionConfig : ScriptableObject
    {
        [Header("Server")]
        public string apiBaseUrl = "https://api.journale.ai";
        public string sessionCreatePath = "/session/create";
        public string chatPath = "/chat/player";

        [Header("Project")]
        public string projectId = ""; // set it once

        [Header("Auth")]
        public AuthPlatform platform = AuthPlatform.Guest;
        public string deviceIdOverride = "";
        public bool allowGuestFallbackIfSteamMissing = true;

        [Header("Client Behavior")]
        public int  maxHistoryLinesForContext = 16;
        public int  maxRetriesOn429 = 2;
        public float baseBackoffSeconds = 0.6f;

        [Header("Player")]
        [TextArea(2,6)] public string defaultPlayerDescription = "A curious player testing NPC chat.";
    }
}
