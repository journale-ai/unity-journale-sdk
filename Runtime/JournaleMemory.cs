// Assets/JournaleClient/Runtime/JournaleMemory.cs
using System.Collections.Generic;
using System.Text;

namespace JournaleClient
{
    public class JournaleMemory
    {
        // key = localId + "::" + playerId
        private readonly Dictionary<string, List<(string role,string content)>> _map = new();

        public List<(string role,string content)> Get(string localId, string playerId)
        {
            var key = $"{localId}::{playerId}";
            if (!_map.TryGetValue(key, out var list)) _map[key] = list = new();
            return list;
        }

        public void Add(string localId, string playerId, string role, string content, int maxKeep)
        {
            var list = Get(localId, playerId);
            list.Add((role, content));
            if (maxKeep > 0 && list.Count > maxKeep) list.RemoveRange(0, list.Count - maxKeep);
        }

        public string BuildContext(string localId, string playerId, int lastN)
        {
            var list = Get(localId, playerId);
            int start = System.Math.Max(0, list.Count - lastN);
            var sb = new StringBuilder();
            for (int i = start; i < list.Count; i++)
            {
                var (role, content) = list[i];
                sb.Append(role == "user" ? "Player: " : "NPC: ").AppendLine(content);
            }
            return sb.ToString();
        }
    }
}
