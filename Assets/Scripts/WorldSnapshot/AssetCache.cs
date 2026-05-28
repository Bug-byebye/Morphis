using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Morphis.WorldSnapshot
{
    /// <summary>
    /// 用户资产（GLB）本地缓存 + 后端拉取。
    ///
    /// 命名约定：所有"用户生成 / 用户上传"的 GLB 通过 SHA256 标识，prefab_id 形如 "asset:&lt;sha256&gt;"。
    /// 落地路径：Application.persistentDataPath/AssetCache/&lt;sha256&gt;.glb
    ///
    /// 与旧资源体系的关系：
    /// - "primitive:XXX"                          → 由 Unity 原生 PrimitiveType 创建（不走这里）
    /// - "Placeables/XXX" / "glb:XXX"             → 走 Resources.Load（项目自带的打包资产）
    /// - "asset:&lt;sha256&gt;"                       → 走本类（先查本地缓存，未命中则 HTTP GET 后端）
    /// </summary>
    public static class AssetCache
    {
        public const string AssetPrefix = "asset:";

        private static string CacheDir
        {
            get
            {
                var dir = Path.Combine(Application.persistentDataPath, "AssetCache");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static bool IsAssetId(string prefabId)
        {
            return !string.IsNullOrEmpty(prefabId) && prefabId.StartsWith(AssetPrefix, StringComparison.OrdinalIgnoreCase);
        }

        public static string ExtractSha(string prefabId)
        {
            if (!IsAssetId(prefabId)) return null;
            return prefabId.Substring(AssetPrefix.Length).Trim();
        }

        public static string ToAssetPrefabId(string sha256)
        {
            return string.IsNullOrEmpty(sha256) ? null : AssetPrefix + sha256;
        }

        public static string GetCachedPath(string sha256)
        {
            if (string.IsNullOrEmpty(sha256)) return null;
            return Path.Combine(CacheDir, sha256 + ".glb");
        }

        public static bool ExistsLocally(string sha256)
        {
            var p = GetCachedPath(sha256);
            return !string.IsNullOrEmpty(p) && File.Exists(p);
        }

        /// <summary>将字节落到本地缓存（按 SHA256 命名），返回 sha256；写盘失败返回 null。</summary>
        public static string StoreBytes(byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            var sha = ComputeSha256(data);
            var path = GetCachedPath(sha);
            try
            {
                File.WriteAllBytes(path, data);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AssetCache] StoreBytes failed: {e.Message}");
                return null;
            }
            return sha;
        }

        public static byte[] LoadBytes(string sha256)
        {
            var path = GetCachedPath(sha256);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try { return File.ReadAllBytes(path); }
            catch (Exception e)
            {
                Debug.LogError($"[AssetCache] LoadBytes failed: {e.Message}");
                return null;
            }
        }

        public static string ComputeSha256(byte[] data)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(data);
            var sb = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }

        /// <summary>
        /// 拉取并缓存：若本地已存在直接成功；否则从后端 GET /assets/{sha}，写入缓存。
        /// </summary>
        public static IEnumerator EnsureCachedCoroutine(string sha256, Action<bool, string> onComplete)
        {
            if (string.IsNullOrEmpty(sha256))
            {
                onComplete?.Invoke(false, "empty sha");
                yield break;
            }

            if (ExistsLocally(sha256))
            {
                onComplete?.Invoke(true, null);
                yield break;
            }

            var url = $"{BaseUrl()}/assets/{sha256}";
            using var req = UnityWebRequest.Get(url);
            req.downloadHandler = new DownloadHandlerBuffer();
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success || req.responseCode >= 400)
            {
                var err = $"GET {url} failed: {(int)req.responseCode} {req.error}";
                Debug.LogWarning($"[AssetCache] {err}");
                onComplete?.Invoke(false, err);
                yield break;
            }

            var bytes = req.downloadHandler.data;
            if (bytes == null || bytes.Length == 0)
            {
                onComplete?.Invoke(false, "empty body");
                yield break;
            }

            try
            {
                File.WriteAllBytes(GetCachedPath(sha256), bytes);
            }
            catch (Exception e)
            {
                onComplete?.Invoke(false, e.Message);
                yield break;
            }
            onComplete?.Invoke(true, null);
        }

        /// <summary>
        /// 上传本地资产到后端。data 为完整字节；服务器会按 SHA256 入库（幂等）。
        /// </summary>
        public static IEnumerator UploadCoroutine(byte[] data, string filename, Action<bool, string, string> onComplete)
        {
            if (data == null || data.Length == 0)
            {
                onComplete?.Invoke(false, null, "empty data");
                yield break;
            }

            var url = $"{BaseUrl()}/assets/upload";
            var form = new WWWForm();
            form.AddBinaryData("file", data, filename ?? "asset.glb", "model/gltf-binary");

            using var req = UnityWebRequest.Post(url, form);
            // 复用 AppSession.Token（若有）
            var token = TryGetSessionToken();
            if (!string.IsNullOrEmpty(token))
                req.SetRequestHeader("Authorization", $"Bearer {token}");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success || req.responseCode >= 400)
            {
                var err = $"POST {url} failed: {(int)req.responseCode} {req.error} {req.downloadHandler?.text}";
                Debug.LogWarning($"[AssetCache] {err}");
                onComplete?.Invoke(false, null, err);
                yield break;
            }

            var text = req.downloadHandler.text;
            // 简单解析 {"asset_id":"<sha>",...}
            var sha = ExtractAssetIdFromJson(text);
            if (string.IsNullOrEmpty(sha))
            {
                onComplete?.Invoke(false, null, $"bad response: {text}");
                yield break;
            }

            // 把上传内容也写一份到本地缓存（按 sha 命名）以避免下次往返
            try { File.WriteAllBytes(GetCachedPath(sha), data); } catch { }

            onComplete?.Invoke(true, sha, null);
        }

        private static string ExtractAssetIdFromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            const string key = "\"asset_id\"";
            var i = json.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return null;
            var colon = json.IndexOf(':', i + key.Length);
            if (colon < 0) return null;
            var q1 = json.IndexOf('"', colon + 1);
            if (q1 < 0) return null;
            var q2 = json.IndexOf('"', q1 + 1);
            if (q2 < 0) return null;
            return json.Substring(q1 + 1, q2 - q1 - 1);
        }

        private static string BaseUrl()
        {
            try
            {
                return Morphis.Config.AppConfig.Instance.ApiBaseUrl;
            }
            catch
            {
                return "";
            }
        }

        private static string TryGetSessionToken()
        {
            try
            {
                var t = Type.GetType("Morphis.AppFlow.AppSession");
                if (t == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        t = asm.GetType("Morphis.AppFlow.AppSession");
                        if (t != null) break;
                    }
                }
                if (t == null) return null;
                var prop = t.GetProperty("Token", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                return prop?.GetValue(null) as string;
            }
            catch { return null; }
        }
    }
}
