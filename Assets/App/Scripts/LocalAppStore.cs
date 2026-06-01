using System;
using System.Collections.Generic;
using UnityEngine;

public static class LocalAppStore
{
    private const string SessionKey = "ardea.session";
    private const string GuestDataKey = "ardea.data.guest";
    private const string UserDataKeyPrefix = "ardea.data.";

    public static void SaveSession(AuthUser user)
    {
        if (user == null)
            return;

        PlayerPrefs.SetString(SessionKey, JsonUtility.ToJson(user.WithoutTokens()));
        PlayerPrefs.Save();
    }

    public static AuthUser LoadSession()
    {
        var json = PlayerPrefs.GetString(SessionKey, string.Empty);
        if (string.IsNullOrEmpty(json))
            return null;

        var user = JsonUtility.FromJson<AuthUser>(json);
        if (user == null)
            return null;

        user.IdToken = null;
        user.RefreshToken = null;
        user.IdTokenExpiresAtUtcTicks = 0;
        SaveSession(user);
        return user;
    }

    public static void ClearSession()
    {
        PlayerPrefs.DeleteKey(SessionKey);
        PlayerPrefs.Save();
    }

    public static AppDataSnapshot LoadData()
    {
        var json = PlayerPrefs.GetString(GetDataKey(), string.Empty);
        if (string.IsNullOrEmpty(json))
            return CreateDefaultData();

        var data = JsonUtility.FromJson<AppDataSnapshot>(json);
        if (data == null)
            return CreateDefaultData();

        Normalize(data);
        return data;
    }

    public static void SaveData(AppDataSnapshot data)
    {
        if (data == null)
            return;

        Normalize(data);
        PlayerPrefs.SetString(GetDataKey(), JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    public static ProjectRecord CreateProject(string name)
    {
        var data = LoadData();
        var project = new ProjectRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            UpdatedLabel = "Edited just now"
        };

        data.Projects.Insert(0, project);
        SaveData(data);
        return project;
    }

    public static void SaveProject(ProjectRecord project)
    {
        var data = LoadData();
        var index = data.Projects.FindIndex(item => item.Id == project.Id);
        if (index >= 0)
            data.Projects[index] = project;
        else
            data.Projects.Insert(0, project);

        SaveData(data);
    }

    private static AppDataSnapshot CreateDefaultData()
    {
        var data = new AppDataSnapshot();
        data.SavedFurnitureIds.Add("chair-01");
        data.SavedFurnitureIds.Add("closet-01");
        data.Projects.Add(new ProjectRecord { Id = "sample-living-room", Name = "Living Room Refresh", UpdatedLabel = "Edited today" });
        data.Projects.Add(new ProjectRecord { Id = "sample-bedroom", Name = "Bedroom Study Setup", UpdatedLabel = "Edited yesterday" });
        data.Projects.Add(new ProjectRecord { Id = "sample-kitchen", Name = "Kitchen Storage Plan", UpdatedLabel = "Edited May 25" });
        SaveData(data);
        return data;
    }

    private static string GetDataKey()
    {
        var user = AppState.CurrentUser;
        return user == null || string.IsNullOrEmpty(user.LocalId) ? GuestDataKey : UserDataKeyPrefix + user.LocalId;
    }

    private static void Normalize(AppDataSnapshot data)
    {
        if (data.SavedFurnitureIds == null)
            data.SavedFurnitureIds = new List<string>();

        if (data.Projects == null)
            data.Projects = new List<ProjectRecord>();

        foreach (var project in data.Projects)
        {
            if (project.PlacedFurniture == null)
                project.PlacedFurniture = new List<PlacedFurnitureRecord>();
        }
    }
}
