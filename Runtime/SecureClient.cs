// Assets/JournaleClient/Runtime/SecureClient.cs
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace JournaleClient
{
    public class SecureClient
    {
        readonly SessionConfig _cfg;

        public SecureClient(SessionConfig cfg) { _cfg = cfg; }

        public async Task<ChatResponse> ChatAsync(ChatRequest payload)
        {
            string path = _cfg.chatPath;
            string body = JsonUtility.ToJson(payload);

            int tries = 0;
            while (true)
            {
                var (req, waiter) = await SessionManager.Instance.SignedPostAsync(path, body);
                await waiter.Task;

                string respText = req.downloadHandler?.text ?? "";

                if (req.result == UnityWebRequest.Result.Success)
                {
                    // ---- DEBUG: log /chat/player JSON for copy/paste ----
                    Debug.Log($"[JOURNALE] /chat/player response: {Compact(respText)}");

                    var resp = JsonUtility.FromJson<ChatResponse>(respText);
                    if (resp == null) throw new Exception("Bad JSON from /chat/player");
                    return resp;
                }

                // Backoff on 429
                if ((int)req.responseCode == 429 && tries < _cfg.maxRetriesOn429)
                {
                    tries++;
                    float delay = _cfg.baseBackoffSeconds * (float)Math.Pow(2, tries - 1);
                    await Task.Delay(TimeSpan.FromSeconds(delay));
                    continue;
                }

                // ---- Filter out HTML error pages and only log relevant error info ----
                string errorMsg = GetReadableError(respText, req);
                Debug.LogWarning($"[JOURNALE] /chat/player HTTP {(int)req.responseCode}: {errorMsg}");
                throw new Exception($"HTTP {req.responseCode}: {errorMsg}");
            }
        }

        /// <summary>
        /// Extracts a readable error message from the response, filtering out HTML error pages.
        /// </summary>
        static string GetReadableError(string respText, UnityWebRequest req)
        {
            // Check if response is HTML (common for proxy errors like Cloudflare 502)
            if (!string.IsNullOrEmpty(respText) && (
                respText.TrimStart().StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
                respText.TrimStart().StartsWith("<html", StringComparison.OrdinalIgnoreCase)))
            {
                // Extract basic info from HTML if possible, otherwise return generic message
                string title = ExtractHtmlTitle(respText);
                if (!string.IsNullOrEmpty(title))
                {
                    return $"Server error: {title}";
                }
                return "Server error (HTML error page received)";
            }

            // If it's not HTML, try to parse as JSON error or return the text
            if (!string.IsNullOrEmpty(respText))
            {
                // Limit length to avoid bloat in logs/chat
                if (respText.Length > 200)
                {
                    return respText.Substring(0, 200) + "...";
                }
                return respText;
            }

            // Fallback to Unity's error message
            return string.IsNullOrEmpty(req.error) ? "Unknown error" : req.error;
        }

        /// <summary>
        /// Attempts to extract the title from an HTML error page.
        /// </summary>
        static string ExtractHtmlTitle(string html)
        {
            int titleStart = html.IndexOf("<title>", StringComparison.OrdinalIgnoreCase);
            if (titleStart < 0) return null;
            
            titleStart += 7; // length of "<title>"
            int titleEnd = html.IndexOf("</title>", titleStart, StringComparison.OrdinalIgnoreCase);
            if (titleEnd < 0) return null;
            
            return html.Substring(titleStart, titleEnd - titleStart).Trim();
        }

        static string Compact(string s) => (s ?? string.Empty).Replace("\n", " ").Replace("\r", " ");
    }
}
