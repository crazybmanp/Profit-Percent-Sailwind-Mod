param (
    [string]$Configuration = "Release"
)

$ProjectName = "ProfitPercent"

# 1. Auto-detect the version from the C# file so the zip is always named correctly
$MainFileContent = Get-Content -Path "$PSScriptRoot\ProfitPercentMain.cs" -Raw
if ($MainFileContent -match 'public const string pluginVersion = "(.*?)";') {
    $Version = $matches[1]
} else {
    $Version = "Unknown"
}

$ZipName = "${ProjectName}_Unofficial_v${Version}.zip"
$BuildsDir = "$PSScriptRoot\builds"
$ZipPath = "$BuildsDir\$ZipName"
$OutputDir = "$PSScriptRoot\bin\ReleasePackage"
$PluginFolder = "$OutputDir\$ProjectName"

Write-Host "Starting packaging process for $ProjectName v$Version..." -ForegroundColor Cyan

# 0. Check if it already exists before building
if (-not (Test-Path $BuildsDir)) {
    New-Item -ItemType Directory -Path $BuildsDir | Out-Null
}
if (Test-Path $ZipPath) {
    Write-Host "ERROR: $ZipName already exists in the builds folder!" -ForegroundColor Red
    Write-Host "Packaging aborted. You must manually delete the old zip or bump the pluginVersion in ProfitPercentMain.cs." -ForegroundColor Red
    exit 1
}

# 2. Compile the project
Write-Host "Building project in $Configuration mode..."
dotnet build -c $Configuration "$PSScriptRoot\${ProjectName}.csproj"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed! Aborting packaging." -ForegroundColor Red
    exit $LASTEXITCODE
}

# 3. Clean and prepare the temporary staging folder
if (Test-Path $OutputDir) { Remove-Item -Recurse -Force $OutputDir }
New-Item -ItemType Directory -Path $PluginFolder | Out-Null

# 4. Copy all required files into the staging folder
Write-Host "Copying files to staging directory..."
Copy-Item -Path "$PSScriptRoot\bin\$Configuration\${ProjectName}.dll" -Destination $PluginFolder
Copy-Item -Path "$PSScriptRoot\bin\$Configuration\${ProjectName}.pdb" -Destination $PluginFolder
Copy-Item -Path "$PSScriptRoot\README.md" -Destination $PluginFolder
Copy-Item -Path "$PSScriptRoot\CHANGELOG.md" -Destination $PluginFolder

# 5. Compress into a Zip archive
Write-Host "Zipping package into builds directory..."
# By pointing to the folder itself, the zip will contain a root "ProfitPercent" folder.
Compress-Archive -Path $PluginFolder -DestinationPath $ZipPath

# 6. Cleanup
Remove-Item -Recurse -Force $OutputDir

Write-Host "Success! Created package: $ZipName" -ForegroundColor Green
Write-Host "You can now distribute this zip file."
