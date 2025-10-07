using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace JournaleSDK
{
    public class SessionManager : MonoBehaviour
    {
        public static SessionManager Instance { get; private set; }

        [Header("Assign SessionConfig")]
        public SessionConfig config;

        public string SessionId { get; private set; }
        public string PlayerId  { get; private set; }
        public string Jwt       { get; private set; }

        byte[]   sessionSecret;
        DateTime expiresAt;

        // Concurrency control for session creation
        Task _ensureTask;
        readonly SemaphoreSlim _sessionGate = new(1, 1);

        const string DeviceKey = "journale_device_id";

        void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        string EnsureDeviceId()
        {
            var id = string.IsNullOrEmpty(config.deviceIdOverride)
                ? PlayerPrefs.GetString(DeviceKey, "")
                : config.deviceIdOverride;

            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(DeviceKey, id);
                PlayerPrefs.Save();
            }
            return id;
        }

        public bool IsSessionValid()
        {
            return !string.IsNullOrEmpty(SessionId)
                && !string.IsNullOrEmpty(Jwt)
                && DateTime.UtcNow < expiresAt;
        }

        public async Task EnsureSessionAsync()
        {
            if (IsSessionValid()) return;

            await _sessionGate.WaitAsync();
            try
            {
                // Re-check after entering the gate in case another caller finished it.
                if (IsSessionValid()) return;

                if (_ensureTask != null)
                {
                    try { await _ensureTask; }
                    catch { /* bubble on next call if needed */ }
                    return;
                }

                var outerTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _ensureTask = outerTcs.Task;
                try
                {
                    string platform = (config.platform == AuthPlatform.Steam) ? "steam" : "guest";
                    string platformUserId = null;
                    string steamTicket = null;

                    if (config.platform == AuthPlatform.Steam)
                    {
                        if (SteamHelper.TryGetSteamIdentity(out var sid, out var ticket))
                        {
                            platformUserId = sid;
                            steamTicket = ticket;
                        }
                        else if (!config.allowGuestFallbackIfSteamMissing)
                        {
                            throw new Exception("Steam not available and guest fallback disabled.");
                        }
                        else
                        {
                            platform = "guest";
                        }
                    }

                    var req = new SessionCreateRequest
                    {
                        platform        = platform,
                        platformUserId  = platformUserId,
                        deviceId        = EnsureDeviceId(),
                        isGuest         = platform == "guest",
                        steamAuthTicket = steamTicket,
                        projectId       = config.projectId
                    };

                    string url = config.apiBaseUrl.TrimEnd('/') + config.sessionCreatePath;
                    string json = JsonUtility.ToJson(req);

                    // Extra diagnostics (sanitize secrets)
                    try
                    {
                        var dbgTicketLen = string.IsNullOrEmpty(steamTicket) ? 0 : steamTicket.Length;
                        Debug.Log($"[JOURNALE] Creating session → url={url} platform={platform} platformUserId={(platformUserId??"<null>")} deviceId={req.deviceId} projectId={(config.projectId??"<null>")} steamTicketLen={dbgTicketLen}");
                    }
                    catch {}

                    using var www = new UnityWebRequest(url, "POST");
                    www.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    www.downloadHandler = new DownloadHandlerBuffer();
                    www.SetRequestHeader("Content-Type", "application/json");

                    var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    www.SendWebRequest().completed += _ => tcs.SetResult(true);
                    await tcs.Task;

                    string raw = www.downloadHandler?.text ?? "";
                    Debug.Log($"[JOURNALE] /session/create status={(int)www.responseCode} ok={(www.result==UnityWebRequest.Result.Success)}");
                    try
                    {
                        var headers = www.GetResponseHeaders();
                        if (headers != null)
                        {
                            // Print a small subset of headers for troubleshooting
                            headers.TryGetValue("x-supabase-function-id", out var fnId);
                            headers.TryGetValue("content-type", out var ct);
                            Debug.Log($"[JOURNALE] /session/create hdr content-type={ct} function-id={fnId}");
                        }
                    }
                    catch {}
                    if (www.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning($"[JOURNALE] /session/create HTTP {(int)www.responseCode} body: {Compact(raw)}");
                        throw new Exception($"Session create failed: HTTP {www.responseCode}: {www.error}");
                    }
                    Debug.Log($"[JOURNALE] /session/create response: {Compact(raw)}");

                    var resp = JsonUtility.FromJson<SessionCreateResponse>(raw);
                    if (resp == null || string.IsNullOrEmpty(resp.session_id) || string.IsNullOrEmpty(resp.jwt))
                        throw new Exception("Bad session response");

                    SessionId = resp.session_id;
                    PlayerId  = resp.player_id;
                    Jwt       = resp.jwt;

                    Debug.Log($"[JOURNALE] JWT: {Jwt}");

                    try { sessionSecret = Convert.FromBase64String(resp.session_secret); }
                    catch { sessionSecret = Encoding.UTF8.GetBytes(resp.session_secret ?? ""); }

                    // Prefer JWT exp if available; else fall back to server-provided expires_at; else 30m.
                    if (!TryGetJwtExpUnixSeconds(Jwt, out var expUnix))
                    {
                        if (!DateTime.TryParse(resp.expires_at, out expiresAt))
                            expiresAt = DateTime.UtcNow.AddMinutes(30);
                    }
                    else
                    {
                        expiresAt = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
                    }

                    outerTcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    outerTcs.TrySetException(ex);
                    throw;
                }
                finally
                {
                    _ensureTask = null;
                }
            }
            finally
            {
                _sessionGate.Release();
            }
        }

        // canonical: METHOD \n PATH \n NONCE \n TS \n BODY
        string BuildCanonical(string method, string path, string nonce, string ts, string body)
        {
            return $"{method}\n{path}\n{nonce}\n{ts}\n{body}";
        }

        string HmacSign(string canonical)
        {
            using var h = new HMACSHA256(sessionSecret);
            var sig = h.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            return Convert.ToBase64String(sig);
        }

        public async Task<(UnityWebRequest req, TaskCompletionSource<bool> waiter)> SignedPostAsync(string path, string jsonBody)
        {
            await EnsureSessionAsync();

            string nonce = Guid.NewGuid().ToString("N");
            string ts    = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

            string url = config.apiBaseUrl.TrimEnd('/') + path;

            // Use configured path as canonical (must match server verify)
            string canonicalPath = path.StartsWith("/") ? path : "/" + path;

            string canonical = BuildCanonical("POST", canonicalPath, nonce, ts, jsonBody);
            string sig = HmacSign(canonical);

            // ---- DEBUG: print headers + canonical for copy/paste ----
            Debug.Log($"[JOURNALE] Signed POST {url}");
            Debug.Log($"[JOURNALE] X-Session-Id: {SessionId}");
            Debug.Log($"[JOURNALE] X-Nonce: {nonce}");
            Debug.Log($"[JOURNALE] X-Ts: {ts}");
            Debug.Log($"[JOURNALE] Canonical (multi-line):\n{canonical}");
            Debug.Log($"[JOURNALE] Canonical (one-line): {canonical.Replace('\n','|')}");
            Debug.Log($"[JOURNALE] X-Signature: {sig}");

            var www = new UnityWebRequest(url, "POST");
            www.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("Authorization", "Bearer " + Jwt);
            www.SetRequestHeader("X-Session-Id", SessionId);
            www.SetRequestHeader("X-Nonce", nonce);
            www.SetRequestHeader("X-Ts", ts);
            www.SetRequestHeader("X-Signature", sig);

            var tcs = new TaskCompletionSource<bool>();
            www.SendWebRequest().completed += _ => tcs.SetResult(true);
            return (www, tcs);
        }

        // Try to read "exp" from JWT payload without external deps
        static bool TryGetJwtExpUnixSeconds(string jwt, out long exp)
        {
            exp = 0;
            try
            {
                if (string.IsNullOrEmpty(jwt)) return false;
                var parts = jwt.Split('.');
                if (parts.Length < 2) return false;
                string payload = parts[1].Replace('-', '+').Replace('_', '/');
                switch (payload.Length % 4)
                {
                    case 2: payload += "=="; break;
                    case 3: payload += "="; break;
                }
                var bytes = Convert.FromBase64String(payload);
                var json  = Encoding.UTF8.GetString(bytes);

                var keyIdx = json.IndexOf("\"exp\"", StringComparison.Ordinal);
                if (keyIdx < 0) keyIdx = json.IndexOf("'exp'", StringComparison.Ordinal);
                if (keyIdx < 0) return false;

                var colon = json.IndexOf(':', keyIdx);
                if (colon < 0) return false;
                int i = colon + 1;
                while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
                int start = i;
                while (i < json.Length && char.IsDigit(json[i])) i++;
                if (i <= start) return false;

                var num = json.Substring(start, i - start);
                return long.TryParse(num, out exp);
            }
            catch { return false; }
        }

        static string Compact(string s) => (s ?? string.Empty).Replace("\n", " ").Replace("\r", " ");
    }
}
