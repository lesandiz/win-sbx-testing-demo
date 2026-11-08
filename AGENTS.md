# Agent instructions

This repo is an example of **testing a Windows desktop UI inside Windows Sandbox** with the
[sandbox-mcp](https://shanselman.github.io/sandbox-mcp/) `sbx` CLI. RomanCalc, a WPF Roman numeral
calculator, is the sample app under test. Read [README.md](README.md) for the overview.

## Layout

| Path | What | Runs where |
|---|---|---|
| `src/RomanCalc.Core` | Roman numeral parsing/formatting and calculator rules | anywhere |
| `src/RomanCalc.App` | WPF UI (`net10.0-windows`); only UI wiring, no business rules | host / sandbox |
| `tests/RomanCalc.Core.Tests` | xUnit tests for Core | host (`dotnet test`) |
| `sandbox/ui-test.ps1` | UI test script | **inside the sandbox only** |
| `sandbox/Run-SandboxTest.ps1` | publish + `sbx run --test` | host |
| `artifacts/` | published self-contained build (gitignored) | — |

## Prerequisites

- .NET 10 SDK (`dotnet --list-sdks`).
- sbx v1.2.0 at `%LOCALAPPDATA%\sandbox-mcp\releases\v1.2.0\sbx.cmd`. Check the machine with `sbx doctor`.

## Commands

```powershell
dotnet build -c Release
dotnet test -c Release                      # seconds; run this first and often
.\sandbox\Run-SandboxTest.ps1               # publish + sandbox UI test, about 60 s; exit 0 = pass
.\sandbox\Run-SandboxTest.ps1 -NoPublish    # ONLY if the app has not changed since the last publish
.\sandbox\Run-SandboxTest.ps1 -Keep         # leave the sandbox up to investigate; you MUST `sbx stop` afterwards
```

Give the sandbox command a timeout of at least 5 minutes.

## Sandbox rules

- **One sandbox at a time** per machine. Before a run, check `sbx status` or `wsb list --raw`. Never stop a
  sandbox you did not start. Every task must end with `wsb list --raw` showing no environments.
- **`SANDBOX_MCP_HOME` must be outside `%LOCALAPPDATA%`** when you run from a packaged (MSIX) host such as the
  Claude desktop app. That host redirects AppData writes, so `wsb.exe` cannot see them and `wsb share` fails
  with `0x80070002`. `Run-SandboxTest.ps1` defaults it to `%USERPROFILE%\sandbox-mcp`. Set it yourself
  whenever you call `sbx` directly.
- The app **must be published self-contained**: the sandbox has no .NET runtime.
- The sandbox has **no network** (`--network off`). Do not add tests that need the internet.
- Run reports live in `%SANDBOX_MCP_HOME%\runs\<timestamp>\out\`, outside the repo. The path is printed as
  `report` at the end of each run. Because each run creates a new folder outside the working directory, some
  agent hosts ask for permission before reading it. Read the files by their absolute path, or have the user
  allow `%SANDBOX_MCP_HOME%\runs`.

## UI conventions

- Every interactive or readable control has `AutomationProperties.AutomationId`. Existing IDs, which the UI
  test depends on (do not rename them):
  - `Btn_M` `Btn_D` `Btn_C` `Btn_L` `Btn_X` `Btn_V` `Btn_I`: numeral buttons
  - `Btn_Add` `Btn_Subtract` `Btn_Multiply` `Btn_Divide` `Btn_Equals` `Btn_Clear` `Btn_Back`
  - `Display` (current entry/result), `Expression` (pending expression), `Decimal` (decimal hint / error text)
- Any new control gets an AutomationId with the same naming style.
- Values are read through UIA **Name**. For a WPF `TextBlock` that is its text. A new display element that is
  not a TextBlock must expose its value through Name or the Value pattern.
- On error, `Display` shows exactly `Error`, and `Decimal` holds the message.

## Where a test belongs

- **Calculation rules** (parsing, range, operations, remainders, errors): xUnit tests in
  `tests/RomanCalc.Core.Tests`. These are fast and need no sandbox.
- **UI behaviour** (button wiring, display text, chaining, error presentation): `sandbox/ui-test.ps1`.
  Keep it small: each case costs real sandbox time. Do not copy every Core rule into it.

## Editing `sandbox/ui-test.ps1`

- It runs in **Windows PowerShell 5.1 inside the guest**. Do not use PS7-only syntax: `??`, `?:` ternaries,
  `&&`/`||` pipeline chains, `ForEach-Object -Parallel`, `Clean` blocks.
- It cannot run on the host. sbx supplies `$env:SBX_APP_PID`, `$env:SBX_OUT` and `$env:SBX_APP_DIR`, and
  pre-imports `SbxUi.psm1`, which provides `Invoke-SbxUi`, `Wait-SbxUi` and `Get-SbxUiTree`.
- **To add a case**, append to `$cases`:
  ```powershell
  @{ Name = 'my case'; Tokens = 'XL', '+', 'II'; Expect = 'XLII' }
  ```
  - Tokens are numerals (typed letter by letter) or the operators `+ - * /`. Equals is pressed automatically.
  - `Expect` is the exact `Display` text, compared case-sensitively (e.g. `III r I`, `Error`).
- Inside the `foreach` that builds `$results`, log with `[Console]::Out.WriteLine(...)`, **never
  `Write-Output`**: Write-Output adds the line to `$results` and the strings are counted as failed cases.
- The script must `exit 1` on any failure and `exit 0` otherwise. It writes `ui-test-results.json` to
  `$env:SBX_OUT`.

## Fix loop: resolving sandbox test failures

Use this loop when asked to "run the sandbox tests and fix failures".

1. **Run** `.\sandbox\Run-SandboxTest.ps1`. If it exits 0, you are done. Otherwise collect the evidence
   from the printed run folder, in this order:
   1. the console output (`why`, `crashes`, `test` lines)
   2. `ui-test-results.json`: expected vs actual for each case
   3. `report.md`: process state, crashes, dependency notes
   4. `screenshots\*.png`: **open and look at them**, especially `app-after-test`
2. **Classify the failure** before changing anything:

   | Category | Signs | Action |
   |---|---|---|
   | **Environment** | sbx/wsb errors, `0x80070002`, "already running", sandbox never boots, timeout before launch | Fix the setup (see Sandbox rules) or report it. **Do not edit code.** |
   | **App crash / no window** | `crashes` not `none`, process exited, no `Roman Calculator` window | Read `report.md`. Use `-Keep` + `sbx errors` for the event log. Fix the app. |
   | **Wrong value** | a case with `actual` ≠ `expected` | Check whether Core gets it right: find an existing xUnit test for that calculation, or add one. If Core is wrong, fix Core. If Core is right (including when existing Core tests already cover the case and pass), the bug is in the UI layer: `MainWindow.xaml` (button `Tag` values, `Click` handlers, AutomationIds) or `MainWindow.xaml.cs` (state handling). |
   | **Test bug** | selector not found, PS 5.1 syntax error, the test logic itself is wrong | Fix the test, but only if the spec (README/this file) shows the test is wrong. Say so explicitly. |
   | **Stale build** | the failure does not match the current code | You used `-NoPublish` after changing the app. Re-run without it. |

3. **Fix** with the smallest change that addresses the cause, and make sure a test guards it:
   - **Core bug:** add or adjust an xUnit test that fails before the fix and passes after. Iterate with
     `dotnet test`, which takes seconds, not the sandbox.
   - **UI-only bug** (XAML wiring, handlers, display logic): no xUnit test can reach it, because the test project
     references only Core. The sandbox UI test is the regression test. If the failing case is already in
     `ui-test.ps1`, it guards the fix; don't add an xUnit test. If no UI case would have caught the bug, add one
     to `$cases`.
4. **Confirm** with a full `.\sandbox\Run-SandboxTest.ps1` (without `-NoPublish`).
5. To **investigate** a puzzling UI state:
   ```powershell
   $env:SANDBOX_MCP_HOME = "$env:USERPROFILE\sandbox-mcp"
   $sbx = "$env:LOCALAPPDATA\sandbox-mcp\releases\v1.2.0\sbx.cmd"
   .\sandbox\Run-SandboxTest.ps1 -NoPublish -Keep
   & $sbx ui tree                 # controls, names, AutomationIds
   & $sbx ui read --id Display
   & $sbx shot --window "Roman Calculator"
   & $sbx stop                    # always
   ```

### Rules

- **Never delete, skip or loosen a test case or expected value to make a run pass.** If an expectation is
  genuinely wrong, change it explicitly and explain why.
- **Stop after 3 sandbox attempts at the same failure.** Report what you found and what you tried instead of
  continuing.
- Never leave a sandbox running. End with `wsb list --raw` showing no environments.
- When done, report for each failure: its category, root cause, files changed, which test guards the fix
  (xUnit or UI case), and the final test results (`dotnet test` count and sandbox PASS/FAIL with the report path).
- Do not commit `artifacts/` or anything from the run folders.
