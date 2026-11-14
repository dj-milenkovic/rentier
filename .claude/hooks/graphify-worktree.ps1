# SessionStart hook: give linked git worktrees their own Graphify graph.
#
# graphify's post-commit hook deliberately rebuilds only in the primary checkout,
# so a fresh `git worktree add` has no graphify-out/. When a session starts in a
# linked worktree with no graph yet, kick off `python -m graphify update .` in the
# background (AST-only, no LLM, no API cost). graphify-out/ is gitignored.
#
# Fails open (exit 0) on any problem: a missing graph must never block a session.

try {
    $gitDir = (git rev-parse --absolute-git-dir 2>$null)
    $commonDir = (git rev-parse --path-format=absolute --git-common-dir 2>$null)
    if (-not $gitDir -or -not $commonDir) { exit 0 }

    # Primary checkout: git-dir == common-dir. graphify's own hook handles that one.
    if ((Resolve-Path $gitDir).Path -eq (Resolve-Path $commonDir).Path) { exit 0 }

    $top = (git rev-parse --show-toplevel 2>$null)
    if (-not $top -or (Test-Path -LiteralPath (Join-Path $top 'graphify-out/graph.json'))) { exit 0 }

    $python = Get-Command python -ErrorAction SilentlyContinue
    if (-not $python) { exit 0 }
    & $python.Source -c "import graphify" 2>$null
    if ($LASTEXITCODE -ne 0) { exit 0 }

    $log = Join-Path ([System.IO.Path]::GetTempPath()) 'graphify-worktree-build.log'
    Start-Process -FilePath $python.Source -ArgumentList '-m', 'graphify', 'update', '.' `
        -WorkingDirectory $top -WindowStyle Hidden `
        -RedirectStandardOutput $log -RedirectStandardError "$log.err" | Out-Null
    Write-Output "Graphify: building graph for this worktree in the background (log: $log)."
} catch { }
exit 0
