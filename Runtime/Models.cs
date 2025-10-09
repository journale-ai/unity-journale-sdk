// Assets/JournaleClient/Runtime/Models.cs
using System;

namespace JournaleClient
{
    [Serializable] public class SessionCreateRequest
    {
        public string platform;           // "guest" | "steam"
        public string platformUserId;     // steam64 id if steam
        public string deviceId;           // guid/device id
        public bool   isGuest;
        public string steamAuthTicket;    // base64 steam ticket (if steam)
        public string projectId;          // optional
    }

    [Serializable] public class SessionCreateResponse
    {
        public string session_id;
        public string player_id;
        public string session_secret; // base64 or raw string
        public string refresh_token;
        public string jwt;
        public string expires_at;     // ISO-8601
    }

    [Serializable] public class ChatRequest
    {
        public string message;
        public string context;                // compact local history
        public string characterDescription;   // optional
        public string characterID;            // optional UUID
        public string playerDescription;      // optional
    }

    [Serializable] public class ChatResponse
    {
        public string reply;
        public ChatUsage usage;
    }

    [Serializable] public class ChatUsage
    {
        public int prompt_tokens;
        public int completion_tokens;
        public int total_tokens;
    }
}
