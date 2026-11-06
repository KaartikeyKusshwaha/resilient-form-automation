$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$clonePath = Join-Path ([System.IO.Path]::GetTempPath()) ("resilient-form-automation-clean-" + [guid]::NewGuid().ToString('N'))
$dotnetPath = Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnetPath)) { $dotnetPath = 'dotnet' }

try {
    git clone --no-hardlinks $repositoryRoot $clonePath
    if ($LASTEXITCODE -ne 0) { throw 'git clone failed.' }
    Push-Location $clonePath
    try {
        & $dotnetPath build -c Release -warnaserror
        if ($LASTEXITCODE -ne 0) { throw 'Clean-clone build failed.' }
        & $dotnetPath test -c Release --filter 'Category!=Live'
        if ($LASTEXITCODE -ne 0) { throw 'Clean-clone non-live tests failed.' }
    }
    finally { Pop-Location }
    Write-Output "Clean-clone verification passed: $clonePath"
}
finally {
    $tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
    $resolvedClone = [System.IO.Path]::GetFullPath($clonePath)
    $expectedPrefix = Join-Path $tempRoot 'resilient-form-automation-clean-'
    if ($resolvedClone.StartsWith($expectedPrefix, [System.StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $resolvedClone)) {
        Remove-Item -LiteralPath $resolvedClone -Recurse -Force
    }
}
