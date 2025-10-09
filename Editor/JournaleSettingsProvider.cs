#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using JournaleClient; // for SessionConfig

namespace JournaleClient.Editor
{
    public class JournaleSettingsProvider : SettingsProvider
    {
        private SerializedObject _configSO;

        public JournaleSettingsProvider(string path, SettingsScope scope)
            : base(path, scope)
        {
        }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new JournaleSettingsProvider("Project/Journale SDK", SettingsScope.Project);
        }

        public override void OnGUI(string searchContext)
        {
            // Try to find or create a SessionConfig asset
            var config = FindOrCreateConfig();

            if (config == null)
            {
                EditorGUILayout.HelpBox("No SessionConfig found. Click below to create one.", MessageType.Warning);
                if (GUILayout.Button("Create SessionConfig"))
                {
                    config = CreateConfig();
                }
                return;
            }

            if (_configSO == null)
                _configSO = new SerializedObject(config);

            _configSO.Update();

            EditorGUILayout.LabelField("Server", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_configSO.FindProperty("apiBaseUrl"));
            EditorGUILayout.PropertyField(_configSO.FindProperty("sessionCreatePath"));
            EditorGUILayout.PropertyField(_configSO.FindProperty("chatPath"));
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Project", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_configSO.FindProperty("projectId"));
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Auth", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_configSO.FindProperty("platform"));
            EditorGUILayout.PropertyField(_configSO.FindProperty("deviceIdOverride"));
            EditorGUILayout.PropertyField(_configSO.FindProperty("allowGuestFallbackIfSteamMissing"));
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Client Behavior", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_configSO.FindProperty("maxHistoryLinesForContext"));
            EditorGUILayout.PropertyField(_configSO.FindProperty("maxRetriesOn429"));
            EditorGUILayout.PropertyField(_configSO.FindProperty("baseBackoffSeconds"));
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Player", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_configSO.FindProperty("defaultPlayerDescription"));
            EditorGUILayout.Space();

            _configSO.ApplyModifiedProperties();

            if (GUILayout.Button("Ping Asset"))
                EditorGUIUtility.PingObject(config);
        }

        private SessionConfig FindOrCreateConfig()
        {
            var config = Resources.Load<SessionConfig>("SessionConfig");
            if (config == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:SessionConfig");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    config = AssetDatabase.LoadAssetAtPath<SessionConfig>(path);
                }
            }
            return config;
        }

        private SessionConfig CreateConfig()
        {
            const string dir = "Assets/Resources";
            const string path = dir + "/SessionConfig.asset";

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            var config = ScriptableObject.CreateInstance<SessionConfig>();
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(config);
            return config;
        }
    }
}
#endif
