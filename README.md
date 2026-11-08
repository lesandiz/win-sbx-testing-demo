# Windows UI testing in Windows Sandbox with `sbx`

> **The calculator is not the point of this project.**
> This repository is a working example of **testing a Windows desktop UI inside the native Windows Sandbox**
> using the **[sandbox-mcp](https://shanselman.github.io/sandbox-mcp/) `sbx` CLI**.
> RomanCalc is a small sample app to test: a WPF Roman numeral calculator, chosen because it is
> self-contained and easy to check, and also because it gives me nostalgic vibes ;-)
> Swap in your own app and the same approach applies.

## What this demonstrates

- Booting a **fresh, disposable Windows Sandbox** for each test run: clean machine, **network off**.
- Copying a published app in and launching it on a real Windows desktop.
- Driving the UI through **UI Automation** from inside the guest: clicking buttons and reading text.
- Failing the run on wrong results, crashes or a missing window, with screenshots and a report as evidence.
- Destroying the sandbox afterwards, so nothing is left on the host.

All of this is done with the **`sbx` command-line tool**. No MCP client, test framework or test agent is
installed in the app or on the guest.

## Required dependency: sandbox-mcp (`sbx` CLI)

| | |
|---|---|
| **Project** | **https://shanselman.github.io/sandbox-mcp/** (source: [shanselman/sandbox-mcp](https://github.com/shanselman/sandbox-mcp)) |
| **Version used** | v1.2.0 (portable release, includes its own Node.js runtime) |
| **Used for** | `sbx run --test`: boots the sandbox, deploys, launches, runs the guest test script, reports |
| **MCP adapter** | Not required. This project only uses the CLI. |

Host requirements, as stated by sandbox-mcp:
- Windows 11 24H2 (build 26100) or newer, x64
- Pro, Enterprise or Education edition
- Virtualization enabled
- The **Windows Sandbox** optional feature enabled
- Windows Sandbox opened once from Start, so that the `wsb` CLI is installed

Install `sbx` by following the
[agent setup guide](https://github.com/shanselman/sandbox-mcp/blob/main/docs/agent-setup.md).
Then check the machine is ready:

```powershell
& "$env:LOCALAPPDATA\sandbox-mcp\releases\v1.2.0\sbx.cmd" doctor
```

To build the sample app you also need the **.NET 10 SDK**. The sandbox itself needs no .NET: the app is
published self-contained.

## Run the sandbox UI test

```powershell
.\sandbox\Run-SandboxTest.ps1              # publish + test in a fresh offline sandbox; exit 0 = pass
.\sandbox\Run-SandboxTest.ps1 -NoPublish   # reuse artifacts\publish
.\sandbox\Run-SandboxTest.ps1 -Keep        # leave the sandbox running; inspect with sbx ui / sbx shot, then sbx stop
.\sandbox\Run-SandboxTest.ps1 -Record      # also record a flipbook of the run
```

Typical output:

```
[sbx] booting a fresh Windows Sandbox (network off)
[sbx] copying ...\artifacts\publish to C:\apps\RomanCalc.App
[sbx] running RomanCalc.App.exe
[sbx] running test ui-test.ps1
[sbx] destroying the sandbox
PASS  RomanCalc.App.exe on a fresh Windows Sandbox (58.1s)
  process  still running, window "Roman Calculator"
  crashes  none
  test     ui-test.ps1 passed
  report   C:\Users\<you>\sandbox-mcp\runs\<timestamp>\out\report.md
```

Each run leaves `report.md`, screenshots and `ui-test-results.json` in its run folder.

## How it works

```
host                                         Windows Sandbox (fresh, network off)
────                                         ───────────────────────────────────
Run-SandboxTest.ps1
  dotnet publish (self-contained)
  sbx run RomanCalc.App.exe ──────────────►  app copied to C:\apps\RomanCalc.App, launched
        --network off                        ui-test.ps1 runs in the guest:
        --test sandbox\ui-test.ps1             Invoke-SbxUi -AutomationId Btn_X -Action invoke
                                               Invoke-SbxUi -AutomationId Display -Action read
                                               exit 1 on any mismatch
  ◄────────────── report, screenshots, results JSON; crash check; sandbox destroyed
  exit code 0 = PASS
```

- [sandbox/Run-SandboxTest.ps1](sandbox/Run-SandboxTest.ps1) runs on the host. It publishes the app, then
  calls `sbx run ... --test`.
- [sandbox/ui-test.ps1](sandbox/ui-test.ps1) runs inside the guest under Windows PowerShell 5.1. sbx gives it
  `$env:SBX_APP_PID` and `$env:SBX_OUT` and pre-imports `SbxUi.psm1`, which provides `Invoke-SbxUi`,
  `Wait-SbxUi` and `Get-SbxUiTree`. The script runs 8 calculations through the UI and checks each result.
- The app sets `AutomationProperties.AutomationId` on every control, so the test has stable names to target.
  Legacy apps without them can still be tested by Name and ControlType. Use `sbx ui tree` to see what an app
  exposes.

### Step-by-step alternative: drive the sandbox from the host

```powershell
$sbx = "$env:LOCALAPPDATA\sandbox-mcp\releases\v1.2.0\sbx.cmd"
& $sbx start --network off
& $sbx push artifacts\publish C:\apps\RomanCalc
& $sbx launch C:\apps\RomanCalc\RomanCalc.App.exe --wait 5
& $sbx ui tree                                   # see every control, name and AutomationId
& $sbx ui click --id Btn_X; & $sbx ui click --id Btn_Add; & $sbx ui click --id Btn_V; & $sbx ui click --id Btn_Equals
& $sbx ui read --id Display                      # -> XV
& $sbx shot --window "Roman Calculator"
& $sbx stop
```

### Note: running from a packaged (MSIX) host

If `sbx` is started from a packaged app, such as the Claude desktop app, writes under `%LOCALAPPDATA%` are
redirected into the package's private folder. `wsb.exe` cannot see them, and `wsb share` fails with
`0x80070002`. `Run-SandboxTest.ps1` therefore defaults `SANDBOX_MCP_HOME` to `%USERPROFILE%\sandbox-mcp`.

## The sample app (RomanCalc)

A minimal WPF calculator for Roman numerals from I to MMMCMXCIX (1–3999), with add, subtract, multiply and
divide. Division shows a remainder (`X ÷ III = III r I`). Results outside 1–3999 show `Error`.

```
src/RomanCalc.Core           Roman numeral parsing/formatting and calculator logic (no UI)
src/RomanCalc.App            WPF UI (net10.0-windows), AutomationIds on every control
tests/RomanCalc.Core.Tests   xUnit unit tests for the core logic
sandbox/ui-test.ps1          UI test that runs inside Windows Sandbox
sandbox/Run-SandboxTest.ps1  Host script: publish, then sbx run --test
```

```powershell
dotnet build -c Release
dotnet test -c Release        # unit tests (host only, no sandbox)
```
