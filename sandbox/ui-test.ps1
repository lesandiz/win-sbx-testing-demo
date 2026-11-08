# Runs INSIDE Windows Sandbox via:  sbx run <RomanCalc.App.exe> --test sandbox\ui-test.ps1
# sbx provides: $env:SBX_APP_PID, $env:SBX_APP_DIR, $env:SBX_APP_EXE, $env:SBX_OUT, and imports SbxUi.psm1
# (Invoke-SbxUi / Wait-SbxUi / Get-SbxUiTree). Guest shell is Windows PowerShell 5.1.
# Exit code 0 = pass, non-zero = fail (sbx marks the whole run FAIL).

$ErrorActionPreference = 'Stop'
$appPid = [int]$env:SBX_APP_PID
if (-not $appPid) { throw 'SBX_APP_PID not set; run this through "sbx run --test".' }

function Press([string]$id) {
    Invoke-SbxUi -ProcessId $appPid -AutomationId $id -Action invoke -TimeoutSec 10 | Out-Null
}

function Read-Display {
    (Invoke-SbxUi -ProcessId $appPid -AutomationId 'Display' -Action read -TimeoutSec 10)['name']
}

$opIds = @{ '+' = 'Btn_Add'; '-' = 'Btn_Subtract'; '*' = 'Btn_Multiply'; '/' = 'Btn_Divide' }

# Each token is either a Roman numeral (typed letter by letter) or an operator.
function Enter-Expression([string[]]$tokens) {
    Press 'Btn_Clear'
    foreach ($t in $tokens) {
        if ($opIds.ContainsKey($t)) { Press $opIds[$t] }
        else { foreach ($ch in $t.ToCharArray()) { Press "Btn_$ch" } }
    }
    Press 'Btn_Equals'
}

$cases = @(
    @{ Name = 'add';           Tokens = 'XII', '+', 'VIII';          Expect = 'XX' }
    @{ Name = 'subtract';      Tokens = 'L', '-', 'IX';              Expect = 'XLI' }
    @{ Name = 'multiply';      Tokens = 'XII', '*', 'XII';           Expect = 'CXLIV' }
    @{ Name = 'divide';        Tokens = 'C', '/', 'IV';              Expect = 'XXV' }
    @{ Name = 'remainder';     Tokens = 'X', '/', 'III';             Expect = 'III r I' }
    @{ Name = 'chain';         Tokens = 'X', '+', 'V', '-', 'III';   Expect = 'XII' }
    @{ Name = 'large';         Tokens = 'MCM', '+', 'XCIX';          Expect = 'MCMXCIX' }
    @{ Name = 'zero is error'; Tokens = 'V', '-', 'V';               Expect = 'Error' }
)

# Wait for the window and its display before driving it.
Wait-SbxUi -ProcessId $appPid -AutomationId 'Display' -TimeoutSec 30 | Out-Null

$results = foreach ($c in $cases) {
    $actual = $null; $err = $null
    try { Enter-Expression $c.Tokens; $actual = Read-Display } catch { $err = "$_" }
    $pass = (-not $err) -and ($actual -ceq $c.Expect)
    $line = '{0,-4} {1,-14} {2,-24} expected {3,-10} got {4}' -f ($(if ($pass) { 'PASS' } else { 'FAIL' })), $c.Name, ($c.Tokens -join ' '), $c.Expect, $(if ($err) { "ERROR: $err" } else { $actual })
    [Console]::Out.WriteLine($line)   # not Write-Output: that would add the line to $results
    [pscustomobject]@{ name = $c.Name; expression = ($c.Tokens -join ' '); expected = $c.Expect; actual = $actual; error = $err; pass = $pass }
}

# Leave the app showing a final result for the post-test screenshot sbx takes.
Enter-Expression @('MMXXVI', '-', 'I')

$failed = @($results | Where-Object { -not $_.pass })
$summary = [pscustomobject]@{ total = $results.Count; failed = $failed.Count; cases = $results }
$summary | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 (Join-Path $env:SBX_OUT 'ui-test-results.json')

Write-Output ("{0}/{1} UI cases passed" -f ($results.Count - $failed.Count), $results.Count)
if ($failed.Count) { exit 1 }
exit 0
