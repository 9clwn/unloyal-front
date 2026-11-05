using System.Text.Json;
using System.Text.Json.Nodes;
using UnloyalFront.Models;
using UnloyalFront.Utility;

namespace UnloyalFront.Integrations;

/// <summary>Writes ClientSettings\ClientAppSettings.json (Roblox's FastFlag override file).</summary>
public static class FastFlagManager
{
    public static bool TryValidate(string json, out string error)
    {
        error = "";
        try
        {
            if (JsonNode.Parse(json) is not JsonObject)
            {
                error = "Flags must be a JSON object like { \"FFlagName\": true }.";
                return false;
            }
            return true;
        }
        catch (JsonException ex) { error = ex.Message; return false; }
    }

    public static void Apply(string versionDir, AppSettings s)
    {
        var dir = Path.Combine(versionDir, "ClientSettings");
        var file = Path.Combine(dir, "ClientAppSettings.json");
        try
        {
            var node = JsonNode.Parse(string.IsNullOrWhiteSpace(s.FastFlagsJson) ? "{}" : s.FastFlagsJson) as JsonObject;
            if (node == null || node.Count == 0)
            {
                if (File.Exists(file)) File.Delete(file);
                return;
            }
            Directory.CreateDirectory(dir);
            File.WriteAllText(file, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { Log.Write("Applying FastFlags", ex); }
    }
}
