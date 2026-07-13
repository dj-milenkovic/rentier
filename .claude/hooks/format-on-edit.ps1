# PostToolUse hook: auto-format a just-edited C# file so CI's fast-fail
# `dotnet format --verify-no-changes` gate doesn't trip on whitespace.
#
# Uses `dotnet format whitespace --folder` (no MSBuild workspace load) to stay
# fast per-edit. Whitespace rules cover the most common format failures; the
# full `dotnet format Rentier.slnx` (style + analyzers) remains the CI authority.
#
# Requires PowerShell 7+ (pwsh). If pwsh or dotnet is missing, the hook fails
# open (exit 0) — formatting is then caught by CI instead of blocking edits.

$payload = [Console]::In.ReadToEnd()
try { $json = $payload | ConvertFrom-Json } catch { exit 0 }

$file = $json.tool_input.file_path
if (-not $file -or [System.IO.Path]::GetExtension($file) -ne '.cs') { exit 0 }
# Never touch EF migrations (forward-only, never edited) or build output.
if ($file -match '[\\/](Migrations|obj|bin)[\\/]') { exit 0 }
if (-not (Test-Path -LiteralPath $file)) { exit 0 }

$dir = Split-Path -Path $file -Parent
$name = Split-Path -Path $file -Leaf
dotnet format whitespace $dir --folder --include $name 2>$null | Out-Null
exit 0
