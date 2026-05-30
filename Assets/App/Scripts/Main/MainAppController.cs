using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public sealed class MainAppController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private string arDesignerSceneName = "ARDesigner";

    private readonly HashSet<string> _savedFurniture = new HashSet<string>();
    private AppDataSnapshot _data;
    private FirebaseRestService _firebase;

    private VisualElement _homePage;
    private VisualElement _browsePage;
    private VisualElement _profilePage;
    private VisualElement _settingsPage;
    private VisualElement _savedProfilePage;
    private VisualElement _projectsProfilePage;
    private Button _navHome;
    private Button _navBrowse;
    private Button _navProfile;
    private VisualElement _bottomNav;
    private ScrollView _projectList;
    private ScrollView _savedProfileList;
    private ScrollView _projectsProfileList;
    private ScrollView _categoryRow;
    private ScrollView _furnitureList;
    private TextField _searchField;
    private Label _projectCountLabel;
    private Label _savedCountLabel;
    private Label _profileProjectCountLabel;
    private Label _profileSavedCountLabel;
    private Label _profileNameLabel;
    private Label _profileEmailLabel;
    private Label _browseTitleLabel;
    private Label _browseSubtitleLabel;
    private VisualElement _confirmDialog;
    private Label _confirmMessage;
    private VisualElement _createProjectDialog;
    private TextField _projectNameField;
    private Label _projectNameError;
    private string _activeCategory = "All";
    private bool _showSavedOnly;
    private string _pendingDeleteProjectId;

    private void OnEnable()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        AppState.RestoreSession();
        _data = LocalAppStore.LoadData();
        _savedFurniture.Clear();
        foreach (var savedId in _data.SavedFurnitureIds)
            _savedFurniture.Add(savedId);

        _firebase = GetComponent<FirebaseRestService>();
        if (_firebase == null)
            _firebase = gameObject.AddComponent<FirebaseRestService>();

        var root = uiDocument.rootVisualElement;

        _homePage = root.Q<VisualElement>("home-page");
        _browsePage = root.Q<VisualElement>("browse-page");
        _profilePage = root.Q<VisualElement>("profile-page");
        _settingsPage = root.Q<VisualElement>("settings-page");
        _savedProfilePage = root.Q<VisualElement>("saved-profile-page");
        _projectsProfilePage = root.Q<VisualElement>("projects-profile-page");
        _navHome = root.Q<Button>("nav-home");
        _navBrowse = root.Q<Button>("nav-browse");
        _navProfile = root.Q<Button>("nav-profile");
        _bottomNav = root.Q<VisualElement>("main-bottom-nav");
        _projectList = root.Q<ScrollView>("project-list");
        _savedProfileList = root.Q<ScrollView>("saved-profile-list");
        _projectsProfileList = root.Q<ScrollView>("projects-profile-list");
        _categoryRow = root.Q<ScrollView>("category-row");
        _furnitureList = root.Q<ScrollView>("furniture-list");
        ConfigureMobileScrollView(_projectList, false, true);
        ConfigureMobileScrollView(_savedProfileList, false, true);
        ConfigureMobileScrollView(_projectsProfileList, false, true);
        ConfigureMobileScrollView(_categoryRow, true, false);
        ConfigureMobileScrollView(_furnitureList, false, true);
        _searchField = root.Q<TextField>("search-field");
        _projectCountLabel = root.Q<Label>("project-count-label");
        _savedCountLabel = root.Q<Label>("saved-count-label");
        _profileProjectCountLabel = root.Q<Label>("profile-project-count-label");
        _profileSavedCountLabel = root.Q<Label>("profile-saved-count-label");
        _profileNameLabel = root.Q<Label>("profile-name-label");
        _profileEmailLabel = root.Q<Label>("profile-email-label");
        _browseTitleLabel = root.Q<Label>("browse-title-label");
        _browseSubtitleLabel = root.Q<Label>("browse-subtitle-label");
        _confirmDialog = root.Q<VisualElement>("confirm-dialog");
        _confirmMessage = root.Q<Label>("confirm-message");
        _createProjectDialog = root.Q<VisualElement>("create-project-dialog");
        _projectNameField = root.Q<TextField>("project-name-field");
        _projectNameError = root.Q<Label>("project-name-error");

        root.Q<Button>("add-project-btn").clicked += ShowCreateProjectDialog;
        root.Q<Button>("settings-btn").clicked += ShowSettings;
        root.Q<Button>("settings-back-btn").clicked += ShowProfile;
        root.Q<Button>("saved-profile-back-btn").clicked += ShowProfile;
        root.Q<Button>("projects-profile-back-btn").clicked += ShowProfile;
        root.Q<Button>("signout-btn").clicked += SignOut;
        root.Q<Button>("cancel-delete-btn").clicked += HideDeleteConfirmation;
        root.Q<Button>("confirm-delete-btn").clicked += ConfirmDeleteProject;
        root.Q<Button>("cancel-create-project-btn").clicked += HideCreateProjectDialog;
        root.Q<Button>("confirm-create-project-btn").clicked += CreateNamedProject;
        RegisterClick(root.Q<VisualElement>("profile-saved-btn"), ShowSavedProfilePage);
        RegisterClick(root.Q<VisualElement>("profile-projects-btn"), ShowProjectsProfilePage);
        RegisterClick(root.Q<VisualElement>("profile-settings-btn"), ShowSettings);
        RegisterClick(root.Q<VisualElement>("profile-help-btn"), ShowHelp);

        _navHome.clicked += ShowHome;
        _navBrowse.clicked += ShowBrowse;
        _navProfile.clicked += ShowProfile;

        if (_searchField != null)
            _searchField.RegisterValueChangedCallback(_ => RenderFurniture());

        RenderProjects();
        RenderProfileProjects();
        RenderProfileSavedFurniture();
        RenderCategories();
        RenderFurniture();
        UpdateCounts();
        UpdateProfile();
        ShowHome();
    }

    private void ShowHome()
    {
        _showSavedOnly = false;
        ShowPage(_homePage);
        SetActiveNav(_navHome);
    }

    private void ShowBrowse()
    {
        _showSavedOnly = false;
        UpdateBrowseHeader();
        ShowPage(_browsePage);
        SetActiveNav(_navBrowse);
        RenderFurniture();
    }

    private void ShowSavedFurniture()
    {
        _showSavedOnly = true;
        _activeCategory = "All";
        UpdateBrowseHeader();
        RenderCategories();
        RenderFurniture();
        ShowPage(_browsePage);
        SetActiveNav(_navBrowse);
    }

    private void ShowProfile()
    {
        _showSavedOnly = false;
        ShowPage(_profilePage);
        SetActiveNav(_navProfile);
    }

    private void ShowSavedProfilePage()
    {
        _showSavedOnly = false;
        RenderProfileSavedFurniture();
        ShowPage(_savedProfilePage);
        SetActiveNav(null);
    }

    private void ShowProjectsProfilePage()
    {
        _showSavedOnly = false;
        RenderProfileProjects();
        ShowPage(_projectsProfilePage);
        SetActiveNav(null);
    }

    private void ShowSettings()
    {
        _showSavedOnly = false;
        ShowPage(_settingsPage);
        SetActiveNav(null);
    }

    private void ShowHelp()
    {
        ShowSettings();
    }

    private void ShowPage(VisualElement page)
    {
        SetPageVisible(_homePage, page == _homePage);
        SetPageVisible(_browsePage, page == _browsePage);
        SetPageVisible(_profilePage, page == _profilePage);
        SetPageVisible(_settingsPage, page == _settingsPage);
        SetPageVisible(_savedProfilePage, page == _savedProfilePage);
        SetPageVisible(_projectsProfilePage, page == _projectsProfilePage);

        if (_bottomNav != null)
            _bottomNav.EnableInClassList("hidden", page == _settingsPage || page == _savedProfilePage || page == _projectsProfilePage);
    }

    private static void SetPageVisible(VisualElement page, bool visible)
    {
        if (page == null)
            return;

        page.EnableInClassList("hidden", !visible);
        page.EnableInClassList("page-active", visible);
    }

    private void SetActiveNav(Button active)
    {
        SetNavActive(_navHome, active == _navHome);
        SetNavActive(_navBrowse, active == _navBrowse);
        SetNavActive(_navProfile, active == _navProfile);
    }

    private static void SetNavActive(Button button, bool active)
    {
        if (button != null)
            button.EnableInClassList("nav-active", active);
    }

    private void RenderProjects()
    {
        _projectList.Clear();
        foreach (var project in _data.Projects)
            _projectList.Add(CreateProjectCard(project, false));
    }

    private void RenderProfileProjects()
    {
        if (_projectsProfileList == null)
            return;

        _projectsProfileList.Clear();
        if (_data.Projects.Count == 0)
        {
            _projectsProfileList.Add(CreateEmptyState("No projects yet. Create your first room from Home."));
            return;
        }

        foreach (var project in _data.Projects)
            _projectsProfileList.Add(CreateProjectCard(project, true));
    }

    private void RenderProfileSavedFurniture()
    {
        if (_savedProfileList == null)
            return;

        _savedProfileList.Clear();
        if (_savedFurniture.Count == 0)
        {
            _savedProfileList.Add(CreateEmptyState("No saved furniture yet. Save items from Browse first."));
            return;
        }

        foreach (var savedId in _data.SavedFurnitureIds)
            _savedProfileList.Add(CreateSavedFurnitureListItem(FurnitureCatalog.Find(savedId)));
    }

    private VisualElement CreateProjectCard(ProjectRecord project, bool showDelete)
    {
        var card = new VisualElement();
        card.AddToClassList("project-card");
        card.EnableInClassList("project-card-with-delete", showDelete);

        var thumb = new VisualElement();
        thumb.AddToClassList("project-thumb");
        var mark = new VisualElement();
        mark.AddToClassList("project-mark");
        thumb.Add(mark);

        var textBlock = new VisualElement();
        textBlock.AddToClassList("project-copy");
        var titleLabel = new Label(project.Name);
        titleLabel.AddToClassList("card-title");
        var count = project.PlacedFurniture == null ? 0 : project.PlacedFurniture.Count;
        var subtitleLabel = new Label($"{count} furniture items placed");
        subtitleLabel.AddToClassList("card-subtitle");
        var dateLabel = new Label(project.UpdatedLabel);
        dateLabel.AddToClassList("card-date");
        textBlock.Add(titleLabel);
        textBlock.Add(subtitleLabel);
        textBlock.Add(dateLabel);

        var actions = new VisualElement();
        actions.AddToClassList("project-actions");

        var openButton = new Button(() => OpenProject(project.Id)) { text = "Open" };
        openButton.AddToClassList("card-action");
        actions.Add(openButton);

        if (showDelete)
        {
            var deleteButton = new Button(() => ShowDeleteConfirmation(project)) { text = "Delete" };
            deleteButton.AddToClassList("card-delete-action");
            actions.Add(deleteButton);
        }

        card.Add(thumb);
        card.Add(textBlock);
        card.Add(actions);
        return card;
    }

    private void RenderCategories()
    {
        _categoryRow.Clear();

        foreach (var category in FurnitureCatalog.Categories)
        {
            var captured = category;
            var chip = new Button(() =>
            {
                _activeCategory = captured;
                RenderCategories();
                RenderFurniture();
            }) { text = captured };

            chip.AddToClassList("category-chip");
            chip.EnableInClassList("category-chip-active", captured == _activeCategory);
            _categoryRow.Add(chip);
        }
    }

    private void RenderFurniture()
    {
        _furnitureList.Clear();

        var query = _searchField != null ? _searchField.value : string.Empty;
        VisualElement row = null;
        var index = 0;

        foreach (var item in FurnitureCatalog.Items)
        {
            if (_activeCategory != "All" && item.Category != _activeCategory)
                continue;

            if (_showSavedOnly && !_savedFurniture.Contains(item.Id))
                continue;

            if (!string.IsNullOrEmpty(query) &&
                item.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 &&
                item.Category.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            if (index % 2 == 0)
            {
                row = new VisualElement();
                row.AddToClassList("furniture-row");
                _furnitureList.Add(row);
            }

            row.Add(CreateFurnitureCard(item));
            index++;
        }

        if (index == 0)
            _furnitureList.Add(CreateEmptyState(_showSavedOnly ? "No saved furniture yet. Save items from Browse first." : "No furniture found."));
    }

    private VisualElement CreateFurnitureCard(FurnitureRecord item)
    {
        var card = new VisualElement();
        card.AddToClassList("furniture-card");

        var thumb = new VisualElement();
        thumb.AddToClassList("furniture-thumb");
        var initial = new Label(item.Category.Substring(0, 1));
        initial.AddToClassList("furniture-initial");
        thumb.Add(initial);

        var info = new VisualElement();
        info.AddToClassList("furniture-info");
        var title = new Label(item.Title);
        title.AddToClassList("card-title");
        var subtitle = new Label(item.Category);
        subtitle.AddToClassList("card-subtitle");
        info.Add(title);
        info.Add(subtitle);

        var saveButton = new Button(() => ToggleSavedFurniture(item));
        saveButton.AddToClassList("save-button");
        SetSaveButtonState(saveButton, item);

        card.Add(thumb);
        card.Add(info);
        card.Add(saveButton);
        return card;
    }

    private VisualElement CreateSavedFurnitureListItem(FurnitureRecord item)
    {
        var row = new VisualElement();
        row.AddToClassList("profile-detail-furniture-item");

        var thumb = new VisualElement();
        thumb.AddToClassList("profile-detail-thumb");
        var initial = new Label(item.Category.Substring(0, 1));
        initial.AddToClassList("profile-detail-initial");
        thumb.Add(initial);

        var copy = new VisualElement();
        copy.AddToClassList("profile-detail-copy");
        var title = new Label(item.Title);
        title.AddToClassList("card-title");
        var category = new Label(item.Category);
        category.AddToClassList("card-subtitle");
        copy.Add(title);
        copy.Add(category);

        var removeButton = new Button(() => ToggleSavedFurniture(item)) { text = "Remove" };
        removeButton.AddToClassList("profile-detail-action");

        row.Add(thumb);
        row.Add(copy);
        row.Add(removeButton);
        return row;
    }

    private void ToggleSavedFurniture(FurnitureRecord item)
    {
        if (_savedFurniture.Contains(item.Id))
            _savedFurniture.Remove(item.Id);
        else
            _savedFurniture.Add(item.Id);

        _data.SavedFurnitureIds.Clear();
        _data.SavedFurnitureIds.AddRange(_savedFurniture);
        LocalAppStore.SaveData(_data);
        StartCoroutine(_firebase.SyncAppData(AppState.CurrentUser, _data));
        RenderFurniture();
        RenderProfileSavedFurniture();
        UpdateCounts();
    }

    private void SetSaveButtonState(Button button, FurnitureRecord item)
    {
        var saved = _savedFurniture.Contains(item.Id);
        button.text = saved ? "Saved" : "Save";
        button.EnableInClassList("save-button-saved", saved);
    }

    private void UpdateCounts()
    {
        SetLabel(_projectCountLabel, _data.Projects.Count.ToString());
        SetLabel(_savedCountLabel, _savedFurniture.Count.ToString());
        SetLabel(_profileProjectCountLabel, _data.Projects.Count.ToString());
        SetLabel(_profileSavedCountLabel, _savedFurniture.Count.ToString());
    }

    private void UpdateProfile()
    {
        var user = AppState.CurrentUser;
        if (user == null)
            return;

        SetLabel(_profileNameLabel, string.IsNullOrEmpty(user.DisplayName) ? "ARdea User" : user.DisplayName);
        SetLabel(_profileEmailLabel, string.IsNullOrEmpty(user.Email) ? "name@example.com" : user.Email);
    }

    private static void SetLabel(Label label, string text)
    {
        if (label != null)
            label.text = text;
    }

    private void UpdateBrowseHeader()
    {
        SetLabel(_browseTitleLabel, _showSavedOnly ? "Saved furniture" : "Browse Furniture");
        SetLabel(
            _browseSubtitleLabel,
            _showSavedOnly
                ? "Furniture saved for placement inside your AR rooms."
                : "Save furniture you like, then place it inside your AR room.");
    }

    private static Label CreateEmptyState(string text)
    {
        var label = new Label(text);
        label.AddToClassList("empty-state");
        return label;
    }

    private static void RegisterClick(VisualElement element, Action action)
    {
        if (element == null || action == null)
            return;

        element.RegisterCallback<ClickEvent>(_ => action());
    }

    private static void ConfigureMobileScrollView(ScrollView scrollView, bool horizontal, bool vertical)
    {
        if (scrollView == null)
            return;

        scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        scrollView.verticalScrollerVisibility = ScrollerVisibility.Hidden;
        scrollView.mode = horizontal && !vertical ? ScrollViewMode.Horizontal : ScrollViewMode.Vertical;
        scrollView.touchScrollBehavior = ScrollView.TouchScrollBehavior.Elastic;
    }

    private void ShowCreateProjectDialog()
    {
        if (_createProjectDialog == null)
            return;

        if (_projectNameField != null)
            _projectNameField.value = string.Empty;

        SetLabel(_projectNameError, string.Empty);
        _createProjectDialog.RemoveFromClassList("hidden");
        _projectNameField?.Focus();
    }

    private void HideCreateProjectDialog()
    {
        if (_createProjectDialog != null)
            _createProjectDialog.AddToClassList("hidden");

        SetLabel(_projectNameError, string.Empty);
    }

    private void CreateNamedProject()
    {
        var projectName = _projectNameField == null ? string.Empty : _projectNameField.value.Trim();
        if (string.IsNullOrEmpty(projectName))
        {
            SetLabel(_projectNameError, "Enter a project name.");
            return;
        }

        OpenNewProject(projectName);
    }

    private void OpenNewProject(string projectName)
    {
        HideCreateProjectDialog();
        var project = LocalAppStore.CreateProject(projectName);
        _data = LocalAppStore.LoadData();
        AppState.ActiveProjectId = project.Id;
        SceneManager.LoadScene(arDesignerSceneName);
    }

    private void OpenProject(string projectId)
    {
        AppState.ActiveProjectId = projectId;
        SceneManager.LoadScene(arDesignerSceneName);
    }

    private void DeleteProject(string projectId)
    {
        var project = _data.Projects.Find(item => item.Id == projectId);
        if (project == null)
            return;

        _data.Projects.Remove(project);
        if (AppState.ActiveProjectId == projectId)
            AppState.ActiveProjectId = null;

        LocalAppStore.SaveData(_data);
        StartCoroutine(_firebase.SyncAppData(AppState.CurrentUser, _data));

        RenderProjects();
        RenderProfileProjects();
        UpdateCounts();
    }

    private void ShowDeleteConfirmation(ProjectRecord project)
    {
        if (project == null || _confirmDialog == null)
            return;

        _pendingDeleteProjectId = project.Id;
        if (_confirmMessage != null)
            _confirmMessage.text = $"Are you sure you want to delete \"{project.Name}\"? This cannot be undone.";

        _confirmDialog.RemoveFromClassList("hidden");
    }

    private void HideDeleteConfirmation()
    {
        _pendingDeleteProjectId = null;
        if (_confirmDialog != null)
            _confirmDialog.AddToClassList("hidden");
    }

    private void ConfirmDeleteProject()
    {
        if (string.IsNullOrEmpty(_pendingDeleteProjectId))
            return;

        var projectId = _pendingDeleteProjectId;
        HideDeleteConfirmation();
        DeleteProject(projectId);
    }

    private void SignOut()
    {
        AppState.SignOut();
        SceneManager.LoadScene("Auth");
    }
}
