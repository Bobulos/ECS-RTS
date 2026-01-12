using System.IO;
using UnityEngine;
using System.Collections.Generic;
using System;

public static class ReplayFileManager
{
    private static string ManifestPath => Path.Combine(Application.persistentDataPath, "replay_manifest.json");
    /// <summary>
    /// returns current count
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static void AddFile(string path)
    {
        ReplayManifest manifest = LoadManifest();

        // Convert array to list for easy addition
        List<string> replayList = manifest.replays != null
            ? new List<string>(manifest.replays)
            : new List<string>();

        // Avoid duplicate entries if necessary
        if (!replayList.Contains(path))
        {
            replayList.Add(path);
        }

        manifest.replays = replayList.ToArray();
        SaveManifest(manifest);
    }
    public static void ClearManifest()
    {
        var manifest = LoadManifest();
        foreach (var replay in manifest.replays)
        {
            FileDeleter.DeleteSaveFile(replay);
        }
        SaveManifest(new ReplayManifest { replays = new string[0] });
    }
    public static ReplayManifest LoadManifest()
    {
        //EditorUtility.RevealInFinder(Application.persistentDataPath);
        if (!File.Exists(ManifestPath))
        {
            return new ReplayManifest { replays = new string[0] };
        }

        try
        {
            string json = File.ReadAllText(ManifestPath);
            return JsonUtility.FromJson<ReplayManifest>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load replay manifest: {e.Message}");
            return new ReplayManifest { replays = new string[0]};
        }
    }

    private static void SaveManifest(ReplayManifest manifest)
    {
        try
        {
            string json = JsonUtility.ToJson(manifest, true); // 'true' for pretty print
            File.WriteAllText(ManifestPath, json);
            Debug.Log($"Manifest updated at: {ManifestPath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save replay manifest: {e.Message}");
        }
    }
}
public static class FileDeleter
{
    public static void DeleteSaveFile(string fileName)
    {
        string path = Path.Combine(Application.persistentDataPath, fileName);

        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log($"File deleted successfully at: {path}");
        }
        else
        {
            Debug.LogWarning($"File not found at: {path}");
        }
    }
}

[System.Serializable]
public class ReplayManifest
{
    public string[] replays;
}