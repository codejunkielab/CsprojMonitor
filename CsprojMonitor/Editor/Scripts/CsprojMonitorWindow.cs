using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CodeJunkie.CsprojMonitor {
  /// <summary>
  /// Unity Editor window for monitoring and building .NET projects with dotnet watch
  /// </summary>
  public class CsprojMonitorWindow : EditorWindow {
    /// <summary>
    /// Configuration for a single project to monitor
    /// </summary>
    [System.Serializable]
    public class ProjectConfig {
      public string projectName = "";
      public string csprojPath = "";
      public bool isWatching = false;
      public bool autoStartOnLoad = true;
      [System.NonSerialized]
      public Process watchProcess;
    }

    #region Constants
    private const string _prefsKey = "CsprojMonitor_ProjectConfigs";
    private const string _autoStartKey = "CsprojMonitor_GlobalAutoStart";
    private const string _debugLogsKey = "CsprojMonitor_EnableDebugLogs";
    private const string _warningLogsKey = "CsprojMonitor_EnableWarningLogs";
    private const string _errorLogsKey = "CsprojMonitor_EnableErrorLogs";
    #endregion

    #region Private Fields
    private List<ProjectConfig> _projectConfigs = new List<ProjectConfig>();
    private Vector2 _scrollPosition;
    private bool _showAddProject = false;
    private string _newProjectName = "";
    private string _newCsprojPath = "";
    private bool _globalAutoStart = true;
    private bool _enableDebugLogs = true;
    private bool _enableWarningLogs = true;
    private bool _enableErrorLogs = true;
    #endregion

    #region Properties
    /// <summary>
    /// Path to the project-specific settings file
    /// </summary>
    private static string ProjectSettingsPath => Path.Combine(Application.dataPath, "..", "ProjectSettings", "CsprojMonitorSettings.json");

    /// <summary>
    /// Directory containing the project settings
    /// </summary>
    private static string ProjectSettingsDir => Path.GetDirectoryName(ProjectSettingsPath);
    #endregion

    #region Menu Item
    [MenuItem("Tools/CodeJunkie/Csproj Monitor")]
    public static void ShowWindow() {
      GetWindow<CsprojMonitorWindow>("Csproj Monitor");
    }
    #endregion

    #region Unity Lifecycle
    private void OnEnable() {
      LoadProjectConfigs();
      EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
      EditorApplication.quitting += OnEditorQuitting;
      EditorApplication.delayCall += AutoStartWatchingOnLoad;
    }

    private void OnDisable() {
      EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
      EditorApplication.quitting -= OnEditorQuitting;
      StopAllWatching();
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state) {
      // Continue watching during play mode changes
    }

    private void OnEditorQuitting() {
      StopAllWatching();
    }
    #endregion

    #region GUI Drawing
    private void OnGUI() {
      EditorGUILayout.LabelField("Csproj Project Monitor", EditorStyles.boldLabel);
      EditorGUILayout.Space();

      _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

      // Display existing projects
      for (int i = 0; i < _projectConfigs.Count; i++) {
        DrawProjectConfig(i);
        EditorGUILayout.Space();
      }

      EditorGUILayout.EndScrollView();

      EditorGUILayout.Space();

      // Add new project UI
      DrawAddProjectSection();

      EditorGUILayout.Space();

      // Project information display
      DrawProjectInfo();

      EditorGUILayout.Space();

      // Global settings
      DrawGlobalSettings();

      EditorGUILayout.Space();

      // Batch operation buttons
      EditorGUILayout.BeginHorizontal();
      if (GUILayout.Button("Start All Watching")) {
        StartAllWatching();
      }
      if (GUILayout.Button("Stop All Watching")) {
        StopAllWatching();
      }
      EditorGUILayout.EndHorizontal();

      // Save settings when changed
      if (GUI.changed) {
        SaveProjectConfigs();
      }
    }

    private void DrawProjectConfig(int index) {
      var config = _projectConfigs[index];

      EditorGUILayout.BeginVertical("box");

      EditorGUILayout.BeginHorizontal();
      EditorGUILayout.LabelField($"Project: {config.projectName}", EditorStyles.boldLabel);

      // Display watching status
      var statusColor = config.isWatching ? Color.green : Color.gray;
      var originalColor = GUI.color;
      GUI.color = statusColor;
      EditorGUILayout.LabelField(config.isWatching ? "●" : "○", GUILayout.Width(20));
      GUI.color = originalColor;

      EditorGUILayout.EndHorizontal();

      // Project configuration
      EditorGUI.BeginChangeCheck();
      config.projectName = EditorGUILayout.TextField("Name", config.projectName);

      EditorGUILayout.BeginHorizontal();
      config.csprojPath = EditorGUILayout.TextField("Csproj Path", config.csprojPath);
      if (GUILayout.Button("Browse", GUILayout.Width(60))) {
        string path = EditorUtility.OpenFilePanel("Select .csproj file", "", "csproj");
        if (!string.IsNullOrEmpty(path)) {
          config.csprojPath = path;
        }
      }
      EditorGUILayout.EndHorizontal();

      // Auto start configuration
      config.autoStartOnLoad = EditorGUILayout.Toggle("Auto Start on Unity Load", config.autoStartOnLoad);

      // Display project information
      if (!string.IsNullOrEmpty(config.csprojPath) && File.Exists(config.csprojPath)) {
        string watchCommand = GetWatchCommand(config.csprojPath);
        EditorGUILayout.LabelField($"Watch Command: dotnet watch {watchCommand}", EditorStyles.miniLabel);
      }

      if (EditorGUI.EndChangeCheck()) {
        SaveProjectConfigs();
      }

      // Operation buttons
      EditorGUILayout.BeginHorizontal();

      if (!config.isWatching) {
        if (GUILayout.Button("Start Watching")) {
          StartWatching(config);
        }
      } else {
        if (GUILayout.Button("Stop Watching")) {
          StopWatching(config);
        }
      }

      if (GUILayout.Button("Build Once")) {
        BuildProject(config);
      }

      GUI.color = Color.red;
      if (GUILayout.Button("Remove", GUILayout.Width(60))) {
        if (EditorUtility.DisplayDialog("Remove Project",
            $"Remove '{config.projectName}' from monitoring?", "Yes", "No")) {
          StopWatching(config);
          _projectConfigs.RemoveAt(index);
          SaveProjectConfigs();
          return;
        }
      }
      GUI.color = originalColor;

      EditorGUILayout.EndHorizontal();

      // Path existence check
      if (!string.IsNullOrEmpty(config.csprojPath) && !File.Exists(config.csprojPath)) {
        EditorGUILayout.HelpBox("Csproj file not found!", MessageType.Warning);
      }

      EditorGUILayout.EndVertical();
    }

    private void DrawAddProjectSection() {
      EditorGUILayout.BeginVertical("box");

      _showAddProject = EditorGUILayout.Foldout(_showAddProject, "Add New Project");

      if (_showAddProject) {
        _newProjectName = EditorGUILayout.TextField("Project Name", _newProjectName);

        EditorGUILayout.BeginHorizontal();
        _newCsprojPath = EditorGUILayout.TextField("Csproj Path", _newCsprojPath);
        if (GUILayout.Button("Browse", GUILayout.Width(60))) {
          string path = EditorUtility.OpenFilePanel("Select .csproj file", "", "csproj");
          if (!string.IsNullOrEmpty(path)) {
            _newCsprojPath = path;
            if (string.IsNullOrEmpty(_newProjectName)) {
              _newProjectName = Path.GetFileNameWithoutExtension(path);
            }
          }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUI.enabled = !string.IsNullOrEmpty(_newProjectName) && !string.IsNullOrEmpty(_newCsprojPath);
        if (GUILayout.Button("Add Project")) {
          _projectConfigs.Add(new ProjectConfig {
            projectName = _newProjectName,
            csprojPath = _newCsprojPath,
            autoStartOnLoad = true
          });

          _newProjectName = "";
          _newCsprojPath = "";
          _showAddProject = false;
          SaveProjectConfigs();
        }
        GUI.enabled = true;

        if (GUILayout.Button("Cancel")) {
          _newProjectName = "";
          _newCsprojPath = "";
          _showAddProject = false;
        }
        EditorGUILayout.EndHorizontal();
      }

      EditorGUILayout.EndVertical();
    }

    private void DrawProjectInfo() {
      EditorGUILayout.BeginVertical("box");
      EditorGUILayout.LabelField("Project Information", EditorStyles.boldLabel);

      EditorGUILayout.LabelField($"Project: {Application.productName}", EditorStyles.miniLabel);
      EditorGUILayout.LabelField($"Settings: {ProjectSettingsPath}", EditorStyles.miniLabel);

      EditorGUILayout.BeginHorizontal();
      if (GUILayout.Button("Open Settings Folder", GUILayout.Width(150))) {
        EditorUtility.RevealInFinder(ProjectSettingsPath);
      }

      if (GUILayout.Button("Reset Settings", GUILayout.Width(100))) {
        if (EditorUtility.DisplayDialog("Reset Settings",
            "Are you sure you want to reset all Csproj Monitor settings for this project?", "Yes", "No")) {
          ResetSettings();
        }
      }
      EditorGUILayout.EndHorizontal();

      EditorGUILayout.EndVertical();
    }

    private void DrawGlobalSettings() {
      EditorGUILayout.BeginVertical("box");
      EditorGUILayout.LabelField("Global Settings", EditorStyles.boldLabel);

      EditorGUI.BeginChangeCheck();
      _globalAutoStart = EditorGUILayout.Toggle("Enable Auto Start on Unity Load", _globalAutoStart);
      if (EditorGUI.EndChangeCheck()) {
        EditorPrefs.SetBool(_autoStartKey, _globalAutoStart);
      }

      if (_globalAutoStart) {
        EditorGUILayout.HelpBox("Projects with 'Auto Start on Unity Load' enabled will automatically start watching when Unity loads.", MessageType.Info);
      }

      EditorGUILayout.Space();

      // Log level settings
      EditorGUILayout.LabelField("Log Level Settings", EditorStyles.boldLabel);

      EditorGUI.BeginChangeCheck();
      _enableDebugLogs = EditorGUILayout.Toggle("Enable Debug Logs", _enableDebugLogs);
      _enableWarningLogs = EditorGUILayout.Toggle("Enable Warning Logs", _enableWarningLogs);
      _enableErrorLogs = EditorGUILayout.Toggle("Enable Error Logs", _enableErrorLogs);

      if (EditorGUI.EndChangeCheck()) {
        SaveLogSettings();
      }

      // Log settings description
      if (!_enableDebugLogs || !_enableWarningLogs || !_enableErrorLogs) {
        var disabledTypes = new List<string>();
        if (!_enableDebugLogs) disabledTypes.Add("Debug");
        if (!_enableWarningLogs) disabledTypes.Add("Warning");
        if (!_enableErrorLogs) disabledTypes.Add("Error");

        EditorGUILayout.HelpBox($"Disabled log types: {string.Join(", ", disabledTypes)}", MessageType.Info);
      }

      EditorGUILayout.EndVertical();
    }
    #endregion

    #region Project Operations
    private void AutoStartWatchingOnLoad() {
      if (!_globalAutoStart) return;

      foreach (var config in _projectConfigs) {
        if (config.autoStartOnLoad && !config.isWatching &&
            !string.IsNullOrEmpty(config.csprojPath) && File.Exists(config.csprojPath)) {
          StartWatching(config);
        }
      }

      if (_projectConfigs.Any(c => c.autoStartOnLoad && c.isWatching)) {
        if (_enableDebugLogs) {
          UnityEngine.Debug.Log("[Csproj Monitor] Auto-started watching enabled projects.");
        }
      }
    }

    private void StartWatching(ProjectConfig config) {
      if (string.IsNullOrEmpty(config.csprojPath) || !File.Exists(config.csprojPath)) {
        EditorUtility.DisplayDialog("Error", "Invalid csproj path!", "OK");
        return;
      }

      string dotnetPath = GetDotNetPath();
      if (string.IsNullOrEmpty(dotnetPath)) {
        EditorUtility.DisplayDialog("Error", "dotnet command not found! Please ensure .NET CLI is installed and accessible.", "OK");
        return;
      }

      // Determine project type and select appropriate command
      string watchCommand = GetWatchCommand(config.csprojPath);

      try {
        var startInfo = new ProcessStartInfo {
          FileName = dotnetPath,
          Arguments = $"watch --project \"{config.csprojPath}\" {watchCommand}",
          UseShellExecute = false,
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          CreateNoWindow = true,
          WorkingDirectory = Path.GetDirectoryName(config.csprojPath)
        };

        // Explicitly setup environment variables
        SetupEnvironmentVariables(startInfo);

        config.watchProcess = new Process { StartInfo = startInfo };

        config.watchProcess.OutputDataReceived += (sender, e) => {
          if (!string.IsNullOrEmpty(e.Data)) {
            LogWithAppropriateLevel(config.projectName, e.Data);
          }
        };

        config.watchProcess.ErrorDataReceived += (sender, e) => {
          if (!string.IsNullOrEmpty(e.Data)) {
            LogWithAppropriateLevel(config.projectName, e.Data);
          }
        };

        config.watchProcess.Start();
        config.watchProcess.BeginOutputReadLine();
        config.watchProcess.BeginErrorReadLine();

        config.isWatching = true;

        if (_enableDebugLogs) {
          UnityEngine.Debug.Log($"Started watching: {config.projectName}");
        }
      } catch (Exception e) {
        EditorUtility.DisplayDialog("Error", $"Failed to start dotnet watch: {e.Message}", "OK");
      }
    }

    private void StopWatching(ProjectConfig config) {
      if (config.watchProcess != null && !config.watchProcess.HasExited) {
        try {
          config.watchProcess.Kill();
          config.watchProcess.Dispose();
        } catch (Exception e) {
          UnityEngine.Debug.LogWarning($"Error stopping watch process: {e.Message}");
        }
      }

      config.watchProcess = null;
      config.isWatching = false;

      if (_enableDebugLogs) {
        UnityEngine.Debug.Log($"Stopped watching: {config.projectName}");
      }
    }

    private void BuildProject(ProjectConfig config) {
      if (string.IsNullOrEmpty(config.csprojPath) || !File.Exists(config.csprojPath)) {
        EditorUtility.DisplayDialog("Error", "Invalid csproj path!", "OK");
        return;
      }

      string dotnetPath = GetDotNetPath();
      if (string.IsNullOrEmpty(dotnetPath)) {
        EditorUtility.DisplayDialog("Error", "dotnet command not found! Please ensure .NET CLI is installed and accessible.", "OK");
        return;
      }

      try {
        var startInfo = new ProcessStartInfo {
          FileName = dotnetPath,
          Arguments = $"build \"{config.csprojPath}\"",
          UseShellExecute = false,
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          CreateNoWindow = true,
          WorkingDirectory = Path.GetDirectoryName(config.csprojPath)
        };

        // Explicitly setup environment variables
        SetupEnvironmentVariables(startInfo);

        var process = Process.Start(startInfo);
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode == 0) {
          LogBuildOutput(config.projectName, output, false);
          AssetDatabase.Refresh();
        } else {
          LogBuildOutput(config.projectName, error, true);
        }
      } catch (Exception e) {
        EditorUtility.DisplayDialog("Error", $"Failed to build project: {e.Message}", "OK");
      }
    }

    private void StartAllWatching() {
      foreach (var config in _projectConfigs) {
        if (!config.isWatching) {
          StartWatching(config);
        }
      }
    }

    private void StopAllWatching() {
      foreach (var config in _projectConfigs) {
        if (config.isWatching) {
          StopWatching(config);
        }
      }
    }
    #endregion

    #region Logging
    private void LogWithAppropriateLevel(string projectName, string message) {
      if (string.IsNullOrEmpty(message)) return;

      // Check for error patterns
      if (IsErrorMessage(message)) {
        if (_enableErrorLogs) {
          UnityEngine.Debug.LogError($"[{projectName}] {message}");
        }
      }
      // Check for warning patterns
      else if (IsWarningMessage(message)) {
        if (_enableWarningLogs) {
          UnityEngine.Debug.LogWarning($"[{projectName}] {message}");
        }
      }
      // Regular log
      else {
        if (_enableDebugLogs) {
          UnityEngine.Debug.Log($"[{projectName}] {message}");
        }
      }
    }

    private void LogBuildOutput(string projectName, string output, bool isError) {
      if (string.IsNullOrEmpty(output)) return;

      // Process each line individually
      var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

      foreach (var line in lines) {
        if (string.IsNullOrWhiteSpace(line)) continue;

        if (isError || IsErrorMessage(line)) {
          if (_enableErrorLogs) {
            UnityEngine.Debug.LogError($"[{projectName}] {line}");
          }
        } else if (IsWarningMessage(line)) {
          if (_enableWarningLogs) {
            UnityEngine.Debug.LogWarning($"[{projectName}] {line}");
          }
        } else {
          if (_enableDebugLogs) {
            UnityEngine.Debug.Log($"[{projectName}] {line}");
          }
        }
      }
    }

    private bool IsErrorMessage(string message) {
      if (string.IsNullOrEmpty(message)) return false;

      var lowerMessage = message.ToLower();

      // Keywords indicating errors
      var errorKeywords = new[] {
        "error cs",        // C# compiler errors
        "error msbuild",   // MSBuild errors
        "error:",          // General error format
        "build failed",    // Build failure
        "compilation failed",
        "fatal error",
        "exception:",
        "unhandled exception",
        "could not execute",
        "failed to compile",
        ": error ",        // MSBuild error format
        "compiler error",
        "syntax error",
        "reference error",
        "assembly could not be found",
        "type or namespace name",
        "does not exist in the current context"
      };

      return errorKeywords.Any(keyword => lowerMessage.Contains(keyword));
    }

    private bool IsWarningMessage(string message) {
      if (string.IsNullOrEmpty(message)) return false;

      var lowerMessage = message.ToLower();

      // Keywords indicating warnings
      var warningKeywords = new[] {
        "warning cs",      // C# compiler warnings
        "warning msbuild", // MSBuild warnings
        "warning:",        // General warning format
        ": warning ",      // MSBuild warning format
        "deprecated",
        "obsolete",
        "unreachable code",
        "unused variable",
        "unused using directive",
        "possible null reference",
        "assignment to same variable",
        "field is never assigned",
        "event is never used",
        "variable is assigned but never used",
        "comparison made to same variable"
      };

      return warningKeywords.Any(keyword => lowerMessage.Contains(keyword));
    }
    #endregion

    #region Utility Methods
    private string GetWatchCommand(string csprojPath) {
      try {
        string content = File.ReadAllText(csprojPath);

        // Use build command if OutputType is Library
        if (content.Contains("<OutputType>Library</OutputType>") ||
            content.Contains("<OutputType>library</OutputType>")) {
          return "build";
        }

        // Use run command if OutputType is Exe
        if (content.Contains("<OutputType>Exe</OutputType>") ||
            content.Contains("<OutputType>exe</OutputType>")) {
          return "run";
        }

        // For web projects
        if (content.Contains("Microsoft.NET.Sdk.Web")) {
          return "run";
        }

        // Default to build (assume library project)
        return "build";
      } catch (Exception e) {
        UnityEngine.Debug.LogWarning($"Failed to analyze project file: {e.Message}. Using 'build' command.");
        return "build";
      }
    }

    private string GetDotNetPath() {
      // Check common dotnet locations
      string[] commonPaths = {
        "/usr/local/share/dotnet/dotnet",
        "/usr/local/bin/dotnet",
        "/opt/homebrew/bin/dotnet",  // For Apple Silicon Mac
        "/usr/bin/dotnet"
      };

      foreach (string path in commonPaths) {
        if (File.Exists(path)) {
          return path;
        }
      }

      // Search from PATH environment variable
      try {
        var process = Process.Start(new ProcessStartInfo {
          FileName = "/bin/bash",
          Arguments = "-c \"which dotnet\"",
          UseShellExecute = false,
          RedirectStandardOutput = true,
          CreateNoWindow = true
        });

        string output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();

        if (process.ExitCode == 0 && !string.IsNullOrEmpty(output) && File.Exists(output)) {
          return output;
        }
      } catch (Exception e) {
        UnityEngine.Debug.LogWarning($"Failed to find dotnet path: {e.Message}");
      }

      return null;
    }

    private void SetupEnvironmentVariables(ProcessStartInfo startInfo) {
      // Setup important environment variables
      var envVars = new Dictionary<string, string> {
        ["HOME"] = Environment.GetEnvironmentVariable("HOME") ?? "",
        ["USER"] = Environment.GetEnvironmentVariable("USER") ?? "",
        ["LOGNAME"] = Environment.GetEnvironmentVariable("LOGNAME") ?? "",
        ["SHELL"] = Environment.GetEnvironmentVariable("SHELL") ?? "/bin/bash",
        ["TMPDIR"] = Environment.GetEnvironmentVariable("TMPDIR") ?? "/tmp",
        ["DOTNET_ROOT"] = Environment.GetEnvironmentVariable("DOTNET_ROOT") ?? "/usr/local/share/dotnet"
      };

      // Build PATH environment variable
      var pathBuilder = new List<string>();

      // Add existing PATH
      string existingPath = Environment.GetEnvironmentVariable("PATH");
      if (!string.IsNullOrEmpty(existingPath)) {
        pathBuilder.Add(existingPath);
      }

      // Add common dotnet locations to PATH
      pathBuilder.AddRange(new[] {
        "/usr/local/share/dotnet",
        "/usr/local/bin",
        "/opt/homebrew/bin",
        "/usr/bin",
        "/bin"
      });

      envVars["PATH"] = string.Join(":", pathBuilder);

      // Set environment variables
      foreach (var kvp in envVars) {
        if (startInfo.Environment.ContainsKey(kvp.Key)) {
          startInfo.Environment[kvp.Key] = kvp.Value;
        } else {
          startInfo.Environment.Add(kvp.Key, kvp.Value);
        }
      }
    }
    #endregion

    #region Settings Management
    private void ResetSettings() {
      try {
        if (File.Exists(ProjectSettingsPath)) {
          File.Delete(ProjectSettingsPath);
        }

        SetDefaultSettings();
        SaveProjectConfigs();

        if (_enableDebugLogs) {
          UnityEngine.Debug.Log("[Csproj Monitor] Settings reset to defaults");
        }
      } catch (Exception e) {
        UnityEngine.Debug.LogError($"Failed to reset settings: {e.Message}");
      }
    }

    private void SaveProjectConfigs() {
      try {
        var settings = new ProjectSettings {
          ProjectConfigs = _projectConfigs,
          GlobalAutoStart = _globalAutoStart,
          EnableDebugLogs = _enableDebugLogs,
          EnableWarningLogs = _enableWarningLogs,
          EnableErrorLogs = _enableErrorLogs
        };

        var json = JsonUtility.ToJson(settings, true);

        // Create ProjectSettings directory if it doesn't exist
        if (!Directory.Exists(ProjectSettingsDir)) {
          Directory.CreateDirectory(ProjectSettingsDir);
        }

        File.WriteAllText(ProjectSettingsPath, json);

        if (_enableDebugLogs) {
          UnityEngine.Debug.Log($"[Csproj Monitor] Settings saved to: {ProjectSettingsPath}");
        }
      } catch (Exception e) {
        UnityEngine.Debug.LogError($"Failed to save project configs: {e.Message}");
      }
    }

    private void SaveLogSettings() {
      // Saved together with project settings, no separate save needed
      SaveProjectConfigs();
    }

    private void LoadProjectConfigs() {
      try {
        if (File.Exists(ProjectSettingsPath)) {
          // Load from project-specific settings file
          string json = File.ReadAllText(ProjectSettingsPath);
          if (!string.IsNullOrEmpty(json)) {
            var settings = JsonUtility.FromJson<ProjectSettings>(json);
            if (settings != null) {
              _projectConfigs = settings.ProjectConfigs ?? new List<ProjectConfig>();
              _globalAutoStart = settings.GlobalAutoStart;
              _enableDebugLogs = settings.EnableDebugLogs;
              _enableWarningLogs = settings.EnableWarningLogs;
              _enableErrorLogs = settings.EnableErrorLogs;

              if (_enableDebugLogs) {
                UnityEngine.Debug.Log($"[Csproj Monitor] Settings loaded from: {ProjectSettingsPath}");
              }
              return;
            }
          }
        }

        // Set default values if settings file doesn't exist
        SetDefaultSettings();

        // Try to migrate from old settings (once only)
        TryMigrateFromEditorPrefs();

      } catch (Exception e) {
        UnityEngine.Debug.LogError($"Failed to load project configs: {e.Message}");
        SetDefaultSettings();
      }
    }

    private void SetDefaultSettings() {
      _projectConfigs = new List<ProjectConfig>();
      _globalAutoStart = true;
      _enableDebugLogs = true;
      _enableWarningLogs = true;
      _enableErrorLogs = true;
    }

    private void TryMigrateFromEditorPrefs() {
      try {
        // Migrate if old settings exist
        string oldJson = EditorPrefs.GetString(_prefsKey, "");
        if (!string.IsNullOrEmpty(oldJson)) {
          var wrapper = JsonUtility.FromJson<SerializableList<ProjectConfig>>(oldJson);
          if (wrapper?.items != null && wrapper.items.Count > 0) {
            _projectConfigs = wrapper.items;
            _globalAutoStart = EditorPrefs.GetBool(_autoStartKey, true);
            _enableDebugLogs = EditorPrefs.GetBool(_debugLogsKey, true);
            _enableWarningLogs = EditorPrefs.GetBool(_warningLogsKey, true);
            _enableErrorLogs = EditorPrefs.GetBool(_errorLogsKey, true);

            // Save in new format
            SaveProjectConfigs();

            // Delete old settings
            EditorPrefs.DeleteKey(_prefsKey);
            EditorPrefs.DeleteKey(_autoStartKey);
            EditorPrefs.DeleteKey(_debugLogsKey);
            EditorPrefs.DeleteKey(_warningLogsKey);
            EditorPrefs.DeleteKey(_errorLogsKey);

            if (_enableDebugLogs) {
              UnityEngine.Debug.Log("[Csproj Monitor] Settings migrated from EditorPrefs to project settings");
            }
          }
        }
      } catch (Exception e) {
        UnityEngine.Debug.LogWarning($"Failed to migrate from EditorPrefs: {e.Message}");
      }
    }
    #endregion

    #region Serializable Classes
    /// <summary>
    /// Container for all project settings
    /// </summary>
    [System.Serializable]
    private class ProjectSettings {
      public List<ProjectConfig> ProjectConfigs = new List<ProjectConfig>();
      public bool GlobalAutoStart = true;
      public bool EnableDebugLogs = true;
      public bool EnableWarningLogs = true;
      public bool EnableErrorLogs = true;
    }

    /// <summary>
    /// Generic serializable list wrapper for JSON serialization
    /// </summary>
    [System.Serializable]
    private class SerializableList<T> {
      public List<T> items;
    }
    #endregion
  }
}
