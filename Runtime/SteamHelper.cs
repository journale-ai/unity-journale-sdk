// Assets/JournaleClient/Runtime/SteamHelper.cs
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace JournaleClient
{
    // Attempts to bind to Steamworks.NET dynamically so teams can ship without the plugin.
    // Falls back to guest mode (false) when Steamworks isn't available at runtime.
    public static class SteamHelper
    {
        /// <summary>
    /// Attempts to get the current user's SteamID64 and a Web API auth ticket (hex).
    /// Dynamically binds to Steamworks.NET if the assembly is present and initialized.
    /// Returns false if Steam isn't available, Steamworks isn't installed, or a ticket couldn't be produced.
        /// </summary>
        public static bool TryGetSteamIdentity(out string steamId64, out string ticketB64)
        {
            steamId64 = null;
            ticketB64 = null;

            if (!SteamworksBindings.TryGetSteamIdentity(out steamId64, out ticketB64))
            {
                Debug.LogWarning("[JOURNALE] Steamworks.NET not detected or failed to resolve at runtime. Falling back to guest mode.");
                return false;
            }

            return true;
        }

        private static class SteamworksBindings
        {
            private static bool _attemptedResolve;
            private static bool _isAvailable;

            private static MethodInfo _isSteamRunning;
            private static MethodInfo _init;
            private static MethodInfo _getSteamId;
            private static MethodInfo _getAuthSessionTicket;
            private static MethodInfo _identityClear;
            private static FieldInfo _authTicketValueField;

            private static Type _steamUserType;
            private static Type _authTicketType;
            private static Type _identityType;

            internal static bool TryGetSteamIdentity(out string steamId64, out string ticketB64)
            {
                steamId64 = null;
                ticketB64 = null;

                if (!EnsureBindings())
                {
                    return false;
                }

                try
                {
                    if (!(bool)_isSteamRunning.Invoke(null, null))
                    {
                        Debug.LogWarning("[JOURNALE] Steam is not running. Please launch the Steam client and run the game under Steam.");
                        return false;
                    }

                    bool initialized;
                    try
                    {
                        initialized = (bool)_init.Invoke(null, null);
                    }
                    catch
                    {
                        initialized = false;
                    }

                    if (!initialized)
                    {
                        Debug.LogWarning("[JOURNALE] Steam API failed to initialize. Ensure Steamworks.NET is imported, libsteam_api is present, and steam_appid.txt is set for dev.");
                        return false;
                    }

                    var steamIdInstance = _getSteamId.Invoke(null, null);
                    if (steamIdInstance == null)
                    {
                        Debug.LogWarning("[JOURNALE] Unable to retrieve SteamID.");
                        return false;
                    }

                    steamId64 = steamIdInstance.ToString();

                    byte[] buffer = new byte[2048];
                    object[] args = BuildAuthTicketArgs(buffer, out int sizeIndex);

                    var hTicket = _getAuthSessionTicket.Invoke(null, args);
                    if (!TryExtractAuthTicketHandle(hTicket))
                    {
                        Debug.LogWarning("[JOURNALE] GetAuthSessionTicket failed to produce a valid handle.");
                        return false;
                    }

                    if (!(args[sizeIndex] is uint size) || size == 0)
                    {
                        Debug.LogWarning("[JOURNALE] GetAuthSessionTicket returned an empty ticket.");
                        return false;
                    }

                    var ticketBytes = new byte[size];
                    Array.Copy(buffer, ticketBytes, (int)size);

                    ticketB64 = BytesToHex(ticketBytes);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[JOURNALE] SteamHelper runtime bind error: {ex.Message}");
                    return false;
                }
            }

            private static bool EnsureBindings()
            {
                if (_attemptedResolve)
                {
                    return _isAvailable;
                }

                _attemptedResolve = true;

                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var steamApi = assembly.GetType("Steamworks.SteamAPI");
                    if (steamApi == null)
                    {
                        continue;
                    }

                    _isSteamRunning = steamApi.GetMethod("IsSteamRunning", BindingFlags.Public | BindingFlags.Static);
                    _init = steamApi.GetMethod("Init", BindingFlags.Public | BindingFlags.Static);

                    _steamUserType = assembly.GetType("Steamworks.SteamUser");
                    _getSteamId = _steamUserType?.GetMethod("GetSteamID", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);

                    _authTicketType = assembly.GetType("Steamworks.HAuthTicket");
                    _identityType = assembly.GetType("Steamworks.SteamNetworkingIdentity");

                    _getAuthSessionTicket = ResolveAuthTicketMethod(_steamUserType);
                    _authTicketValueField = _authTicketType?.GetField("m_HAuthTicket", BindingFlags.Public | BindingFlags.Instance);
                    _identityClear = _identityType?.GetMethod("Clear", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);

                    if (new[]
                        {
                            _isSteamRunning,
                            _init,
                            _getSteamId,
                            _getAuthSessionTicket,
                        }.All(mi => mi != null))
                    {
                        _isAvailable = true;
                        break;
                    }
                }

                if (!_isAvailable)
                {
                    Debug.Log("[JOURNALE] Steamworks.NET assembly not found. Steam login is disabled.");
                }

                return _isAvailable;
            }

            private static MethodInfo ResolveAuthTicketMethod(Type steamUserType)
            {
                if (steamUserType == null)
                {
                    return null;
                }

                return steamUserType
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name == "GetAuthSessionTicket")
                    .OrderByDescending(m => m.GetParameters().Length)
                    .FirstOrDefault(m =>
                    {
                        var parameters = m.GetParameters();
                        if (parameters.Length < 3)
                        {
                            return false;
                        }

                        if (parameters[0].ParameterType != typeof(byte[]))
                        {
                            return false;
                        }

                        if (parameters[1].ParameterType != typeof(int))
                        {
                            return false;
                        }

                        if (!parameters[2].ParameterType.IsByRef || parameters[2].ParameterType.GetElementType() != typeof(uint))
                        {
                            return false;
                        }

                        return true;
                    });
            }

            private static object[] BuildAuthTicketArgs(byte[] buffer, out int sizeIndex)
            {
                var parameters = _getAuthSessionTicket.GetParameters();
                var args = new object[parameters.Length];

                args[0] = buffer;
                args[1] = buffer.Length;

                sizeIndex = 2;
                args[sizeIndex] = 0u;

                if (parameters.Length > 3)
                {
                    object identityInstance = null;
                    if (_identityType != null)
                    {
                        try
                        {
                            identityInstance = Activator.CreateInstance(_identityType);
                            _identityClear?.Invoke(identityInstance, null);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[JOURNALE] Failed to create SteamNetworkingIdentity: {ex.Message}");
                        }
                    }

                    args[3] = identityInstance;
                }

                return args;
            }

            private static bool TryExtractAuthTicketHandle(object hTicket)
            {
                if (hTicket == null)
                {
                    return false;
                }

                if (_authTicketValueField == null)
                {
                    return true;
                }

                var value = _authTicketValueField.GetValue(hTicket);
                if (value == null)
                {
                    return false;
                }

                if (value is uint handleValue)
                {
                    return handleValue != 0;
                }

                if (value is int intHandle)
                {
                    return intHandle != 0;
                }

                return true;
            }
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
