# IMEE Patch Plan — Stop Self-Inflicted Kills + Safe Production Rollout

**Root defect:** detection granularity (process *name*) is coarser than authorization granularity (process *path*). Every kill path inherits this mismatch. `FindAuthorizedSibling` already normalizes paths correctly — the enforcement side just never verifies the path of what it kills.

**Precondition before applying anything:** grep the IMEE log from 2026-04-30 15:02:21 for either
`Killed background process for` or `Unauthorized launch detected`. That tells you which patch is the actual fix on your line vs. defense-in-depth.

---

## Patch 0 — Path identity helper (prerequisite for 1 & 2)

`Process.MainModule.FileName` throws `Win32Exception` on 32/64-bit mismatch and access-denied. Use `QueryFullProcessImageName` with `PROCESS_QUERY_LIMITED_INFORMATION` — works across bitness and for most protected processes.

Add to `ProcessManager.cs`:

```csharp
[DllImport("kernel32.dll", SetLastError = true)]
private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
private static extern bool QueryFullProcessImageName(
    IntPtr hProcess, uint flags, StringBuilder exeName, ref uint size);

[DllImport("kernel32.dll", SetLastError = true)]
private static extern bool CloseHandle(IntPtr h);

private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

/// <summary>Full image path of a PID, or null if unavailable. Never throws.</summary>
public static string TryGetProcessPath(int pid)
{
    IntPtr h = IntPtr.Zero;
    try
    {
        h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h == IntPtr.Zero) return null;
        var sb = new StringBuilder(1024);
        uint size = (uint)sb.Capacity;
        return QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString() : null;
    }
    catch { return null; }
    finally { if (h != IntPtr.Zero) CloseHandle(h); }
}

/// <summary>True only when the PID's image path provably matches expectedPath.
/// Unknown path => NOT a match (fail-safe: never kill what you can't identify).</summary>
public static bool ProcessPathMatches(int pid, string expectedPath)
{
    string actual = TryGetProcessPath(pid);
    if (string.IsNullOrEmpty(actual) || string.IsNullOrEmpty(expectedPath))
        return false;
    return string.Equals(
        Path.GetFullPath(actual).TrimEnd('\\'),
        Path.GetFullPath(expectedPath).TrimEnd('\\'),
        StringComparison.OrdinalIgnoreCase);
}
```

The fail-safe direction matters: if the path can't be read, **treat it as not yours and don't kill it**. The cost of a false negative is a lingering process; the cost of a false positive is killing another line's server.

---

## Patch 1 — `KillBackgroundProcesses`: only kill what you can prove is yours

In the kill loop, after the `MainWindowHandle == IntPtr.Zero` / `HasAnyTopLevelWindow` checks, add a path gate:

```csharp
int pid = processes[i].Id;

// PATH GATE: never kill a name-match whose image path isn't this app's exe.
if (!ProcessPathMatches(pid, app.Directory))
{
    SimpleLogger.Warn("KillBackgroundProcesses @ ProcessManager.cs",
        $"Skipping same-name process (PID {pid}) — image path does not match '{app.Directory}'");
    continue;
}

// ENFORCEMENT GATE (Patch 5): in LogOnly mode, record and skip.
if (!_settingsService.EnforcementEnabled)
{
    SimpleLogger.Warn("KillBackgroundProcesses @ ProcessManager.cs",
        $"[DRY-RUN] Would kill background process for {app.AppName} (PID {pid})");
    continue;
}
```

Also gate the **unconditional pre-start cleanup** in `StartApplication`:

```csharp
// Only clean up lingering processes for apps NOT flagged as service-style.
int bgKilled = app.TreatAsService ? 0 : KillBackgroundProcesses(app);
```

---

## Patch 2 — Snapshot/unauthorized detection: filter name matches by path

In `GetProcessSnapshot` (and the batch variant), a same-name process at a different path must not count toward this app's snapshot at all:

```csharp
processes = Process.GetProcessesByName(appName);
for (int i = 0; i < processes.Length; i++)
{
    int pid;
    try { pid = processes[i].Id; } catch (InvalidOperationException) { continue; }

    // A same-name process from a different install path is NOT this app.
    // Unknown path counts as a match here (detection side can be permissive;
    // only the KILL side must be strict — Patch 1 enforces that).
    string path = TryGetProcessPath(pid);
    if (path != null && !ProcessPathMatches(pid, app.Directory))
        continue;

    // ...existing window/tray classification...
}
```

Note the asymmetry: **detection permissive, enforcement strict.** If you make detection strict too, an access-denied path read makes IMEE blind to its own app and the watchdog spirals.

With this patch, `HandleUnauthorizedLaunch` stops firing for the other line's same-named exe, because that process no longer appears in this entry's snapshot. `FindAuthorizedSibling` continues to cover the same-path/two-groups case.

---

## Patch 3 — First-class windowless apps

`bool isRunning = snapshot.HasWindowedProcess` structurally can't manage `*Srv`-style processes.

`Models/ManagedApplication.cs`:
```csharp
/// <summary>Service-style app: no window expected. Liveness = process exists.
/// Disables window-based health checks and zombie cleanup.</summary>
public bool TreatAsService { get; set; }   // default false — zero behavior change
```

`Form1.cs`, `UpdateApplicationStatuses`:
```csharp
bool isRunning = app.TreatAsService
    ? (snapshot.HasWindowedProcess || snapshot.HasBackgroundProcess)
    : snapshot.HasWindowedProcess;
```

For `TreatAsService` apps also skip: not-responding checks (`Process.Responding` is meaningless without a message pump), error-dialog detection, and title-change detection. Memory/CPU checks remain valid.

Persist the flag in `StorageService` + expose a checkbox in `AppSettingsDialog`. Set it for LineControlSrv.

**Better long-term:** liveness by tracked PID (`_launchedPids` already exists) instead of name enumeration entirely. Bigger diff — do it after this release.

---

## Patch 4 — Kill attribution (ends the misdiagnosis)

Every IMEE-initiated termination must be distinguishable from a genuine app exit. You already have `_forceKillReason` for health kills — extend it:

- `HandleUnauthorizedLaunch`: before `StopApplication(app)` →
  `_forceKillReason[app.Index] = "Terminated by IMEE: unauthorized/cross-detection";`
- `KillBackgroundProcesses`: raise an event or callback with PID + reason so Form1 can record it.
- Abnormal-termination dialog: if `_forceKillReason` has an entry for the app, show
  `"Terminated by IMEE (<reason>)"` instead of `"may be experiencing: Abnormal termination"`.
- Log line for **every** `Kill()` call: app, PID, image path, reason, initiating method.

This patch has zero behavioral risk and would have saved you this entire debugging session. Ship it even if you ship nothing else.

---

## Patch 5 — Enforcement kill switch (the actual downtime insurance)

Global setting in `SettingsService`:

```csharp
/// <summary>false = LogOnly: watchdog logs "[DRY-RUN] Would kill ..." but never terminates.</summary>
public bool EnforcementEnabled { get; set; }   // ship as FALSE
```

Gate **every** `Kill()` / `StopApplication()` initiated by the watchdog (unauthorized, background cleanup, not-responding, error-dialog, title-change, memory, CPU) behind it, each logging its dry-run line. User-clicked Stop stays ungated.

---

## Rollout — how you get "no downtime" in practice

You cannot *guarantee* zero downtime with software that terminates processes; you can make enforcement earn its way back on with evidence.

1. **Confirm the kill path** from the 30/04 log before deploying anything.
2. **Deploy with `EnforcementEnabled = false`** (LogOnly) during a planned line stop or shift change — IMEE is a portable WinForms exe; swap is seconds, keep the old exe beside it as rollback.
3. **Soak 3–5 production days.** Success gate: zero `[DRY-RUN] Would kill` lines for healthy apps. Every dry-run line is a kill the old build would have executed.
4. **Flag LineControlSrv (and any other `*Srv`) as `TreatAsService`** during the soak.
5. **Enable enforcement on one line first** (SMTLine7), watch one shift, then roll out.
6. Rollback = flip `EnforcementEnabled` off (no redeploy) or swap back the old exe.

## Test matrix before deploy

| Scenario | Expected after patch |
|---|---|
| Start LineControlSrv via IMEE, never shows window | Runs indefinitely; status "Running" via TreatAsService |
| Same-named exe at different path running (other line) | Not in snapshot; no unauthorized kill; log notes path skip |
| Same exe registered in two groups, same path | Sibling check authorizes; no kill (existing behavior) |
| True zombie: leftover windowless PID at *matching* path, not TreatAsService | Killed pre-start, with attributed log line |
| Path read access-denied on foreign process | Detection counts it, enforcement refuses to kill, warn logged |
| LogOnly mode, app freezes (not responding) | `[DRY-RUN]` line only; nothing killed |
