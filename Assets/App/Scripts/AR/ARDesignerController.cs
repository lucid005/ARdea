using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;
using UnityEngine.InputSystem;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public sealed class ARDesignerController : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private ARRaycastManager raycastManager;
    [SerializeField] private string mainAppSceneName = "MainApp";

    private static readonly List<ARRaycastHit> Hits = new List<ARRaycastHit>();

    private VisualElement _inventorySheet;
    private VisualElement _placedPanel;
    private VisualElement _selectionControls;
    private ScrollView _inventoryList;
    private ScrollView _placedList;
    private VisualElement _inventoryChipRow;
    private TextField _inventorySearch;
    private Label _selectedLabel;
    private Button _moveToolButton;
    private Button _rotateToolButton;
    private Button _scaleToolButton;
    private ProjectRecord _project;
    private readonly List<PlacedObject> _placedObjects = new List<PlacedObject>();
    private FurnitureRecord _pendingFurniture;
    private LineRenderer _selectionBounds;
    private int _selectedIndex = -1;
    private int _skipPlacementFrames;
    private string _activeInventoryCategory = "All";
    private bool _isDraggingSelected;
    private TransformMode _transformMode = TransformMode.Move;
    private Vector2 _dragStartScreenPoint;
    private Quaternion _dragStartRotation;
    private Vector3 _dragStartScale;

    private void Awake()
    {
        EnsureArRuntime();
    }

    private void OnEnable()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (raycastManager == null)
            raycastManager = FindFirstObjectByType<ARRaycastManager>();

        AppState.RestoreSession();
        LoadProject();

        var root = uiDocument.rootVisualElement;
        _inventorySheet = root.Q<VisualElement>("inventory-sheet");
        _placedPanel = root.Q<VisualElement>("placed-panel");
        _selectionControls = root.Q<VisualElement>("selection-controls");
        _inventoryList = root.Q<ScrollView>("inventory-list");
        _placedList = root.Q<ScrollView>("placed-list");
        ConfigureMobileScrollView(_inventoryList);
        ConfigureMobileScrollView(_placedList);
        _inventoryChipRow = root.Q<VisualElement>("inventory-chip-row");
        _inventorySearch = root.Q<TextField>("inventory-search");
        _selectedLabel = root.Q<Label>("selected-label");
        _moveToolButton = root.Q<Button>("move-tool-btn");
        _rotateToolButton = root.Q<Button>("rotate-tool-btn");
        _scaleToolButton = root.Q<Button>("scale-tool-btn");

        root.Q<Button>("ar-back-btn").clicked += BackToMainApp;
        root.Q<Button>("inventory-btn").clicked += ToggleInventory;
        root.Q<Button>("placed-list-btn").clicked += TogglePlacedPanel;
        root.Q<Button>("delete-btn").clicked += DeleteSelectedOrLast;
        root.Q<Button>("save-project-btn").clicked += SaveProject;

        _moveToolButton.clicked += () => SetTransformMode(TransformMode.Move);
        _rotateToolButton.clicked += () => SetTransformMode(TransformMode.Rotate);
        _scaleToolButton.clicked += () => SetTransformMode(TransformMode.Scale);
        root.RegisterCallback<ClickEvent>(ClosePanelsWhenClickingOutside);

        var browseMore = root.Q<Button>("browse-more-btn");
        if (browseMore != null)
            browseMore.clicked += ToggleInventory;

        if (_inventorySearch != null)
            _inventorySearch.RegisterValueChangedCallback(_ => RenderInventory());

        RenderInventoryCategories();
        RenderInventory();
        RestorePlacedObjects();
        RenderPlacedList();
        SetTransformMode(TransformMode.Move);
    }

    private static void EnsureArRuntime()
    {
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            Permission.RequestUserPermission(Permission.Camera);
#endif

        if (FindFirstObjectByType<ARSession>() == null)
        {
            var session = new GameObject("AR Session");
            session.AddComponent<ARSession>();
            session.AddComponent<ARInputManager>();
        }

        var camera = Camera.main;
        if (camera == null)
            return;

        if (camera.GetComponent<ARCameraManager>() == null)
            camera.gameObject.AddComponent<ARCameraManager>();

        if (camera.GetComponent<ARCameraBackground>() == null)
            camera.gameObject.AddComponent<ARCameraBackground>();
    }

    private void Update()
    {
        if (_pendingFurniture == null)
        {
            HandlePlacedObjectInteraction();
            return;
        }

        if (_skipPlacementFrames > 0)
        {
            _skipPlacementFrames--;
            return;
        }

        if (TryGetPlacementPose(out var pose))
            PlacePendingFurniture(pose);
    }

    private void ToggleInventory()
    {
        TogglePanel(_inventorySheet);
        HidePanel(_placedPanel);
        RefreshSelectionControls();
    }

    private void RenderInventoryCategories()
    {
        if (_inventoryChipRow == null)
            return;

        _inventoryChipRow.Clear();
        foreach (var category in FurnitureCatalog.Categories)
        {
            var captured = category;
            var chip = new Button(() =>
            {
                _activeInventoryCategory = captured;
                RenderInventoryCategories();
                RenderInventory();
            }) { text = captured };

            chip.AddToClassList("sheet-chip");
            chip.EnableInClassList("sheet-chip-active", captured == _activeInventoryCategory);
            _inventoryChipRow.Add(chip);
        }
    }

    private void RenderInventory()
    {
        if (_inventoryList == null)
            return;

        _inventoryList.Clear();
        var data = LocalAppStore.LoadData();
        var query = _inventorySearch == null ? string.Empty : _inventorySearch.value?.Trim() ?? string.Empty;
        var count = 0;

        foreach (var savedId in data.SavedFurnitureIds)
        {
            var furniture = FurnitureCatalog.Find(savedId);
            if (_activeInventoryCategory != "All" && furniture.Category != _activeInventoryCategory)
                continue;

            if (!string.IsNullOrEmpty(query) &&
                furniture.Title.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) < 0 &&
                furniture.Category.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            _inventoryList.Add(CreateInventoryItem(furniture));
            count++;
        }

        if (count == 0)
            _inventoryList.Add(CreateEmptyLabel("No saved furniture yet. Save items from Browse first."));
    }

    private VisualElement CreateInventoryItem(FurnitureRecord furniture)
    {
        var item = new VisualElement();
        item.AddToClassList("inventory-item");

        var thumb = new VisualElement();
        thumb.AddToClassList("inventory-thumb");

        var copy = new VisualElement();
        copy.AddToClassList("inventory-copy");
        var title = new Label(furniture.Title);
        title.AddToClassList("item-title");
        var subtitle = new Label(furniture.Category);
        subtitle.AddToClassList("item-subtitle");
        copy.Add(title);
        copy.Add(subtitle);

        var place = new Button(() => BeginPlacement(furniture)) { text = "Place" };
        place.AddToClassList("place-button");

        item.Add(thumb);
        item.Add(copy);
        item.Add(place);
        return item;
    }

    private void BeginPlacement(FurnitureRecord furniture)
    {
        _pendingFurniture = furniture;
        _skipPlacementFrames = 2;
        HidePanel(_inventorySheet);
        HidePanel(_placedPanel);
        ClearSelection();
        Debug.Log("[ARDesigner] Tap a detected surface to place " + _pendingFurniture.Title);
    }

    private bool TryGetPlacementPose(out Pose pose)
    {
        pose = default;

        if (raycastManager == null)
            return TryGetEditorFallbackPose(out pose);

        Vector2 screenPoint;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            screenPoint = Touchscreen.current.primaryTouch.position.ReadValue();
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            screenPoint = Mouse.current.position.ReadValue();
        else
            return false;

        if (raycastManager.Raycast(screenPoint, Hits, TrackableType.PlaneWithinPolygon))
        {
            pose = Hits[0].pose;
            return true;
        }

        return TryGetEditorFallbackPose(out pose);
    }

    private static bool TryGetEditorFallbackPose(out Pose pose)
    {
        pose = default;
        if (!Application.isEditor || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return false;

        var camera = Camera.main;
        if (camera == null)
            return false;

        var position = camera.transform.position + camera.transform.forward * 1.4f;
        position.y -= 0.45f;
        pose = new Pose(position, Quaternion.identity);
        return true;
    }

    private void PlacePendingFurniture(Pose pose)
    {
        var instance = FurniturePrefabResolver.Instantiate(_pendingFurniture, pose.position, pose.rotation);
        EnsureSelectableCollider(instance);
        _placedObjects.Add(new PlacedObject(_pendingFurniture, instance));
        SelectObject(_placedObjects.Count - 1);
        _pendingFurniture = null;
        SaveProject();
        RenderPlacedList();
    }

    private void DeleteSelectedOrLast()
    {
        if (_selectedIndex >= 0)
        {
            DeletePlacedObject(_selectedIndex);
            return;
        }

        DeleteLastPlaced();
    }

    private void DeleteLastPlaced()
    {
        if (_placedObjects.Count == 0)
            return;

        var last = _placedObjects[_placedObjects.Count - 1];
        if (last.Instance != null)
            Destroy(last.Instance);

        _placedObjects.RemoveAt(_placedObjects.Count - 1);
        ClearSelection();
        SaveProject();
        RenderPlacedList();
    }

    private void LoadProject()
    {
        var data = LocalAppStore.LoadData();
        if (string.IsNullOrEmpty(AppState.ActiveProjectId))
        {
            _project = LocalAppStore.CreateProject("Untitled room");
            AppState.ActiveProjectId = _project.Id;
            return;
        }

        _project = data.Projects.Find(item => item.Id == AppState.ActiveProjectId);
        if (_project == null)
        {
            _project = LocalAppStore.CreateProject("Untitled room");
            AppState.ActiveProjectId = _project.Id;
        }
    }

    private void RestorePlacedObjects()
    {
        if (_project?.PlacedFurniture == null)
            return;

        foreach (var placed in _project.PlacedFurniture)
        {
            var furniture = FurnitureCatalog.Find(placed.FurnitureId);
            var instance = FurniturePrefabResolver.Instantiate(furniture, placed.Position.ToVector3(), Quaternion.Euler(placed.Rotation.ToVector3()));
            instance.transform.localScale = placed.Scale.ToVector3();
            EnsureSelectableCollider(instance);

            _placedObjects.Add(new PlacedObject(furniture, instance));
        }
    }

    private void RenderPlacedList()
    {
        if (_placedList == null)
            return;

        _placedList.Clear();
        if (_placedObjects.Count == 0)
        {
            _placedList.Add(CreateEmptyLabel("No placed furniture yet."));
            return;
        }

        for (var i = 0; i < _placedObjects.Count; i++)
        {
            var index = i;
            _placedList.Add(CreatePlacedItem(_placedObjects[index], index));
        }
    }

    private VisualElement CreatePlacedItem(PlacedObject placed, int index)
    {
        var item = new VisualElement();
        item.AddToClassList("placed-item");

        var copy = new VisualElement();
        copy.AddToClassList("placed-copy");
        var title = new Label(placed.Furniture.Title);
        title.AddToClassList("item-title");
        var subtitle = new Label(placed.Furniture.Category);
        subtitle.AddToClassList("item-subtitle");
        copy.Add(title);
        copy.Add(subtitle);

        var focus = new Button(() => FocusPlacedObject(index)) { text = "Focus" };
        focus.AddToClassList("focus-button");
        var delete = new Button(() => DeletePlacedObject(index)) { text = "Delete" };
        delete.AddToClassList("delete-item-button");

        item.Add(copy);
        item.Add(focus);
        item.Add(delete);
        return item;
    }

    private void FocusPlacedObject(int index)
    {
        if (index < 0 || index >= _placedObjects.Count || _placedObjects[index].Instance == null)
            return;

        SelectObject(index);
        HidePanel(_placedPanel);
        Debug.Log("[ARDesigner] Focus " + _placedObjects[index].Furniture.Title);
    }

    private void DeletePlacedObject(int index)
    {
        if (index < 0 || index >= _placedObjects.Count)
            return;

        var placed = _placedObjects[index];
        if (placed.Instance != null)
            Destroy(placed.Instance);

        _placedObjects.RemoveAt(index);
        if (_selectedIndex == index)
            ClearSelection();
        else if (_selectedIndex > index)
            _selectedIndex--;

        RefreshSelectionControls();
        SaveProject();
        RenderPlacedList();
    }

    private void HandlePlacedObjectInteraction()
    {
        if (!TryGetPointer(out var screenPoint, out var wasPressed, out var isPressed, out var wasReleased))
            return;

        if (wasReleased && _isDraggingSelected)
        {
            _isDraggingSelected = false;
            SaveProject();
            return;
        }

        if (IsLikelyUiPoint(screenPoint))
            return;

        if (wasPressed)
        {
            var hitIndex = RaycastPlacedObject(screenPoint);
            if (hitIndex >= 0)
            {
                SelectObject(hitIndex);
                BeginSelectedDrag(screenPoint);
            }
            else
            {
                ClearSelection();
            }
        }

        if (isPressed && _selectedIndex >= 0 && _isDraggingSelected)
            ApplySelectedTransform(screenPoint);
    }

    private void BeginSelectedDrag(Vector2 screenPoint)
    {
        var selected = GetSelectedInstance();
        if (selected == null)
            return;

        _isDraggingSelected = true;
        _dragStartScreenPoint = screenPoint;
        _dragStartRotation = selected.transform.rotation;
        _dragStartScale = selected.transform.localScale;
    }

    private void ApplySelectedTransform(Vector2 screenPoint)
    {
        switch (_transformMode)
        {
            case TransformMode.Move:
                MoveSelectedToPointer(screenPoint);
                break;
            case TransformMode.Rotate:
                DragRotateSelected(screenPoint);
                break;
            case TransformMode.Scale:
                DragScaleSelected(screenPoint);
                break;
        }
    }

    private bool MoveSelectedToPointer(Vector2 screenPoint)
    {
        if (_selectedIndex < 0 || _selectedIndex >= _placedObjects.Count)
            return false;

        var selected = _placedObjects[_selectedIndex].Instance;
        if (selected == null)
            return false;

        if (TryGetSurfacePose(screenPoint, out var pose))
        {
            selected.transform.position = pose.position;
            UpdateSelectionBounds();
            return true;
        }

        return false;
    }

    private void DragRotateSelected(Vector2 screenPoint)
    {
        var selected = GetSelectedInstance();
        if (selected == null)
            return;

        var delta = screenPoint.x - _dragStartScreenPoint.x;
        selected.transform.rotation = _dragStartRotation * Quaternion.Euler(0f, delta * 0.35f, 0f);
        UpdateSelectionBounds();
    }

    private void DragScaleSelected(Vector2 screenPoint)
    {
        var selected = GetSelectedInstance();
        if (selected == null)
            return;

        var delta = screenPoint.y - _dragStartScreenPoint.y;
        var multiplier = Mathf.Clamp(1f + delta / 360f, 0.25f, 4f);
        var scale = _dragStartScale * multiplier;
        selected.transform.localScale = new Vector3(
            Mathf.Clamp(scale.x, 0.15f, 4f),
            Mathf.Clamp(scale.y, 0.15f, 4f),
            Mathf.Clamp(scale.z, 0.15f, 4f));
        UpdateSelectionBounds();
    }

    private int RaycastPlacedObject(Vector2 screenPoint)
    {
        var camera = Camera.main;
        if (camera == null)
            return -1;

        var ray = camera.ScreenPointToRay(screenPoint);
        if (!Physics.Raycast(ray, out var hit, 100f))
            return -1;

        for (var i = 0; i < _placedObjects.Count; i++)
        {
            var instance = _placedObjects[i].Instance;
            if (instance == null)
                continue;

            if (hit.collider.transform == instance.transform || hit.collider.transform.IsChildOf(instance.transform))
                return i;
        }

        return -1;
    }

    private bool TryGetSurfacePose(Vector2 screenPoint, out Pose pose)
    {
        pose = default;

        if (raycastManager != null && raycastManager.Raycast(screenPoint, Hits, TrackableType.PlaneWithinPolygon))
        {
            pose = Hits[0].pose;
            return true;
        }

        return TryGetEditorPointerPose(screenPoint, out pose);
    }

    private static bool TryGetEditorPointerPose(Vector2 screenPoint, out Pose pose)
    {
        pose = default;
        if (!Application.isEditor)
            return false;

        var camera = Camera.main;
        if (camera == null)
            return false;

        var ray = camera.ScreenPointToRay(screenPoint);
        var plane = new Plane(Vector3.up, new Vector3(0f, camera.transform.position.y - 1.2f, 0f));
        if (!plane.Raycast(ray, out var distance))
            return false;

        pose = new Pose(ray.GetPoint(distance), Quaternion.identity);
        return true;
    }

    private static bool TryGetPointer(out Vector2 screenPoint, out bool wasPressed, out bool isPressed, out bool wasReleased)
    {
        screenPoint = default;
        wasPressed = false;
        isPressed = false;
        wasReleased = false;

        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;
            screenPoint = touch.position.ReadValue();
            wasPressed = touch.press.wasPressedThisFrame;
            isPressed = touch.press.isPressed;
            wasReleased = touch.press.wasReleasedThisFrame;
            return wasPressed || isPressed || wasReleased;
        }

        if (Mouse.current != null)
        {
            screenPoint = Mouse.current.position.ReadValue();
            wasPressed = Mouse.current.leftButton.wasPressedThisFrame;
            isPressed = Mouse.current.leftButton.isPressed;
            wasReleased = Mouse.current.leftButton.wasReleasedThisFrame;
            return wasPressed || isPressed || wasReleased;
        }

        return false;
    }

    private bool IsLikelyUiPoint(Vector2 screenPoint)
    {
        if (!IsHidden(_inventorySheet) || !IsHidden(_placedPanel))
            return true;

        if (Screen.height <= 0)
            return false;

        var normalizedY = screenPoint.y / Screen.height;
        return normalizedY < 0.24f || normalizedY > 0.90f;
    }

    private void SelectObject(int index)
    {
        if (index < 0 || index >= _placedObjects.Count || _placedObjects[index].Instance == null)
            return;

        _selectedIndex = index;
        RefreshSelectionControls();
        UpdateSelectionBounds();
    }

    private void ClearSelection()
    {
        _selectedIndex = -1;
        _isDraggingSelected = false;
        RefreshSelectionControls();

        if (_selectionBounds != null)
            _selectionBounds.enabled = false;
    }

    private void RefreshSelectionControls()
    {
        if (_selectionControls == null)
            return;

        var hasSelection = _selectedIndex >= 0 && _selectedIndex < _placedObjects.Count && IsHidden(_inventorySheet) && IsHidden(_placedPanel);
        _selectionControls.EnableInClassList("hidden", !hasSelection);

        if (hasSelection && _selectedLabel != null)
            _selectedLabel.text = _placedObjects[_selectedIndex].Furniture.Title;
    }

    private void SetTransformMode(TransformMode mode)
    {
        _transformMode = mode;
        _moveToolButton?.EnableInClassList("tool-button-active", mode == TransformMode.Move);
        _rotateToolButton?.EnableInClassList("tool-button-active", mode == TransformMode.Rotate);
        _scaleToolButton?.EnableInClassList("tool-button-active", mode == TransformMode.Scale);
    }

    private void RotateSelected(float degrees)
    {
        var selected = GetSelectedInstance();
        if (selected == null)
            return;

        selected.transform.Rotate(Vector3.up, degrees, Space.World);
        UpdateSelectionBounds();
        SaveProject();
    }

    private void ScaleSelected(float multiplier)
    {
        var selected = GetSelectedInstance();
        if (selected == null)
            return;

        var scale = selected.transform.localScale * multiplier;
        selected.transform.localScale = new Vector3(
            Mathf.Clamp(scale.x, 0.15f, 4f),
            Mathf.Clamp(scale.y, 0.15f, 4f),
            Mathf.Clamp(scale.z, 0.15f, 4f));

        UpdateSelectionBounds();
        SaveProject();
    }

    private GameObject GetSelectedInstance()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _placedObjects.Count)
            return null;

        return _placedObjects[_selectedIndex].Instance;
    }

    private void EnsureSelectableCollider(GameObject instance)
    {
        if (instance == null || instance.GetComponentInChildren<Collider>() != null)
            return;

        var renderers = instance.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            var defaultCollider = instance.AddComponent<BoxCollider>();
            defaultCollider.size = Vector3.one;
            return;
        }

        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        var collider = instance.AddComponent<BoxCollider>();
        collider.center = instance.transform.InverseTransformPoint(bounds.center);
        collider.size = new Vector3(
            Mathf.Max(bounds.size.x / Mathf.Max(instance.transform.lossyScale.x, 0.001f), 0.05f),
            Mathf.Max(bounds.size.y / Mathf.Max(instance.transform.lossyScale.y, 0.001f), 0.05f),
            Mathf.Max(bounds.size.z / Mathf.Max(instance.transform.lossyScale.z, 0.001f), 0.05f));
    }

    private void UpdateSelectionBounds()
    {
        var selected = GetSelectedInstance();
        if (selected == null)
            return;

        if (!TryGetRendererBounds(selected, out var bounds))
            return;

        if (_selectionBounds == null)
            _selectionBounds = CreateSelectionBoundsRenderer();

        _selectionBounds.enabled = true;
        _selectionBounds.positionCount = 16;
        _selectionBounds.SetPositions(BuildBoundsLine(bounds));
    }

    private static bool TryGetRendererBounds(GameObject instance, out Bounds bounds)
    {
        bounds = default;
        var renderers = instance.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return false;

        bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return true;
    }

    private static Vector3[] BuildBoundsLine(Bounds bounds)
    {
        var min = bounds.min;
        var max = bounds.max;
        var a = new Vector3(min.x, min.y, min.z);
        var b = new Vector3(max.x, min.y, min.z);
        var c = new Vector3(max.x, min.y, max.z);
        var d = new Vector3(min.x, min.y, max.z);
        var e = new Vector3(min.x, max.y, min.z);
        var f = new Vector3(max.x, max.y, min.z);
        var g = new Vector3(max.x, max.y, max.z);
        var h = new Vector3(min.x, max.y, max.z);

        return new[]
        {
            a, b, c, d, a,
            e, f, g, h, e,
            f, b, c, g, h, d
        };
    }

    private static LineRenderer CreateSelectionBoundsRenderer()
    {
        var go = new GameObject("Selection Bounds");
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.widthMultiplier = 0.012f;
        line.numCornerVertices = 2;
        line.numCapVertices = 2;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = new Color(0.05f, 0.78f, 0.76f, 1f);
        line.endColor = new Color(0.05f, 0.78f, 0.76f, 1f);
        return line;
    }

    private static Label CreateEmptyLabel(string text)
    {
        var label = new Label(text);
        label.AddToClassList("empty-copy");
        return label;
    }

    private static void ConfigureMobileScrollView(ScrollView scrollView)
    {
        if (scrollView == null)
            return;

        scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        scrollView.verticalScrollerVisibility = ScrollerVisibility.Hidden;
        scrollView.mode = ScrollViewMode.Vertical;
        scrollView.touchScrollBehavior = ScrollView.TouchScrollBehavior.Elastic;
    }

    private void SaveProject()
    {
        if (_project == null)
            return;

        _project.UpdatedLabel = "Edited just now";
        _project.PlacedFurniture.Clear();

        foreach (var placed in _placedObjects)
        {
            if (placed.Instance == null)
                continue;

            _project.PlacedFurniture.Add(new PlacedFurnitureRecord
            {
                FurnitureId = placed.Furniture.Id,
                Title = placed.Furniture.Title,
                Category = placed.Furniture.Category,
                Position = new SerializableVector3(placed.Instance.transform.position),
                Rotation = new SerializableVector3(placed.Instance.transform.eulerAngles),
                Scale = new SerializableVector3(placed.Instance.transform.localScale)
            });
        }

        LocalAppStore.SaveProject(_project);
        var firebase = GetComponent<FirebaseRestService>();
        if (firebase == null)
            firebase = gameObject.AddComponent<FirebaseRestService>();

        StartCoroutine(firebase.SyncAppData(AppState.CurrentUser, LocalAppStore.LoadData()));
    }

    private void TogglePlacedPanel()
    {
        TogglePanel(_placedPanel);
        HidePanel(_inventorySheet);
        RefreshSelectionControls();
    }

    private void ClosePanelsWhenClickingOutside(ClickEvent evt)
    {
        if (IsHidden(_inventorySheet) && IsHidden(_placedPanel))
            return;

        if (ClickedElement(evt.target, "inventory-btn") || ClickedElement(evt.target, "placed-list-btn"))
            return;

        if (IsPointInsideVisiblePanel(evt.position, _inventorySheet) || IsPointInsideVisiblePanel(evt.position, _placedPanel))
            return;

        HidePanel(_inventorySheet);
        HidePanel(_placedPanel);
        RefreshSelectionControls();
    }

    private static bool IsPointInsideVisiblePanel(Vector2 panelPosition, VisualElement panel)
    {
        return !IsHidden(panel) && panel.worldBound.Contains(panelPosition);
    }

    private static bool ClickedElement(object target, string elementName)
    {
        var current = target as VisualElement;
        while (current != null)
        {
            if (current.name == elementName)
                return true;

            current = current.parent;
        }

        return false;
    }

    private static void TogglePanel(VisualElement panel)
    {
        if (panel == null)
            return;

        panel.ToggleInClassList("hidden");
    }

    private static void HidePanel(VisualElement panel)
    {
        if (panel != null)
            panel.AddToClassList("hidden");
    }

    private static bool IsHidden(VisualElement panel)
    {
        return panel == null || panel.ClassListContains("hidden");
    }

    private void BackToMainApp()
    {
        SaveProject();
        SceneManager.LoadScene(mainAppSceneName);
    }

    private sealed class PlacedObject
    {
        public readonly FurnitureRecord Furniture;
        public readonly GameObject Instance;

        public PlacedObject(FurnitureRecord furniture, GameObject instance)
        {
            Furniture = furniture;
            Instance = instance;
        }
    }

    private enum TransformMode
    {
        Move,
        Rotate,
        Scale
    }
}
