if (-not (Get-Command sonar -ErrorAction SilentlyContinue)) {
    [Console]::Error.WriteLine('Sonar secrets scanning is disabled: the `sonar` CLI was not found on PATH.')
    exit 0
}
$stdinData = [Console]::In.ReadToEnd()
$stdinData | & sonar hook claude-pre-tool-use
exit $LASTEXITCODE
