# Editor Diagnostic Logs

This folder contains diagnostic logs written by `DebugManager.DiagLog()`.

## Files

- **diagnostic_log.txt** — Main diagnostic log file, automatically cleared on Editor restart

## Usage

### Writing Logs (in code)
```csharp
DebugManager.DiagLog($"[MyComponent] Important info: {data}");
```

### Reading Logs (in Unity Editor)
1. **View in Console:** `Tools → Diagnostics → View Diagnostic Log`
2. **Open in External Editor:** Same menu, then click "Open in External Editor"
3. **Open Folder:** `Tools → Diagnostics → Open Log Folder`
4. **Clear Log:** `Tools → Diagnostics → Clear Diagnostic Log`

### Reading Logs (programmatically)
```csharp
string content = DebugManager.ReadDiagLog();
string path = DebugManager.GetDiagLogPath();
```

## For AI Assistants

When debugging issues:
1. Add `DebugManager.DiagLog()` calls to log important data
2. Run the code to generate logs
3. Use `read_file` on `EditorLogs/diagnostic_log.txt` to analyze
4. No need to ask user to copy logs manually

## Location

- **Editor builds:** `<ProjectRoot>/EditorLogs/diagnostic_log.txt`
- **Standalone builds:** `<PersistentDataPath>/diagnostic_log.txt`
