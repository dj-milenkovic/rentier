# PreToolUse (Bash) hook: block a `git commit` while the solution is not formatted,
# so a commit can never trip CI's fast-fail `dotnet format --verify-no-changes` gate.
#
# format-on-edit.ps1 only fixes whitespace in the one file an Edit/Write just touched;
# it never sees style/analyzer rules or files changed from the shell. This gate runs
# the same full check as CI (`dotnet format Rentier.slnx --verify-no-changes`) right
# before the commit, and on failure blocks it (exit 2) with the files to fix.
#
#   not a git commit                         -> allow, nothing runs
#   no changed .cs/.csproj/.props/.targets/
#   .editorconfig/.slnx files in the tree    -> allow (e.g. docs-only commit), nothing runs
#   formatted                                -> allow
#   not formatted                            -> block; stderr tells Claude how to fix it
#
# Fails open (exit 0) when it cannot run the check (pwsh 7+ is required; dotnet or the
# solution missing, an unrestored solution, an unexpected dotnet error) — but never
# silently: it prints a warning on stderr. CI remains the authority either way.

$payload = [Console]::In.ReadToEnd()
try { $json = $payload | ConvertFrom-Json } catch { exit 0 }

$command = [string]$json.tool_input.command
# `git commit`, also after global options (`git -C dir commit`, `git -c k=v commit`) and
# inside compound commands (`git add -A && git commit -m ...`). Not `git commit-tree`.
$gitCommit = '(?<![\w./-])git(\s+(-[Cc]\s+\S+|--?[\w-]+(=\S+)?))*\s+commit(?![\w-])'
if ($command -notmatch $gitCommit) { exit 0 }

$root = if ($env:CLAUDE_PROJECT_DIR) { $env:CLAUDE_PROJECT_DIR } else { [string]$json.cwd }
if (-not $root) { exit 0 }
$solution = Join-Path $root 'Rentier.slnx'
if (-not (Test-Path -LiteralPath $solution)) {
    [Console]::Error.WriteLine("format-gate skipped: Rentier.slnx not found in '$root'.")
    exit 0
}

# Only C#/build inputs can change the format check's result. The commit's final content is
# not known yet (`git add` may run in the same command), so look at the whole working tree.
$formatInputs = '\.(cs|csproj|props|targets|slnx|editorconfig)$|(^|/)\.editorconfig$'
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    [Console]::Error.WriteLine('format-gate skipped: `git` was not found on PATH; CI will check formatting.')
    exit 0
}
$status = git -C $root status --porcelain --untracked-files=all 2>$null
if ($LASTEXITCODE -ne 0) {
    [Console]::Error.WriteLine("format-gate skipped: ``git status`` failed in '$root'; CI will check formatting.")
    exit 0
}
$changed = $status |
    ForEach-Object { ($_.Substring(3) -split ' -> ')[-1].Trim('"') } |
    Where-Object { $_ -match $formatInputs }
if (-not $changed) { exit 0 }

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    [Console]::Error.WriteLine('format-gate skipped: `dotnet` was not found on PATH; CI will check formatting.')
    exit 0
}

$output = & dotnet format $solution --no-restore --verify-no-changes 2>&1 | ForEach-Object { "$_" }
$code = $LASTEXITCODE

if ($code -eq 0) { exit 0 }

if ($code -eq 2) {
    $findings = $output | Where-Object { $_ -match '(error|warning) [A-Z]+\d*' } |
        ForEach-Object { $_.Trim() } | Select-Object -First 20
    [Console]::Error.WriteLine(@"
format-gate: commit blocked - the solution is not formatted (CI's ``dotnet format --verify-no-changes`` would fail).
Fix it with:  dotnet format Rentier.slnx --no-restore
then stage the changes and commit again. First findings:
$($findings -join [Environment]::NewLine)
"@)
    exit 2
}

# Any other exit code means the check itself could not run (e.g. the solution was never
# restored). Don't block the commit on a tooling problem, but say so.
$tail = ($output | Select-Object -Last 5) -join [Environment]::NewLine
[Console]::Error.WriteLine("format-gate skipped: ``dotnet format`` exited with $code (run ``dotnet restore Rentier.slnx``?). CI will check formatting.$([Environment]::NewLine)$tail")
exit 0
