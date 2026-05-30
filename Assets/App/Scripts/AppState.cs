using System;
using System.Collections.Generic;
using UnityEngine;

public static class AppState
{
    public static AuthUser CurrentUser { get; private set; }
    public static string ActiveProjectId { get; set; }

    public static bool IsSignedIn => CurrentUser != null && !string.IsNullOrEmpty(CurrentUser.LocalId);

    public static void SetUser(AuthUser user)
    {
        CurrentUser = user;
        LocalAppStore.SaveSession(user);
    }

    public static void RestoreSession()
    {
        CurrentUser = LocalAppStore.LoadSession();
    }

    public static void SignOut()
    {
        CurrentUser = null;
        ActiveProjectId = null;
        LocalAppStore.ClearSession();
    }
}

[Serializable]
public sealed class AuthUser
{
    public string LocalId;
    public string Email;
    public string DisplayName;
    public string IdToken;
    public string RefreshToken;
}

[Serializable]
public sealed class FurnitureRecord
{
    public string Id;
    public string Title;
    public string Category;

    public FurnitureRecord(string id, string title, string category)
    {
        Id = id;
        Title = title;
        Category = category;
    }
}

[Serializable]
public sealed class ProjectRecord
{
    public string Id;
    public string Name;
    public string UpdatedLabel;
    public List<PlacedFurnitureRecord> PlacedFurniture = new List<PlacedFurnitureRecord>();
}

[Serializable]
public sealed class PlacedFurnitureRecord
{
    public string FurnitureId;
    public string Title;
    public string Category;
    public SerializableVector3 Position;
    public SerializableVector3 Rotation;
    public SerializableVector3 Scale;
}

[Serializable]
public struct SerializableVector3
{
    public float X;
    public float Y;
    public float Z;

    public SerializableVector3(Vector3 value)
    {
        X = value.x;
        Y = value.y;
        Z = value.z;
    }

    public Vector3 ToVector3()
    {
        return new Vector3(X, Y, Z);
    }
}

[Serializable]
public sealed class AppDataSnapshot
{
    public List<string> SavedFurnitureIds = new List<string>();
    public List<ProjectRecord> Projects = new List<ProjectRecord>();
}
