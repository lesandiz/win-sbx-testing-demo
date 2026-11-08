# RomanCalc

WPF calculator for Roman numerals (I–MMMCMXCIX / 1–3999): add, subtract, multiply, divide.
Division shows a remainder (`X ÷ III = III r I`); results outside 1–3999 show `Error`.

```
src/RomanCalc.Core         Roman parsing/formatting + calculator logic (no UI)
src/RomanCalc.App          WPF UI (net8.0-windows); every control has an AutomationId
tests/RomanCalc.Core.Tests xUnit tests for the core logic
sandbox/ui-test.ps1        UI test that runs INSIDE Windows Sandbox (via sbx run --test)
sandbox/Run-SandboxTest.ps1 Host script: publish self-contained, then sbx run + ui-test.ps1
```

## Build and unit test

```powershell
dotnet build -c Release
dotnet test -c Release
```

## Sandbox UI test (sandbox-mcp `sbx` CLI)

```powershell
.\sandbox\Run-SandboxTest.ps1              # publish + run in a fresh offline sandbox; exit 0 = pass
.\sandbox\Run-SandboxTest.ps1 -NoPublish   # reuse artifacts\publish
.\sandbox\Run-SandboxTest.ps1 -Keep        # leave sandbox up; then use sbx ui / sbx shot / sbx stop
.\sandbox\Run-SandboxTest.ps1 -Record      # also record a flipbook of the run
```

What happens: `sbx run` boots a clean Windows Sandbox with no network, copies `artifacts\publish` in, launches
`RomanCalc.App.exe`, then runs `ui-test.ps1` in the guest. The script drives buttons through UI Automation
(`Invoke-SbxUi -AutomationId Btn_X -Action invoke`), reads the `Display` text, and exits non-zero on any
mismatch. sbx then checks for crashes, takes screenshots, writes `report.md` + `ui-test-results.json`, and
destroys the sandbox.

The app is published **self-contained** because the sandbox has no .NET Desktop Runtime.
Run folders go to `%SANDBOX_MCP_HOME%` (default `%USERPROFILE%\sandbox-mcp`), outside AppData.
That matters when running from a packaged (MSIX) host such as the Claude desktop app.

### Step-by-step alternative (drive from the host)

```powershell
$sbx = "$env:LOCALAPPDATA\sandbox-mcp\releases\v1.2.0\sbx.cmd"
& $sbx start --network off
& $sbx push artifacts\publish C:\apps\RomanCalc
& $sbx launch C:\apps\RomanCalc\RomanCalc.App.exe --wait 5
& $sbx ui click --id Btn_X; & $sbx ui click --id Btn_Add; & $sbx ui click --id Btn_V; & $sbx ui click --id Btn_Equals
& $sbx ui read --id Display      # -> XV
& $sbx shot --window "Roman Calculator"
& $sbx stop
```
