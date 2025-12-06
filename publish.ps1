# publish.ps1
$project = "PortfolioWatch"
$releaseDir = "Releases"

# Clean previous builds
Write-Host "Cleaning project..."
dotnet clean
if (Test-Path $releaseDir) {
    Remove-Item -Path $releaseDir -Recurse -Force
}

# Framework-Dependent (Requires .NET 9)
# This version is smaller but requires the user to have .NET 9 runtime installed.
Write-Host "Publishing Framework-Dependent version..."
dotnet publish "PortfolioWatch/PortfolioWatch.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o "$releaseDir/FrameworkDependent"

Write-Host "Publish complete. Builds are available in the '$releaseDir' folder."
