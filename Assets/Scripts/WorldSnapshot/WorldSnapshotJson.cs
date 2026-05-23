using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Morphis.WorldSnapshot
{
    /// <summary>
    /// 世界快照 JSON 序列化工具：
    /// 之前使用 Unity 的 JsonUtility，对 WorldObjectData 中 [SerializeField] private 的 pos_x/...
    /// 在反序列化 List 元素时会丢字段（不同 Unity 版本/平台表现不一致），导致 objects 解析为空。
    /// 这里改用 Newtonsoft.Json，并显式以"扁平字段名"读写，确保前后端 JSON schema 兼容：
    /// { world_id, version, objects:[{ object_id, prefab_id, comment, pos_x, pos_y, pos_z, rot_x, rot_y, rot_z, rot_w, scale_x, scale_y, scale_z }, ...] }
    /// </summary>
    public static class WorldSnapshotJson
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Include,
        };

        public static string Serialize(WorldSnapshot snapshot)
        {
            if (snapshot == null) return "null";

            var root = new JObject
            {
                ["world_id"] = snapshot.world_id ?? string.Empty,
                ["version"] = snapshot.version,
            };

            var arr = new JArray();
            if (snapshot.objects != null)
            {
                foreach (var obj in snapshot.objects)
                {
                    if (obj == null) continue;
                    arr.Add(SerializeObject(obj));
                }
            }
            root["objects"] = arr;
            return root.ToString(Formatting.None);
        }

        public static WorldSnapshot Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WorldSnapshotJson] Parse error: {e.Message}");
                return null;
            }

            var snap = new WorldSnapshot
            {
                world_id = (string)root["world_id"] ?? string.Empty,
                version = (int?)root["version"] ?? 1,
                objects = new List<WorldObjectData>(),
            };

            var arr = root["objects"] as JArray;
            if (arr != null)
            {
                foreach (var token in arr)
                {
                    if (!(token is JObject jo)) continue;
                    var obj = DeserializeObject(jo);
                    if (obj != null) snap.objects.Add(obj);
                }
            }

            return snap;
        }

        private static JObject SerializeObject(WorldObjectData obj)
        {
            var p = obj.position;
            var r = obj.rotation;
            var s = obj.scale;
            return new JObject
            {
                ["object_id"] = obj.object_id ?? Guid.NewGuid().ToString(),
                ["prefab_id"] = obj.prefab_id ?? string.Empty,
                ["comment"] = obj.comment ?? string.Empty,
                ["pos_x"] = p.x,
                ["pos_y"] = p.y,
                ["pos_z"] = p.z,
                ["rot_x"] = r.x,
                ["rot_y"] = r.y,
                ["rot_z"] = r.z,
                ["rot_w"] = r.w,
                ["scale_x"] = s.x,
                ["scale_y"] = s.y,
                ["scale_z"] = s.z,
            };
        }

        private static WorldObjectData DeserializeObject(JObject jo)
        {
            var obj = new WorldObjectData
            {
                object_id = (string)jo["object_id"] ?? Guid.NewGuid().ToString(),
                prefab_id = (string)jo["prefab_id"] ?? string.Empty,
                comment = (string)jo["comment"] ?? string.Empty,
            };

            obj.position = new Vector3(
                F(jo, "pos_x"),
                F(jo, "pos_y"),
                F(jo, "pos_z"));

            // 缺省 rot_w=1 避免出现全 0 四元数
            obj.rotation = new Quaternion(
                F(jo, "rot_x"),
                F(jo, "rot_y"),
                F(jo, "rot_z"),
                F(jo, "rot_w", 1f));

            // 缺省 scale=1
            obj.scale = new Vector3(
                F(jo, "scale_x", 1f),
                F(jo, "scale_y", 1f),
                F(jo, "scale_z", 1f));

            return obj;
        }

        private static float F(JObject jo, string key, float fallback = 0f)
        {
            var t = jo[key];
            if (t == null || t.Type == JTokenType.Null) return fallback;
            try { return (float)t; }
            catch { return fallback; }
        }
    }
}
