[CmdletBinding()]
param(
    [ValidateRange(1, 64)]
    [int]$DotNetProcessorCount = 1
)

$ErrorActionPreference = 'Stop'
$unityProjectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$testResultsPath = Join-Path $unityProjectPath 'TestResults'
$originalDotNetProcessorCount = [Environment]::GetEnvironmentVariable('DOTNET_PROCESSOR_COUNT', 'Process')
New-Item -ItemType Directory -Path $testResultsPath -Force | Out-Null

try {
    # Limit the build host only; this does not alter WebGL player settings or global environment.
    $env:DOTNET_PROCESSOR_COUNT = [string]$DotNetProcessorCount
    & unity run $unityProjectPath --editor-version 2022.3.62f2 --timeout 1800 -- -buildTarget WebGL -executeMethod LastExit.Editor.Gameplay.GameplayWebGlBuilder.BuildRelease -logFile (Join-Path $testResultsPath 'build-webgl.log')

    if ($LASTEXITCODE -ne 0) {
        throw "Unity WebGL export failed with exit code $LASTEXITCODE. See TestResults/build-webgl.log."
    }
}
finally {
    if ($null -eq $originalDotNetProcessorCount) {
        Remove-Item -LiteralPath 'Env:DOTNET_PROCESSOR_COUNT' -ErrorAction SilentlyContinue
    }
    else {
        [Environment]::SetEnvironmentVariable('DOTNET_PROCESSOR_COUNT', $originalDotNetProcessorCount, 'Process')
    }
}
