using System.Threading.Tasks;
using UnityEngine;

namespace JournaleSDK
{
    public class JournaleService : MonoBehaviour
    {
        public static JournaleService Instance { get; private set; }

        [Header("Assign SessionConfig")]
        public SessionConfig config;

        SecureClient   _client;
        JournaleMemory _memory;

        void Awake()
        {
            Instance = this;
            // DO NOT touch config-dependent things here.
        }

        void Start()
        {
            // By now Journale.Initialize(...) has assigned config.
            EnsureReady();
        }

        void EnsureReady()
        {
            if (_memory == null) _memory = new JournaleMemory();

            if (_client == null)
            {
                // Fallbacks in case something was misconfigured.
                if (config == null)
                {
                    // Try to reuse the SessionManager's config if it exists.
                    if (SessionManager.Instance && SessionManager.Instance.config != null)
                        config = SessionManager.Instance.config;
                }

                if (config == null)
                {
                    // Last resort: look in Resources (works if you created Resources/SessionConfig.asset)
                    config = Resources.Load<SessionConfig>("SessionConfig");
                }

                if (config == null)
                {
                    Debug.LogError("JournaleService: Missing SessionConfig. " +
                                   "Call Journale.Initialize(config) once, or create Resources/SessionConfig.asset.");
                    // Construct a dummy client to avoid NRE chain; calls will still fail with a clear error.
                    _client = new SecureClient(new SessionConfig());
                }
                else
                {
                    _client = new SecureClient(config);
                }
            }
        }

        /// <summary>
        /// Core send used by the Facade.
        /// </summary>
        public async Task<string> SendAsync(
            string localId,                 // REQUIRED: your own local identifier for this NPC
            string userMessage,
            string characterDescription = null,
            string characterId = null,
            string playerDescriptionOverride = null)
        {
            EnsureReady();
            
            await SessionManager.Instance.EnsureSessionAsync();
            
            var playerId = SessionManager.Instance.PlayerId ?? "local";
            // Build context BEFORE adding the current user message so it is not duplicated
            var contextForThisTurn = _memory.BuildContext(localId, playerId, config.maxHistoryLinesForContext);
            
            // Now record the user message for future turns
            _memory.Add(localId, playerId, "user", userMessage, config.maxHistoryLinesForContext);

            var req = new ChatRequest
            {
                message               = userMessage,
                context               = contextForThisTurn,
                characterDescription  = characterDescription,
                characterID           = characterId,
                playerDescription     = playerDescriptionOverride ?? config.defaultPlayerDescription
            };
            
            ChatResponse resp;
            try
            {
                resp = await _client.ChatAsync(req);
            }
            catch (System.Exception ex)
            {
                // Log error to console only - do NOT add to chat context
                Debug.LogError($"[JOURNALE] Chat request failed: {ex.Message}");
                // Return a user-friendly error that won't be added to context
                throw; // Re-throw so caller can handle
            }
            
            var reply = string.IsNullOrWhiteSpace(resp.reply) ? "(no reply)" : resp.reply.Trim();
            _memory.Add(localId, playerId, "npc", reply, config.maxHistoryLinesForContext);
            return reply;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
