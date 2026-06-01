using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class FurnitureCatalogValidator
{
    [MenuItem("ARdea/Validate Furniture Catalog")]
    public static void ValidateFurnitureCatalog()
    {
        var prefabDirectory = Path.Combine(Application.dataPath, "App", "Resources", "Furniture");
        if (!Directory.Exists(prefabDirectory))
        {
            throw new DirectoryNotFoundException("Furniture prefab directory not found: " + prefabDirectory);
        }

        var prefabIds = new HashSet<string>();
        foreach (var path in Directory.GetFiles(prefabDirectory, "*.prefab", SearchOption.TopDirectoryOnly))
            prefabIds.Add(Path.GetFileNameWithoutExtension(path));

        var catalogIds = new HashSet<string>();
        var missingPrefabs = new List<string>();
        foreach (var item in FurnitureCatalog.Items)
        {
            if (!catalogIds.Add(item.Id))
                throw new System.InvalidOperationException("Duplicate furniture catalog id: " + item.Id);

            if (!prefabIds.Contains(item.Id))
                missingPrefabs.Add(item.Id);
        }

        var orphanedPrefabs = new List<string>();
        foreach (var prefabId in prefabIds)
        {
            if (!catalogIds.Contains(prefabId))
                orphanedPrefabs.Add(prefabId);
        }

        if (missingPrefabs.Count > 0 || orphanedPrefabs.Count > 0)
        {
            throw new System.InvalidOperationException(
                "Furniture catalog validation failed. Missing prefabs: " +
                string.Join(", ", missingPrefabs) +
                ". Orphaned prefabs: " +
                string.Join(", ", orphanedPrefabs));
        }

        Debug.Log("[ARdea] Furniture catalog validation passed for " + catalogIds.Count + " items.");
    }
}
