using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MissingChecker
{
    internal class MissingCheckerWindow : EditorWindow
    {
        private const int DEFAULT_UI_SIZE = 24;

        private const float DEFAULT_SPACE = 8f;

        private static MissingCheckerWindow _window;

        private ExecuteSetting _executeSetting = new ExecuteSetting();

        private GUIContent _deleteButtonTexture;
        private GUIContent _createNewButtonTexture;

        private readonly List<string> _missingPropertyObjects = new List<string>();

        private readonly Dictionary<string, string> _supportedExtensions = new Dictionary<string, string>()
        {
            [".asset"] = "d_ScriptableObject Icon",
            [".mat"] = "d_Material Icon",
            [".prefab"] = "d_Prefab Icon",
            [".anim"] = "d_Animation Icon",
        };

        private readonly Dictionary<string, GUIContent> _objectIcon = new Dictionary<string, GUIContent>();

        [MenuItem("MissingChecker/Open window")]
        internal static void Open()
        {
            if (_window == null)
            {
                _window = GetWindow<MissingCheckerWindow>();
            }
            _window.OnShowWindow();
            _window.Show();
        }

        private void OnShowWindow()
        {
            titleContent = new UnityEngine.GUIContent(Localization.Get("MissingChecker"));
        }

        private void OnEnable()
        {
            _deleteButtonTexture = EditorGUIUtility.IconContent("winbtn_win_close");
            _createNewButtonTexture = EditorGUIUtility.IconContent("CreateAddNew");

            foreach (var icon in _supportedExtensions)
            {
                _objectIcon[icon.Key] = EditorGUIUtility.IconContent(icon.Value);
            }
        }

        private void OnGUI()
        {
            DrawCheckPathList();
            DrawCheckExtensionList();

            if (_executeSetting.CheckAssetPaths.Count == 0)
            {
                return;
            }
            DrawReadWritePreset();

            if (GUILayout.Button(Localization.Get("Execute")))
            {
                _missingPropertyObjects.Clear();
                var request = new ExecuteRequest(_executeSetting);
                request.OnChecked += OnCheckAt;
                request.OnSuccess += OnSuccess;
                request.OnException += OnException;
                MissingCheckerController.ExecuteMissingCheck(request);
            }

            ShowResults();
        }

        private void DrawCheckPathList()
        {
            GUILayout.Label("Check asset path", EditorStyles.boldLabel);
            using (new GUILayout.VerticalScope())
            {
                int i = 0;
                for (i = 0; i < _executeSetting.CheckAssetPaths.Count; i++)
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(_deleteButtonTexture, GUILayout.Width(DEFAULT_UI_SIZE), GUILayout.Height(DEFAULT_UI_SIZE)))
                        {
                            break;
                        }
                        _executeSetting.CheckAssetPaths[i] = GUILayout.TextField(_executeSetting.CheckAssetPaths[i], GUILayout.Height(DEFAULT_UI_SIZE));
                    }
                    (var valid, string cause) = PathUtility.ValidateCheckPath(_executeSetting.CheckAssetPaths[i]);
                    if (!valid)
                    {
                        using (new GUILayout.HorizontalScope())
                        {
                            var warnIcon = EditorGUIUtility.IconContent("Warning");
                            GUILayout.Box(warnIcon, GUIStyle.none);
                            GUILayout.Label(cause, EditorStyles.boldLabel);
                            GUILayout.FlexibleSpace();
                        }
                    }
                    GUILayout.Space(DEFAULT_SPACE);
                }
                if (i != _executeSetting.CheckAssetPaths.Count)
                {
                    _executeSetting.CheckAssetPaths.RemoveAt(i);
                }

                if (GUILayout.Button(_createNewButtonTexture))
                {
                    _executeSetting.CheckAssetPaths.Add("");
                }
            }
        }

        private void DrawCheckExtensionList()
        {
            GUILayout.Label("Check file extensions", EditorStyles.boldLabel);
            using (new GUILayout.VerticalScope())
            {
                int i = 0;
                for (i = 0; i < _executeSetting.CheckFileExtensions.Count; i++)
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(_deleteButtonTexture, GUILayout.Width(DEFAULT_UI_SIZE), GUILayout.Height(DEFAULT_UI_SIZE)))
                        {
                            break;
                        }
                        _executeSetting.CheckFileExtensions[i] = GUILayout.TextField(_executeSetting.CheckFileExtensions[i], GUILayout.Height(DEFAULT_UI_SIZE));
                    }
                    (var valid, string cause) = PathUtility.ValidateCheckExtension(_executeSetting.CheckFileExtensions[i]);
                    if (!valid)
                    {
                        using (new GUILayout.HorizontalScope())
                        {
                            var warnIcon = EditorGUIUtility.IconContent("Warning");
                            GUILayout.Box(warnIcon, GUIStyle.none, GUILayout.Width(DEFAULT_UI_SIZE), GUILayout.Height(DEFAULT_UI_SIZE));
                            GUILayout.Label(cause, EditorStyles.boldLabel);
                            GUILayout.FlexibleSpace();
                        }
                    }
                    GUILayout.Space(DEFAULT_SPACE);
                }
                if (i != _executeSetting.CheckFileExtensions.Count)
                {
                    _executeSetting.CheckFileExtensions.RemoveAt(i);
                }

                if (GUILayout.Button(_createNewButtonTexture))
                {
                    _executeSetting.CheckFileExtensions.Add("");
                }
            }
        }

        private void DrawReadWritePreset()
        {
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button(Localization.Get("Save Preset")))
                {
                    ShowSavePresetConfirmDialog();
                }
                if (GUILayout.Button(Localization.Get("Load Preset")))
                {
                    ShowLoadPresetConfirmDialog();
                }
            }
        }

        private void ShowSavePresetConfirmDialog()
        {
            if (!Directory.Exists(PathUtility.PresetDirectory))
            {
                Directory.CreateDirectory(PathUtility.PresetDirectory);
            }
            var savedPath = EditorUtility.SaveFilePanel("Save preset", PathUtility.PresetDirectory, "new_preset", "json");
            if (string.IsNullOrEmpty(savedPath))
            {
                return;
            }

            var savedDirectory = PathUtility.GetDirectoryName(savedPath);
            var savedDirectoryInfo = PathUtility.GetDirectoryInfo(savedDirectory);
            var targetDirectoryInfo = PathUtility.GetDirectoryInfo(PathUtility.PresetDirectory);
            if (savedDirectoryInfo == targetDirectoryInfo)
            {
                EditorUtility.DisplayDialog(Localization.Get("Warning"), $"Preset can't be saved at {savedDirectory}\nPreset must be saved at {PathUtility.PresetDirectory}", "Close");
                return;
            }

            var savedFileName = PathUtility.GetFileName(savedPath);
            LocalStorageUtility.SaveAsJson(_executeSetting, $"{PathUtility.PRESET}/{savedFileName}.json");
        }

        private void ShowLoadPresetConfirmDialog()
        {
            var loadPath = EditorUtility.OpenFilePanel("Load Preset", PathUtility.PresetDirectory, "json");
            if (string.IsNullOrEmpty(loadPath))
            {
                return;
            }
            var preset = LocalStorageUtility.LoadFromAbsolutePath<ExecuteSetting>(loadPath);
            if (preset != null)
            {
                _executeSetting = preset;
            }
        }

        private void ShowResults()
        {
            if (_missingPropertyObjects.Count == 0)
            {
                return;
            }

            using (new GUILayout.VerticalScope())
            {
                GUILayout.Label("Missingが見つかったもの", EditorStyles.boldLabel);
                for (var i = 0; i < _missingPropertyObjects.Count; i++)
                {
                    var fileName = Path.GetFileName(_missingPropertyObjects[i]);
                    var extension = Path.GetExtension(_missingPropertyObjects[i]);
                    GUIContent icon = _objectIcon.FirstOrDefault().Value;
                    if (_objectIcon.ContainsKey(extension))
                    {
                        icon = _objectIcon[extension];
                    }
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Box(icon, GUILayout.Width(DEFAULT_UI_SIZE), GUILayout.Height(DEFAULT_UI_SIZE));
                        if (GUILayout.Button(fileName, EditorStyles.objectField))
                        {
                            var target = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(_missingPropertyObjects[i]);
                            EditorGUIUtility.PingObject(target);
                        }
                    }
                }
            }
        }

        private void OnCheckAt(string path, bool hasMissingProperty)
        {
            if (hasMissingProperty)
            {
                _missingPropertyObjects.Add(path);
            }
        }

        private void OnSuccess()
        {
            var missings = new StringBuilder();
            foreach (var m in _missingPropertyObjects)
            {
                missings.Append($"{m}\n");
            }
        }

        private void OnException(Exception ex)
        {
            Debug.LogException(ex);
        }
    }
}
