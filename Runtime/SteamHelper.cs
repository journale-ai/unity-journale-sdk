// Assets/JournaleClient/Runtime/SteamHelper.cs
using System;
using System.Threading;
using UnityEngine;

namespace JournaleClient
{
    // Uses Steamworks.NET when JOURNALE_STEAMWORKS define is present in Player Settings.
    // Falls back to false without hard dependency if Steamworks isn't installed.
    public static class SteamHelper
    {
        /// <summary>
        /// Attempts to get the current user's SteamID64 and a Web API auth ticket (base64).
        /// Requires Steamworks.NET and an initialized Steam API context.
        /// Returns false if Steam isn't available or ticket couldn't be produced within timeout.
        /// </summary>
        public static bool TryGetSteamIdentity(out string steamId64, out string ticketB64)
        {
            steamId64 = null;
            ticketB64 = null;

#if STEAMWORKS_NET
            try
            {
                // --- Steamworks.NET path ---
                // Guard: ensure Steam is running and API is initialized.
                // If your project manages Steam init elsewhere (e.g., SteamManager), this will be fast.
                if (!Steamworks.SteamAPI.IsSteamRunning())
                {
                    Debug.LogWarning("[JOURNALE] Steam is not running. Please launch the Steam client and run the game under Steam.");
                    return false;
                }

                // Attempt init if not already. If already initialized, Steamworks.NET will keep it alive.
                bool initialized = false;
                try { initialized = Steamworks.SteamAPI.Init(); } catch {}
                if (!initialized)
                {
                    Debug.LogWarning("[JOURNALE] Steam API failed to initialize. Ensure Steamworks.NET is imported, libsteam_api is present, and steam_appid.txt is set for dev.");
                    return false;
                }

                steamId64 = Steamworks.SteamUser.GetSteamID().m_SteamID.ToString();

                // Prefer the widely-supported session ticket path for compatibility.
                // This ticket is accepted by ISteamUserAuth/AuthenticateUserTicket.
                byte[] buffer = new byte[2048];
                uint   size;
                Steamworks.SteamNetworkingIdentity identity = new Steamworks.SteamNetworkingIdentity();
                identity.Clear(); // optional, ensures it's empty/default

                var hTicket = Steamworks.SteamUser.GetAuthSessionTicket(
                    buffer,
                    buffer.Length,
                    out size,
                    ref identity
                );
                if (hTicket.m_HAuthTicket == 0 || size == 0)
                {
                    Debug.LogWarning("[JOURNALE] GetAuthSessionTicket failed or returned empty ticket.");
                    return false;
                }

                var ticketBytes = new byte[size];
                Array.Copy(buffer, ticketBytes, (int)size);

                ticketB64 = BytesToHex(ticketBytes);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[JOURNALE] SteamHelper error: {ex.Message}");
                return false;
            }
#else
            // --- No Steamworks available ---
            Debug.LogWarning("[JOURNALE] Steamworks not compiled in. Define STEAMWORKS_NET (Steamworks.NET default) and add Steamworks.NET.");
            return false;
#endif
        }

        static string BytesToHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return string.Empty;
            char[] c = new char[bytes.Length * 2];
            const string hex = "0123456789abcdef";
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                c[i * 2]     = hex[(b >> 4) & 0xF];
                c[i * 2 + 1] = hex[b & 0xF];
            }
            return new string(c);
        }
    }
}
