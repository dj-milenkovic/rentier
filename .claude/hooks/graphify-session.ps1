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
# But never *silently*: every failure prints a "Graphify: ..." line into the session
# context, and the background worker records its outcome in a status file next to
# its log so the next session start can report a failed build.
#
# Works on Windows, Linux and macOS (PowerShell 7+; `-WindowStyle` is Windows-only).
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
    $status = Join-Path ([System.IO.Path]::GetTempPath()) "graphify-session-$((Split-Path $Build -Leaf)).log.status"
    try {
        Set-Location -LiteralPath $Build
        $env:PYTHONHASHSEED = '0'   # deterministic Louvain clustering, same as graphify's git hooks
        $python = Find-GraphifyPython
        if (-not $python) {
            Set-Content -LiteralPath $status -Value 'failed: python with the graphify package not found'
            exit 0
        }
        $head = (git rev-parse HEAD 2>$null)
        & $python -m graphify update .
        if ($LASTEXITCODE -ne 0) {
            Set-Content -LiteralPath $status -Value "failed: 'graphify update' exited with code $LASTEXITCODE"
            exit 0
        }
        if ($head) {
            Set-Content -LiteralPath (Join-Path 'graphify-out' $HEAD_MARKER) -Value $head -NoNewline
        }
        Set-Content -LiteralPath $status -Value 'ok'
    } catch {
        Write-Error $_
        try { Set-Content -LiteralPath $status -Value "failed: $($_.Exception.Message)" } catch { }
    }
    exit 0
}

try {
    $top = (git rev-parse --show-toplevel 2>$null)
    if (-not $top) {
        Write-Output 'Graphify: not inside a git checkout (or git is not on PATH); skipped graph refresh.'
        exit 0
    }
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
        $what = if ($hasGraph) { 'graphify-out/graph.json exists but it cannot be refreshed or queried' } else { 'no knowledge graph can be built' }
        Write-Output "Graphify: the graphify Python package was not found (looked for python, python3, py), so $what. Install it with ``pip install graphifyy``. Use normal search meanwhile."
        exit 0
    }

    $log = Join-Path ([System.IO.Path]::GetTempPath()) "graphify-session-$((Split-Path $top -Leaf)).log"
    $statusPath = "$log.status"
    $lastFailure = $null
    if (Test-Path -LiteralPath $statusPath) {
        $prev = (Get-Content -LiteralPath $statusPath -Raw).Trim()
        if ($prev -like 'failed*') { $lastFailure = "Graphify: the previous background build $prev (see $log and $log.err)." }
    }

    $needsBuild = (-not $hasGraph) -or ($head -and $builtHead -ne $head)
    if ($needsBuild) {
        $pwsh = (Get-Process -Id $PID).Path
        $workerArgs = '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath, '-Build', $top
        Set-Content -LiteralPath $statusPath -Value 'running'
        if ($IsWindows -or $PSVersionTable.PSEdition -eq 'Desktop') {
            $worker = Start-Process -FilePath $pwsh -WindowStyle Hidden -WorkingDirectory $top `
                -ArgumentList ($workerArgs | ForEach-Object { "`"$_`"" }) `
                -RedirectStandardOutput $log -RedirectStandardError "$log.err" -PassThru
        } else {
            # Unix: Start-Process pipes the child's output through THIS process, so the worker
            # would die (SIGPIPE) the moment the hook exits. Let a detached shell redirect to the
            # files natively instead, in its own session (setsid, where available) so it outlives us.
            $shScript = 'log=$1; shift; if command -v setsid >/dev/null 2>&1; then set -- setsid "$@"; fi; nohup "$@" >"$log" 2>"$log.err" </dev/null &'
            # ProcessStartInfo.ArgumentList passes each argument verbatim (no re-quoting).
            $psi = [System.Diagnostics.ProcessStartInfo]::new('sh')
            $psi.WorkingDirectory = $top
            foreach ($arg in (@('-c', $shScript, 'sh', $log, $pwsh) + $workerArgs)) { $psi.ArgumentList.Add($arg) }
            $worker = [System.Diagnostics.Process]::Start($psi)
            if (-not $worker.WaitForExit(5000)) { throw 'sh did not return after launching the background build.' }
            if ($worker.ExitCode -ne 0) { throw "sh failed to launch the background build (exit $($worker.ExitCode))." }
        }
        if (-not $worker) { throw 'Start-Process returned no process for the background build.' }
    }

    if ($hasGraph) {
        $state = if ($needsBuild) { "an incremental refresh for HEAD $($head.Substring(0, 8)) is running in the background" } else { 'it is current with HEAD' }
        Write-Output @"
Graphify knowledge graph is available at graphify-out/ ($state).
For any codebase/architecture question, use the graphify skill FIRST: run ``graphify query "<question>"`` (or ``graphify path "<A>" "<B>"`` / ``graphify explain "<concept>"``) before grepping or reading source files.
"@
    } else {
        Write-Output "Graphify: no knowledge graph in this checkout yet; building it in the background (a few minutes; log: $log). Until graphify-out/graph.json exists, use normal search."
    }
    if ($lastFailure) { Write-Output $lastFailure }
} catch {
    Write-Output "Graphify: the SessionStart hook failed and the graph was not refreshed: $($_.Exception.Message). Use normal search."
}
exit 0
