using System;
using System.Collections.Generic;
using System.IO;
using Codely.Newtonsoft.Json.Linq;
using UnityEditor;
using UnityTcp.Editor.Helpers;

namespace UnityTcp.Editor.Tools
{
    /// <summary>
    /// Returns the real 32-character guid that scene, prefab, and asset YAML
    /// use to reference an asset.
    ///
    /// Tuanjie stores an encrypted guid in .meta files. Decryption deliberately
    /// stays inside the editor: AssetDatabase.AssetPathToGUID is the only
    /// authoritative source of the YAML guid.
    /// </summary>
    public static class ManageGuid
    {
        private const int MaxMetaHeaderLines = 64;

        private static readonly Dictionary<string, Func<JObject, object>> ActionHandlers =
            new Dictionary<string, Func<JObject, object>>
            {
                { "path_to_guid", PathToGuid },
            };

        public static object HandleCommand(JObject @params)
            => ActionRouter.Route(@params, ActionHandlers);

        private static object PathToGuid(JObject @params)
        {
            string assetPath = NormalizeAssetPath(@params["asset_path"]?.ToString());
            if (string.IsNullOrEmpty(assetPath))
            {
                return Response.Error("asset_path parameter is required for path_to_guid.");
            }

            if (!IsSafeAssetPath(assetPath))
            {
                return Response.Error($"asset_path '{assetPath}' must be under Assets.");
            }

            // AssetPathToGUID can still answer for assets deleted since the last refresh.
            if (!File.Exists(assetPath) && !Directory.Exists(assetPath))
            {
                return Response.Error($"Asset '{assetPath}' does not exist.");
            }

            string yamlGuid = AssetDatabase.AssetPathToGUID(assetPath);
            if (!IsHexGuid(yamlGuid))
            {
                return Response.Error(
                    $"AssetDatabase has no guid for '{assetPath}'. Is the asset imported?");
            }

            yamlGuid = yamlGuid.ToLowerInvariant();

            string expectedGuid = @params["guid"]?.ToString()?.Trim();
            bool verify = !string.IsNullOrEmpty(expectedGuid);
            if (verify && !TryVerifyGuid(assetPath, yamlGuid, expectedGuid, out string verifyError))
            {
                return Response.Error(verifyError);
            }

            return Response.Success(
                $"YAML guid for '{assetPath}' is '{yamlGuid}'.",
                new Dictionary<string, object>
                {
                    { "asset_path", assetPath },
                    { "yaml_guid", yamlGuid },
                    { "guid_verified", verify },
                });
        }

        /// <summary>
        /// A 32-hex guid is compared with the YAML guid; anything else is treated
        /// as the encrypted value and compared verbatim with the asset's .meta.
        /// </summary>
        private static bool TryVerifyGuid(
            string assetPath,
            string yamlGuid,
            string expectedGuid,
            out string error)
        {
            error = null;
            if (IsHexGuid(expectedGuid))
            {
                if (string.Equals(expectedGuid, yamlGuid, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                error = $"guid '{expectedGuid}' does not match the YAML guid of '{assetPath}'.";
                return false;
            }

            if (!TryReadMetaGuid(assetPath + ".meta", out string metaGuid))
            {
                error = $"Could not read guid from '{assetPath}.meta' to verify the supplied guid.";
                return false;
            }

            if (string.Equals(expectedGuid, metaGuid, StringComparison.Ordinal))
            {
                return true;
            }

            error = $"guid '{expectedGuid}' does not match the guid in '{assetPath}.meta'.";
            return false;
        }

        internal static bool TryReadMetaGuid(string metaPath, out string guid)
        {
            guid = null;
            try
            {
                using (var reader = new StreamReader(metaPath))
                {
                    for (int lineNumber = 0;
                        lineNumber < MaxMetaHeaderLines && !reader.EndOfStream;
                        lineNumber++)
                    {
                        string trimmed = reader.ReadLine()?.TrimStart();
                        if (trimmed == null)
                        {
                            break;
                        }

                        if (trimmed.StartsWith("guid:", StringComparison.Ordinal))
                        {
                            guid = trimmed.Substring("guid:".Length).Trim();
                            return !string.IsNullOrEmpty(guid);
                        }
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return false;
        }

        private static string NormalizeAssetPath(string path)
            => string.IsNullOrWhiteSpace(path)
                ? null
                : path.Trim().Replace('\\', '/');

        internal static bool IsSafeAssetPath(string path)
        {
            if (!(string.Equals(path, "Assets", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            foreach (string segment in path.Split('/'))
            {
                if (segment == "..")
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool IsHexGuid(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 32)
            {
                return false;
            }

            foreach (char c in value)
            {
                if (!((c >= '0' && c <= '9')
                    || (c >= 'a' && c <= 'f')
                    || (c >= 'A' && c <= 'F')))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
