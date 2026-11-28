if (-not (Get-Command sonar -ErrorAction SilentlyContinue)) {
    # Warn once per temp-dir lifetime (stdout is added to the session context), not on every prompt.
    $marker = Join-Path ([System.IO.Path]::GetTempPath()) 'rentier-sonar-missing.warned'
    if (-not (Test-Path -LiteralPath $marker)) {
        Write-Output 'Sonar secrets scanning is disabled: the `sonar` CLI was not found on PATH (install SonarQube CLI to enable it).'
        try { Set-Content -LiteralPath $marker -Value '1' } catch { }
    }
    exit 0
}
$stdinData = [Console]::In.ReadToEnd()
$stdinData | & sonar hook claude-prompt-submit
exit $LASTEXITCODE
