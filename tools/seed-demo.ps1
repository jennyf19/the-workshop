#requires -Version 7.0
<#
.SYNOPSIS
    Seeds a throwaway demo dataset (fake names) for the workshop dashboard.

.DESCRIPTION
    The dashboard reads real workshops (a base dir) and real Copilot sessions
    (~/.copilot/session-state), so screenshots would expose real project names.
    This builds an isolated, fake-named dataset under a temp folder and prints
    the two environment variables that point an instance at it:

        WORKSHOP_DIR           -> the workshops base dir
        WORKSHOP_SESSION_ROOT  -> the Copilot session-state root

    Run the app with those set (any spare port) to get a clean board for docs:

        $env:WORKSHOP_DIR = '<...>\ws'
        $env:WORKSHOP_SESSION_ROOT = '<...>\ss'
        dotnet run --project src/WorkshopRoom --urls http://localhost:5199

    Nothing real is touched. Delete the temp folder when done.
#>
param([string]$Base = (Join-Path $env:TEMP 'workshop-demo'))

$ErrorActionPreference = 'Stop'
$ws = Join-Path $Base 'ws'   # workshops base dir  -> WORKSHOP_DIR
$ss = Join-Path $Base 'ss'   # session-state root  -> WORKSHOP_SESSION_ROOT

if (Test-Path $Base) { Remove-Item $Base -Recurse -Force }
New-Item -ItemType Directory -Force -Path $ws, $ss | Out-Null

# --- acme-api: a full (repo-backed) workshop -------------------------------
$acme = Join-Path $ws 'acme-api'
New-Item -ItemType Directory -Force -Path (Join-Path $acme '.git') | Out-Null   # marks it git-backed (not a mini)
foreach ($d in 'reviewer', 'migrator', 'planner') {
    New-Item -ItemType Directory -Force -Path (Join-Path $acme "desks\$d") | Out-Null
}
New-Item -ItemType Directory -Force -Path (Join-Path $acme 'desks\reviewer\.signals') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $acme 'desks\migrator\.signals') | Out-Null
@'
{ "signal_type": "execution", "agent_name": "reviewer",
  "self_assessment": { "confidence": 4, "accuracy": 4, "completeness": 4, "intent": 5 },
  "patterns": { "what_worked": "walked the diff against the auth spec", "what_was_hard": "", "skill_gap": "" } }
'@ | Set-Content (Join-Path $acme 'desks\reviewer\.signals\s1.json') -Encoding utf8
@'
{ "signal_type": "escalation", "agent_name": "migrator",
  "self_assessment": { "confidence": 3, "accuracy": 4, "completeness": 2, "intent": 4 },
  "escalation": { "reason": "two valid config formats - which do we standardize on?", "blocked_on": "a schema decision", "recommendation": "keep YAML, deprecate the .ini path" } }
'@ | Set-Content (Join-Path $acme 'desks\migrator\.signals\s2.json') -Encoding utf8

# --- billing-review: a mini-workshop (local, no repo) ----------------------
$bill = Join-Path $ws 'billing-review'
New-Item -ItemType Directory -Force -Path (Join-Path $bill 'desks') | Out-Null
"# billing-review - a mini-workshop`n`nlocal scratch; graduate to a full workshop later." |
    Set-Content (Join-Path $bill 'workshop.md') -Encoding utf8

# --- fake live desk sessions in session-state ------------------------------
function New-DemoSession($id, $name, $cwd, $model, $firstMsg, $outTokens) {
    $dir = Join-Path $ss $id
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    $created = (Get-Date).ToUniversalTime().AddMinutes(-6).ToString('yyyy-MM-ddTHH:mm:ss.fffZ')
    @"
id: $id
cwd: $cwd
git_root: $cwd
repository: acme/acme-api
host_type: github
branch: main
client_name: github/cli
name: $name
user_named: true
created_at: $created
"@ | Set-Content (Join-Path $dir 'workspace.yaml') -Encoding utf8
    $lines = @(
        '{"type":"session.start","data":{}}',
        ('{"type":"user.message","data":{"content":"' + $firstMsg + '"}}'),
        ('{"type":"assistant.message","data":{"content":"on it","model":"' + $model + '","outputTokens":' + $outTokens + '}}')
    )
    $ev = Join-Path $dir 'events.jsonl'
    Set-Content -Path $ev -Value (($lines -join "`n") + "`n") -Encoding utf8
    (Get-Item $ev).LastWriteTimeUtc = (Get-Date).ToUniversalTime()   # recent -> shows as active
}

New-DemoSession '11111111-1111-1111-1111-111111111111' 'reviewer' $acme 'claude-opus-4.8' 'review the auth PR before we ship' 8200
New-DemoSession '22222222-2222-2222-2222-222222222222' 'migrator' $acme 'gpt-5.6-sol' 'port the config loader to the new schema' 15400

Write-Host ''
Write-Host 'Demo seeded (fake names). Run an isolated instance:' -ForegroundColor Green
Write-Host "  `$env:WORKSHOP_DIR = '$ws'"
Write-Host "  `$env:WORKSHOP_SESSION_ROOT = '$ss'"
Write-Host '  dotnet run --project src/WorkshopRoom --urls http://localhost:5199'
Write-Host ''
Write-Host "empty first-run board: point both env vars at empty folders instead." -ForegroundColor DarkGray
