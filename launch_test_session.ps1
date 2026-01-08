$ErrorActionPreference = "Stop"

Write-Host "Building Bomberman.App (Release)..."
dotnet build src/Bomberman.App/Bomberman.App.csproj -c Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

$exePath = "src/Bomberman.App/bin/Release/net9.0/Bomberman.App.exe"

if (-not (Test-Path $exePath)) {
    Write-Host "Executable not found at $exePath" -ForegroundColor Red
    exit 1
}

Write-Host "Launching 4 Instances..."

# Launch Host (Instance 1)
Start-Process $exePath

# Launch Clients (Instances 2-4)
Start-Process $exePath
Start-Process $exePath
Start-Process $exePath

Write-Host "Global thermonuclear war initiated. Have fun!"
