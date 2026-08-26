using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImpossibleRobert.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AssetInventory
{
    public sealed class FolderSettingsUI : EditorWindow
    {
        private static readonly Vector2 WindowSize = new Vector2(460f, 430f);
        private static readonly string[] PackageModeOptions = {"Root Folder", "First Level Directories", "Second Level Directories"};

        private FolderSpec _spec;

        public static FolderSettingsUI ShowDropdown(Rect anchor, FolderSpec spec)
        {
            FolderSettingsUI window = CreateInstance<FolderSettingsUI>();
            window.titleContent = new GUIContent("Folder Settings");
            window.minSize = WindowSize;
            window.Init(spec);
            AssetInventoryUITK.ShowAsDropDown(window, anchor, WindowSize);
            return window;
        }

        public static FolderSettingsUI ShowDropdown(EditorWindow owner, VisualElement anchor, FolderSpec spec)
        {
            FolderSettingsUI window = CreateInstance<FolderSettingsUI>();
            window.titleContent = new GUIContent("Folder Settings");
            window.minSize = WindowSize;
            window.Init(spec);
            AssetInventoryUITK.ShowAsDropDown(window, owner, anchor, WindowSize);
            return window;
        }

        public static FolderSettingsUI ShowWindow(FolderSpec spec = null)
        {
            FolderSettingsUI window = GetWindow<FolderSettingsUI>("Folder Settings");
            window.minSize = WindowSize;
            window.Init(spec);
            return window;
        }

        public void Init(FolderSpec spec)
        {
            _spec = spec;
            BuildIfReady();
        }

        private void CreateGUI()
        {
            BuildContent();
        }

        private void BuildIfReady()
        {
            if (rootVisualElement != null && rootVisualElement.childCount > 0)
            {
                BuildContent();
            }
        }

        private void BuildContent()
        {
            VisualElement root = rootVisualElement;
            if (root == null) return;

            root.Clear();
            AssetInventoryUITK.ApplyWindowStyles(root);

            if (_spec == null)
            {
                root.Add(AssetInventoryUITK.CreateHelpBox("No folder selected.", MessageType.Warning));
                root.Add(AssetInventoryUITK.CreateFlexibleSpacer());
                return;
            }

            ScrollView scroll = new ScrollView();
            scroll.AddToClassList("ai-folder-settings-scroll");
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;

            if (ShouldShowAdvanced())
            {
                scroll.Add(CreateLocationSection());
            }

            scroll.Add(CreateContentSection());
            scroll.Add(CreateOptionsSection());

            if (ShouldShowAdvanced())
            {
                scroll.Add(CreateAdvancedSection());
            }

            root.Add(scroll);
        }

        private VisualElement CreateContentSection()
        {
            VisualElement section = AssetInventoryUITK.CreateSection();

            List<string> folderTypes = new List<string>(IndexUI.FolderTypes);
            int selectedFolderType = Mathf.Clamp(_spec.folderType, 0, folderTypes.Count - 1);
            PopupField<string> contentType = new PopupField<string>(folderTypes, selectedFolderType);
            contentType.tooltip = "Type of content to scan for.";
            contentType.RegisterValueChangedCallback(evt =>
            {
                _spec.folderType = folderTypes.IndexOf(evt.newValue);
                SaveAndRebuild();
            });
            section.Add(AssetInventoryUITK.CreateFieldRow("Content", contentType));

            switch (_spec.folderType)
            {
                case 0:
                    section.Add(CreateDisabledToggleRow("Assign Package", true, "Files are connected to a package named after the package file."));
                    section.Add(CreateAssignTagSection());
                    break;

                case 1:
                    AddMediaFields(section);
                    break;

                case 2:
                    section.Add(CreateDisabledToggleRow("Assign Package", true, "Files are connected to a package named after the archive."));
                    section.Add(CreateStringListField("Exclude Extensions", _spec.excludedExtensions, value =>
                    {
                        _spec.excludedExtensions = value;
                        SaveConfig();
                    }, "Excluded Extensions", "e.g. blend,max"));
                    section.Add(CreatePreviewToggle());
                    section.Add(CreateDetectUnityToggle());
                    section.Add(CreateAssignTagSection());
                    break;

                case 3:
                    section.Add(CreatePreviewToggle());
                    section.Add(CreateDetectUnityToggle());
                    section.Add(CreateAssignTagSection());
                    break;
            }

            return section;
        }

        private void AddMediaFields(VisualElement section)
        {
            List<string> mediaTypes = new List<string>(IndexUI.MediaTypes);
            int selectedScanFor = Mathf.Clamp(_spec.scanFor, 0, mediaTypes.Count - 1);
            PopupField<string> scanFor = new PopupField<string>(mediaTypes, selectedScanFor);
            scanFor.tooltip = "File types to search for.";
            scanFor.RegisterValueChangedCallback(evt =>
            {
                _spec.scanFor = mediaTypes.IndexOf(evt.newValue);
                SaveAndRebuild();
            });
            section.Add(AssetInventoryUITK.CreateFieldRow("Find", scanFor));

            if (_spec.scanFor == 7)
            {
                TextField pattern = new TextField {value = _spec.pattern ?? string.Empty, tooltip = "e.g. *.jpg;*.wav"};
                pattern.RegisterValueChangedCallback(evt =>
                {
                    _spec.pattern = evt.newValue;
                    SaveConfig();
                });
                section.Add(AssetInventoryUITK.CreateFieldRow("Pattern", pattern));
            }

            section.Add(CreateStringListField("Exclude Extensions", _spec.excludedExtensions, value =>
            {
                _spec.excludedExtensions = value;
                SaveConfig();
            }, "Excluded Extensions", "e.g. blend,max"));

            section.Add(CreatePreviewToggle());
            section.Add(CreateToggleRow("Remove Orphans", _spec.removeOrphans, "Checks for deleted files and removes them from the index.", value =>
            {
                _spec.removeOrphans = value;
                SaveConfig();
            }));

            section.Add(CreateToggleRow("Assign Package", _spec.attachToPackage, $"Connects indexed files to packages. Otherwise lists them under '{Asset.NONE}'.", value =>
            {
                _spec.attachToPackage = value;
                SaveAndRebuild();
            }));

            if (_spec.attachToPackage)
            {
                List<string> packageModes = new List<string>(PackageModeOptions);
                int selectedPackageMode = Mathf.Clamp(_spec.packageMode, 0, packageModes.Count - 1);
                PopupField<string> packageMode = new PopupField<string>(packageModes, selectedPackageMode);
                packageMode.tooltip = "Controls how the folder structure maps to packages.";
                packageMode.RegisterValueChangedCallback(evt =>
                {
                    _spec.packageMode = packageModes.IndexOf(evt.newValue);
                    SaveConfig();
                });
                section.Add(AssetInventoryUITK.CreateFieldRow("Package Mode", packageMode));
            }

            section.Add(CreateDetectUnityToggle());
            if (_spec.attachToPackage) section.Add(CreateAssignTagSection());
        }

        private VisualElement CreateOptionsSection()
        {
            VisualElement section = AssetInventoryUITK.CreateSection();
            section.Add(CreateToggleRow("Check File Sizes", _spec.checkSize, "Updates files when their size changes. Can slow indexing on network or slow drives.", value =>
            {
                _spec.checkSize = value;
                SaveConfig();
            }));
            return section;
        }

        private VisualElement CreateAdvancedSection()
        {
            VisualElement section = AssetInventoryUITK.CreateSection();

            section.Add(CreateStringListField("Exclude Directories", _spec.excludedDirectories, value =>
            {
                _spec.excludedDirectories = value;
                SaveConfig();
            }, "Excluded Directories", "e.g. node_modules,temp,build"));

            Button relativeButton = AssetInventoryUITK.CreateSecondaryButton(_spec.storeRelative ? "Disable..." : "Enable...", OpenRelativeSettings);
            relativeButton.tooltip = "Persists file paths relative to a named base folder so the database can be reused from different systems.";
            section.Add(AssetInventoryUITK.CreateFieldRow("Store Relative", relativeButton));
            return section;
        }

        private VisualElement CreateAssignTagSection()
        {
            VisualElement container = new VisualElement();
            container.AddToClassList("ai-folder-settings-subgroup");

            container.Add(CreateToggleRow("Assign Tags", _spec.assignTag, "Assigns tags to all found packages for easier filtering.", value =>
            {
                _spec.assignTag = value;
                SaveAndRebuild();
            }));

            if (_spec.assignTag)
            {
                container.Add(CreateStringListField("Tags", _spec.tag, value =>
                {
                    _spec.tag = value;
                    SaveConfig();
                }, "Package Tags", "e.g. essential,2d"));
            }

            return container;
        }

        private VisualElement CreateLocationSection()
        {
            VisualElement section = AssetInventoryUITK.CreateSection();
            string expandedPath = _spec.GetLocation(true);
            bool pathExists = !string.IsNullOrEmpty(expandedPath) && Directory.Exists(expandedPath);

            Label path = AssetInventoryUITK.CreateCopyLabel(string.IsNullOrWhiteSpace(expandedPath) ? "<Invalid Path>" : expandedPath);
            path.AddToClassList("ai-folder-location-path");
            if (!pathExists) path.AddToClassList("ai-folder-location-missing");
            section.Add(AssetInventoryUITK.CreateFieldRow("Location", path));

            if (!pathExists)
            {
                section.Add(AssetInventoryUITK.CreateHelpBox("Folder not found. Use Change Location to point to the new folder location.", MessageType.Warning));
            }

            VisualElement actions = new VisualElement();
            actions.AddToClassList("ai-list-actions");
            actions.AddToClassList("ai-folder-location-actions");
            if (pathExists)
            {
                Button open = AssetInventoryUITK.CreateSecondaryButton("Open", () => EditorUtility.RevealInFinder(expandedPath));
                open.tooltip = "Reveal the folder in the file browser.";
                actions.Add(open);

                Button rename = AssetInventoryUITK.CreateSecondaryButton("Rename...", null);
                rename.clicked += () => OnRenameFolder(rename);
                rename.tooltip = "Rename the folder on disk and update the path.";
                actions.Add(rename);
            }

            Button change = AssetInventoryUITK.CreateSecondaryButton("Change...", OnChangeLocation);
            change.tooltip = "Point to a different folder location.";
            actions.Add(change);
            section.Add(AssetInventoryUITK.CreateFieldRow("Actions", actions));
            return section;
        }

        private VisualElement CreatePreviewToggle()
        {
            return CreateToggleRow("Create Previews", _spec.createPreviews, "Generates previews and additional metadata but requires more indexing time.", value =>
            {
                _spec.createPreviews = value;
                SaveConfig();
            });
        }

        private VisualElement CreateDetectUnityToggle()
        {
            return CreateToggleRow("Unity Projects", _spec.detectUnityProjects, "Detect Unity projects and index only the Assets folder when the selected folder is a project root.", value =>
            {
                _spec.detectUnityProjects = value;
                SaveConfig();
            });
        }

        private static VisualElement CreateDisabledToggleRow(string label, bool value, string tooltip)
        {
            VisualElement row = AssetInventoryUITK.CreateToggleFieldRow(label, value, null, tooltip);
            Toggle toggle = row.Q<Toggle>();
            toggle.SetEnabled(false);
            return row;
        }

        private static VisualElement CreateToggleRow(string label, bool value, string tooltip, Action<bool> onChange)
        {
            return AssetInventoryUITK.CreateToggleFieldRow(label, value, onChange, tooltip, "ai-folder-settings-toggle");
        }

        private VisualElement CreateStringListField(string label, string value, Action<string> onChange, string tooltip, string placeholder)
        {
            VisualElement fieldRow = AssetInventoryUITK.CreateStringListControl(
                this,
                value,
                ",",
                onChange,
                tooltip,
                tooltip,
                "ai-folder-settings-text-field",
                "ai-folder-settings-list-button");

            VisualElement container = new VisualElement();
            container.AddToClassList("ai-folder-settings-list-field");
            container.Add(AssetInventoryUITK.CreateFieldRow(label, fieldRow));
            if (!string.IsNullOrWhiteSpace(placeholder))
            {
                Label hint = AssetInventoryUITK.CreateCopyLabel(placeholder);
                hint.AddToClassList("ai-folder-settings-hint");
                container.Add(hint);
            }
            return container;
        }

        private void OpenRelativeSettings()
        {
            RelativeUI relativeUI = RelativeUI.ShowWindow();
            relativeUI.Init(_spec);
            Close();
        }

        private void SaveConfig()
        {
            AI.SaveConfig();
        }

        private void SaveAndRebuild()
        {
            AI.SaveConfig();
            BuildContent();
        }

        private static bool ShouldShowAdvanced()
        {
            return AI.ShowAdvanced();
        }

        private void OnRenameFolder(Button anchor)
        {
            string currentPath = _spec.GetLocation(true);
            if (string.IsNullOrEmpty(currentPath) || !Directory.Exists(currentPath))
            {
                EditorUtility.DisplayDialog("Invalid Path", "The folder does not exist and cannot be renamed.", "OK");
                return;
            }

            string currentFolderName = Path.GetFileName(currentPath);
            if (string.IsNullOrEmpty(currentFolderName))
            {
                // Handle root paths - can't rename root
                EditorUtility.DisplayDialog("Cannot Rename", "Root directories cannot be renamed.", "OK");
                return;
            }

            NameWindow.ShowAsDropDown(CommonUITK.ToScreenDropdownAnchor(this, anchor), currentFolderName, newName =>
            {
                if (currentFolderName == newName) return;
                if (string.IsNullOrWhiteSpace(newName))
                {
                    EditorUtility.DisplayDialog("Invalid Name", "Folder name cannot be empty.", "OK");
                    return;
                }

                string parentDir = Path.GetDirectoryName(currentPath);
                if (string.IsNullOrEmpty(parentDir))
                {
                    EditorUtility.DisplayDialog("Error", "Cannot determine parent directory.", "OK");
                    return;
                }

                string newPath = Path.Combine(parentDir, newName);

                // Check if target already exists
                if (Directory.Exists(newPath))
                {
                    EditorUtility.DisplayDialog("Folder Exists", $"A folder named '{newName}' already exists in that location.", "OK");
                    return;
                }

                try
                {
                    // Rename the folder on disk
                    Directory.Move(currentPath, newPath);

                    // Update database entries (packages and files) - must be done before updating folder spec
                    UpdateDatabasePaths(currentPath, newPath);

                    // Update the path in the folder spec
                    UpdateFolderLocation(newPath, false); // false = don't close popup yet

                    // Close popup after successful rename
                    Close();
                }
                catch (Exception e)
                {
                    EditorUtility.DisplayDialog("Rename Failed", $"Failed to rename folder: {e.Message}", "OK");
                }
            }, false, "Rename Folder");
        }

        private void OnChangeLocation()
        {
            string currentPath = _spec.GetLocation(true);
            string defaultPath = "";

            if (!string.IsNullOrEmpty(currentPath) && Directory.Exists(currentPath))
            {
                defaultPath = currentPath;
            }

            string folder = EditorUtility.OpenFolderPanel("Select New Folder Location", defaultPath, "");
            if (string.IsNullOrEmpty(folder)) return;

            // Normalize paths for comparison
            string normalizedNewPath = IOUtils.NormalizePath(folder);
            string normalizedCurrentPath = IOUtils.NormalizePath(currentPath);

            // Check if the path actually changed
            if (normalizedNewPath == normalizedCurrentPath)
            {
                // Path hasn't changed, nothing to do
                return;
            }

            // Update database entries (packages and files) - must be done before updating folder spec
            // Update even if current path doesn't exist (e.g., folder was moved/renamed externally)
            if (!string.IsNullOrEmpty(currentPath))
            {
                UpdateDatabasePaths(currentPath, normalizedNewPath);
            }

            // Update the path in the folder spec
            UpdateFolderLocation(folder);
        }

        private void UpdateFolderLocation(string newAbsolutePath, bool closePopup = true)
        {
            if (string.IsNullOrEmpty(newAbsolutePath))
            {
                EditorUtility.DisplayDialog("Invalid Path", "Please select a valid folder.", "OK");
                return;
            }

            // Make absolute and conform to OS separators
            string normalizedPath = Path.GetFullPath(newAbsolutePath);

            // Validate path exists
            if (!Directory.Exists(normalizedPath))
            {
                EditorUtility.DisplayDialog("Invalid Path", "The selected folder does not exist.", "OK");
                return;
            }

            // Update RelativeLocation entry FIRST so MakeRelative detects it as relative below
            string oldRelativeKey = _spec.relativeKey;
            bool wasUsingRelative = !string.IsNullOrEmpty(oldRelativeKey) && oldRelativeKey != "ac" && oldRelativeKey != "pc";

            if (wasUsingRelative)
            {
                string systemId = AI.GetSystemId();
                RelativeLocation relLocation = DBAdapter.DB.Find<RelativeLocation>(rl => rl.Key == oldRelativeKey && rl.System == systemId);
                if (relLocation != null)
                {
                    // Update the relative location to point to the new folder location
                    relLocation.SetLocation(normalizedPath);
                    DBAdapter.DB.Update(relLocation);
                    // Reload relative locations so MakeRelative can use the updated entry
                    Paths.LoadRelativeLocations();
                }
            }

            // Convert to relative path if possible (special case: a relative key is already defined for the folder, replace it immediately)
            // Now that we've updated the RelativeLocation entry, MakeRelative will detect it as relative
            string relativePath = Paths.MakeRelative(normalizedPath);

            // Prevent Unity asset cache folder selection (check after MakeRelative in case it was converted)
            if (relativePath.Contains(AI.ASSET_STORE_FOLDER_NAME))
            {
                EditorUtility.DisplayDialog("Attention", "You selected a custom Unity asset cache location. This should be done by setting the asset cache location above to custom.", "OK");
                return;
            }

            // Ensure no trailing slash if root folder on Windows
            if (relativePath.Length > 1 && relativePath.EndsWith("/"))
            {
                relativePath = relativePath.Substring(0, relativePath.Length - 1);
            }

            // Update location
            _spec.location = relativePath;

            // Update relative path tracking
            if (Paths.IsRel(relativePath))
            {
                _spec.storeRelative = true;
                _spec.relativeKey = Paths.GetRelKey(relativePath);
            }
            else
            {
                _spec.storeRelative = false;
                _spec.relativeKey = null;
            }

            // Save configuration
            AI.SaveConfig();

            // Reload relative locations to pick up any changes
            Paths.LoadRelativeLocations();

            // Close popup to provide feedback that operation completed
            if (closePopup)
            {
                Close();
            }
        }

        private void UpdateDatabasePaths(string oldPath, string newPath)
        {
            // Normalize paths for comparison (use forward slashes)
            string oldPathNormalized = oldPath.Replace("\\", "/").TrimEnd('/');
            string newPathNormalized = newPath.Replace("\\", "/").TrimEnd('/');

            // Get stored path versions (may include relative tags)
            string oldStoredPath = _spec.location;

            // Update packages/assets
            // For Root Folder mode: update packages where Location or SafeName matches the folder
            // For First/Second Level Directories mode: update all packages in subdirectories
            if (_spec.attachToPackage && (_spec.packageMode == 1 || _spec.packageMode == 2))
            {
                // First/Second Level Directories mode: update all packages in subdirectories
                // Find all packages where Location starts with oldPath
                List<Asset> affectedAssets = DBAdapter.DB.Query<Asset>(
                    "SELECT Id, Location, SafeName FROM Asset WHERE Location LIKE ? OR SafeName LIKE ?",
                    oldPathNormalized + "%", oldPathNormalized + "%");

                foreach (Asset asset in affectedAssets)
                {
                    // Only update if the path actually starts with the old path (to avoid false matches)
                    string newLocation = asset.Location;
                    string newSafeName = asset.SafeName;

                    if (!string.IsNullOrEmpty(asset.Location) && (asset.Location == oldPathNormalized || asset.Location.StartsWith(oldPathNormalized + "/")))
                    {
                        newLocation = asset.Location.Replace(oldPathNormalized, newPathNormalized);
                    }

                    if (!string.IsNullOrEmpty(asset.SafeName) && (asset.SafeName == oldPathNormalized || asset.SafeName.StartsWith(oldPathNormalized + "/")))
                    {
                        newSafeName = asset.SafeName.Replace(oldPathNormalized, newPathNormalized);
                    }

                    // Also handle stored path with relative tags if applicable
                    if (Paths.IsRel(oldStoredPath) && !string.IsNullOrEmpty(oldStoredPath))
                    {
                        string oldStoredPathNormalized = Paths.DeRel(oldStoredPath)?.Replace("\\", "/").TrimEnd('/');
                        if (!string.IsNullOrEmpty(oldStoredPathNormalized))
                        {
                            if (!string.IsNullOrEmpty(asset.Location) && (asset.Location == oldStoredPathNormalized || asset.Location.StartsWith(oldStoredPathNormalized + "/")))
                            {
                                newLocation = asset.Location.Replace(oldStoredPathNormalized, newPathNormalized);
                            }
                            if (!string.IsNullOrEmpty(asset.SafeName) && (asset.SafeName == oldStoredPathNormalized || asset.SafeName.StartsWith(oldStoredPathNormalized + "/")))
                            {
                                newSafeName = asset.SafeName.Replace(oldStoredPathNormalized, newPathNormalized);
                            }
                        }
                    }

                    DBAdapter.DB.Execute("UPDATE Asset SET Location = ?, SafeName = ? WHERE Id = ?", newLocation, newSafeName, asset.Id);
                }
            }
            else
            {
                // Root Folder mode: update packages where Location or SafeName matches the folder
                // Use LIKE to catch both exact matches and any edge cases
                List<Asset> affectedAssets = DBAdapter.DB.Query<Asset>(
                    "SELECT Id, Location, SafeName FROM Asset WHERE Location LIKE ? OR SafeName LIKE ?",
                    oldPathNormalized + "%", oldPathNormalized + "%");

                foreach (Asset asset in affectedAssets)
                {
                    // Only update if the path actually starts with the old path (to avoid false matches)
                    string newLocation = asset.Location;
                    string newSafeName = asset.SafeName;

                    if (!string.IsNullOrEmpty(asset.Location) && (asset.Location == oldPathNormalized || asset.Location.StartsWith(oldPathNormalized + "/")))
                    {
                        newLocation = asset.Location.Replace(oldPathNormalized, newPathNormalized);
                    }

                    if (!string.IsNullOrEmpty(asset.SafeName) && (asset.SafeName == oldPathNormalized || asset.SafeName.StartsWith(oldPathNormalized + "/")))
                    {
                        newSafeName = asset.SafeName.Replace(oldPathNormalized, newPathNormalized);
                    }

                    // Also handle stored path with relative tags if applicable
                    if (Paths.IsRel(oldStoredPath) && !string.IsNullOrEmpty(oldStoredPath))
                    {
                        string oldStoredPathNormalized = Paths.DeRel(oldStoredPath)?.Replace("\\", "/").TrimEnd('/');
                        if (!string.IsNullOrEmpty(oldStoredPathNormalized))
                        {
                            if (!string.IsNullOrEmpty(asset.Location) && (asset.Location == oldStoredPathNormalized || asset.Location.StartsWith(oldStoredPathNormalized + "/")))
                            {
                                newLocation = asset.Location.Replace(oldStoredPathNormalized, newPathNormalized);
                            }
                            if (!string.IsNullOrEmpty(asset.SafeName) && (asset.SafeName == oldStoredPathNormalized || asset.SafeName.StartsWith(oldStoredPathNormalized + "/")))
                            {
                                newSafeName = asset.SafeName.Replace(oldStoredPathNormalized, newPathNormalized);
                            }
                        }
                    }

                    DBAdapter.DB.Execute("UPDATE Asset SET Location = ?, SafeName = ? WHERE Id = ?", newLocation, newSafeName, asset.Id);
                }
            }

            // Update asset files (Path and SourcePath)
            // Find all files where Path or SourcePath starts with oldPath
            List<AssetFile> affectedFiles = DBAdapter.DB.Query<AssetFile>(
                "SELECT Id, Path, SourcePath FROM AssetFile WHERE Path LIKE ? OR SourcePath LIKE ?",
                oldPathNormalized + "%", oldPathNormalized + "%");

            foreach (AssetFile file in affectedFiles)
            {
                // Only update if the path actually starts with the old path (to avoid false matches)
                string newFilePath = file.Path;
                string newFileSourcePath = file.SourcePath;

                if (!string.IsNullOrEmpty(file.Path) && (file.Path == oldPathNormalized || file.Path.StartsWith(oldPathNormalized + "/")))
                {
                    newFilePath = file.Path.Replace(oldPathNormalized, newPathNormalized);
                }

                if (!string.IsNullOrEmpty(file.SourcePath) && (file.SourcePath == oldPathNormalized || file.SourcePath.StartsWith(oldPathNormalized + "/")))
                {
                    newFileSourcePath = file.SourcePath.Replace(oldPathNormalized, newPathNormalized);
                }

                // Also handle stored path with relative tags if applicable
                if (Paths.IsRel(oldStoredPath) && !string.IsNullOrEmpty(oldStoredPath))
                {
                    string oldStoredPathNormalized = Paths.DeRel(oldStoredPath)?.Replace("\\", "/").TrimEnd('/');
                    if (!string.IsNullOrEmpty(oldStoredPathNormalized))
                    {
                        if (!string.IsNullOrEmpty(file.Path) && (file.Path == oldStoredPathNormalized || file.Path.StartsWith(oldStoredPathNormalized + "/")))
                        {
                            newFilePath = file.Path.Replace(oldStoredPathNormalized, newPathNormalized);
                        }
                        if (!string.IsNullOrEmpty(file.SourcePath) && (file.SourcePath == oldStoredPathNormalized || file.SourcePath.StartsWith(oldStoredPathNormalized + "/")))
                        {
                            newFileSourcePath = file.SourcePath.Replace(oldStoredPathNormalized, newPathNormalized);
                        }
                    }
                }

                DBAdapter.DB.Execute("UPDATE AssetFile SET Path = ?, SourcePath = ? WHERE Id = ?", newFilePath, newFileSourcePath, file.Id);
            }
        }
    }
}
