using UnityEngine;

public static class FurniturePrefabResolver
{
    public static GameObject Instantiate(FurnitureRecord furniture, Vector3 position, Quaternion rotation)
    {
        var prefab = Resources.Load<GameObject>("Furniture/" + furniture.Id);
        GameObject instance;

        if (prefab != null)
        {
            instance = Object.Instantiate(prefab, position, rotation);
        }
        else
        {
            instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = GuessScale(furniture.Category);
            var renderer = instance.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = ColorForCategory(furniture.Category);
        }

        instance.name = furniture.Title;
        return instance;
    }

    public static Vector3 GuessScale(string category)
    {
        switch (category)
        {
            case "Sofas": return new Vector3(0.9f, 0.35f, 0.35f);
            case "Tables": return new Vector3(0.55f, 0.25f, 0.55f);
            case "Beds": return new Vector3(1.0f, 0.3f, 0.7f);
            case "Closets": return new Vector3(0.55f, 1.0f, 0.25f);
            default: return new Vector3(0.35f, 0.45f, 0.35f);
        }
    }

    public static Color ColorForCategory(string category)
    {
        switch (category)
        {
            case "Sofas": return new Color(0.18f, 0.18f, 0.2f);
            case "Tables": return new Color(0.45f, 0.42f, 0.38f);
            case "Beds": return new Color(0.55f, 0.58f, 0.62f);
            case "Closets": return new Color(0.32f, 0.32f, 0.32f);
            default: return new Color(0.12f, 0.74f, 0.72f);
        }
    }
}
