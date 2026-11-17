# SessionStart hook: keep the Graphify knowledge graph present and current, and
# tell the session it exists so the graphify skill actually gets used.
#
# Runs in the primary checkout AND in linked worktrees (graphify's own git hooks
# skip worktrees, and only refresh a graph that already exists, so on their own
# they never bootstrap one). graphify-out/ is gitignored, so every checkout
# builds its own.
#
#   no graph            -> build in the background (AST-only, no LLM, no API cost)
#   graph built for an
#   older HEAD          -> incremental `graphify update .` in the background
#   graph current       -> nothing to build
#
# Whatever this script writes to stdout is added to the session's context.
# Fails open (exit 0) on any problem: a missing graph must never block a session.
#
#   -Build <dir>  internal: the detached worker that runs the rebuild and then
#                 records the HEAD it was built for in graphify-out/.built-head.

param([string]$Build)

$ErrorActionPreference = 'Stop'
$HEAD_MARKER = '.built-head'

function Find-GraphifyPython {
    $probe = "import importlib.util, sys; sys.exit(0 if importlib.util.find_spec('graphify') else 1)"
    foreach ($name in 'python', 'python3', 'py') {
        $cmd = Get-Command $name -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $cmd) { continue }
        & $cmd.Source -c $probe 2>$null
        if ($LASTEXITCODE -eq 0) { return $cmd.Source }
    }
    return $null
}

if ($Build) {
    try {
        Set-Location -LiteralPath $Build
        $env:PYTHONHASHSEED = '0'   # deterministic Louvain clustering, same as graphify's git hooks
        $python = Find-GraphifyPython
        if (-not $python) { exit 0 }
        $head = (git rev-parse HEAD 2>$null)
        & $python -m graphify update .
        if ($LASTEXITCODE -eq 0 -and $head) {
            Set-Content -LiteralPath (Join-Path 'graphify-out' $HEAD_MARKER) -Value $head -NoNewline
        }
    } catch { Write-Error $_ }
    exit 0
}

try {
    $top = (git rev-parse --show-toplevel 2>$null)
    if (-not $top) { exit 0 }
    $top = (Resolve-Path -LiteralPath $top).Path
    $head = (git -C $top rev-parse HEAD 2>$null)

    $outDir = Join-Path $top 'graphify-out'
    $graph = Join-Path $outDir 'graph.json'
    $hasGraph = Test-Path -LiteralPath $graph
    $builtHead = $null
    $markerPath = Join-Path $outDir $HEAD_MARKER
    if (Test-Path -LiteralPath $markerPath) { $builtHead = (Get-Content -LiteralPath $markerPath -Raw).Trim() }

    $python = Find-GraphifyPython
    if (-not $python) {
        if ($hasGraph) {
            Write-Output 'Graphify: graphify-out/graph.json exists but the graphify Python package was not found, so it cannot be refreshed or queried. Install it with `pip install graphifyy`.'
        }
        exit 0
    }

    $needsBuild = (-not $hasGraph) -or ($head -and $builtHead -ne $head)
    if ($needsBuild) {
        $log = Join-Path ([System.IO.Path]::GetTempPath()) "graphify-session-$((Split-Path $top -Leaf)).log"
        $pwsh = (Get-Process -Id $PID).Path
        Start-Process -FilePath $pwsh `
            -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"", '-Build', "`"$top`"" `
            -WorkingDirectory $top -WindowStyle Hidden `
            -RedirectStandardOutput $log -RedirectStandardError "$log.err" | Out-Null
    }

    if ($hasGraph) {
        $state = if ($needsBuild) { "an incremental refresh for HEAD $($head.Substring(0, 8)) is running in the background" } else { 'it is current with HEAD' }
        Write-Output @"
Graphify knowledge graph is available at graphify-out/ ($state).
For any codebase/architecture question, use the graphify skill FIRST: run ``python -m graphify query "<question>"`` (or ``path "<A>" "<B>"`` / ``explain "<concept>"``) before grepping or reading source files. The bare ``graphify`` launcher is not on PATH here; always use ``python -m graphify``.
"@
    } else {
        Write-Output "Graphify: no knowledge graph in this checkout yet; building it in the background (a few minutes; log: $log). Until graphify-out/graph.json exists, use normal search."
    }
} catch { }
exit 0
