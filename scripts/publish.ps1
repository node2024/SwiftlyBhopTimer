param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot "SwiftlyBhopTimer.csproj"
$buildRoot = Join-Path $projectRoot "build"

$targets = @(
    @{ Runtime = "linux-x64"; Output = "SwiftlyBhopTimer_linux" },
    @{ Runtime = "win-x64"; Output = "SwiftlyBhopTimer_windows" }
)

foreach ($target in $targets) {
    $outputPath = Join-Path $buildRoot $target.Output
    dotnet publish $projectFile `
        -c $Configuration `
        -r $target.Runtime `
        --self-contained false `
        -o $outputPath

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for runtime '$($target.Runtime)' with exit code $LASTEXITCODE."
    }
}
