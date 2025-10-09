#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using JournaleClient;

namespace JournaleClient.Editor
{
    public class JournaleSettingsProvider : SettingsProvider
    {
        private SerializedObject _so;
        private SessionConfig _cfg;

        public JournaleSettingsProvider(string path, SettingsScope scope)
            : base(path, scope) {}

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
            => new JournaleSettingsProvider("Project/Journale SDK", SettingsScope.Project);

        public override void OnGUI(string searchContext)
        {
            _cfg = FindOrCreateConfig();
            if (_cfg == null)
            {
                if (GUILayout.Button("Create SessionConfig"))
                    _cfg = CreateConfig();
                return;
            }

            if (_so == null || _so.targetObject != _cfg)
                _so = new SerializedObject(_cfg);

            _so.Update();

            EditorGUILayout.PropertyField(_so.FindProperty("apiBaseUrl"));
            EditorGUILayout.PropertyField(_so.FindProperty("sessionCreatePath"));
            EditorGUILayout.PropertyField(_so.FindProperty("chatPath"));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_so.FindProperty("projectId"));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_so.FindProperty("platform"));
            EditorGUILayout.PropertyField(_so.FindProperty("deviceIdOverride"));
            EditorGUILayout.PropertyField(_so.FindProperty("allowGuestFallbackIfSteamMissing"));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_so.FindProperty("maxHistoryLinesForContext"));
            EditorGUILayout.PropertyField(_so.FindProperty("maxRetriesOn429"));
            EditorGUILayout.PropertyField(_so.FindProperty("baseBackoffSeconds"));
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_so.FindProperty("defaultPlayerDescription"));
            _so.ApplyModifiedProperties();

            if (GUILayout.Button("Ping Asset")) EditorGUIUtility.PingObject(_cfg);
        }

        private static SessionConfig FindOrCreateConfig()
        {
            var cfg = Resources.Load<SessionConfig>("SessionConfig");
            if (cfg) return cfg;

            string[] guids = AssetDatabase.FindAssets("t:SessionConfig");
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<SessionConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));

            return null;
        }

        private static SessionConfig CreateConfig()
        {
            const string dir = "Assets/Resources";
            const string path = dir + "/SessionConfig.asset";
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            var cfg = ScriptableObject.CreateInstance<SessionConfig>();
            AssetDatabase.CreateAsset(cfg, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(cfg);
            return cfg;
        }
    }
}
#endif
