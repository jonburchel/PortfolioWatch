# publish.ps1
$project = "PortfolioWatch"
$releaseDir = "Releases"

# Clean previous builds
Write-Host "Cleaning project..."
dotnet clean
if (Test-Path $releaseDir) {
    Remove-Item -Path $releaseDir -Recurse -Force
}

# 1. Self-Contained (Includes .NET Runtime)
# This version is larger but runs on machines without .NET 9 installed.
Write-Host "Publishing Self-Contained version (Standalone)..."
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$releaseDir/SelfContained"
Rename-Item -Path "$releaseDir/SelfContained/PortfolioWatch.exe" -NewName "PortfolioWatch.with.NET.9.0.exe"

# 2. Framework-Dependent (Requires .NET 9)
# This version is smaller but requires the user to have .NET 9 runtime installed.
Write-Host "Publishing Framework-Dependent version..."
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o "$releaseDir/FrameworkDependent"

Write-Host "Publish complete. Builds are available in the '$releaseDir' folder."
