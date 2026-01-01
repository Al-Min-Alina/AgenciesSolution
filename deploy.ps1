param(
    [string]$Environment = "Development",
    [switch]$BuildOnly,
    [switch]$TestOnly,
    [switch]$DeployOnly
)

Write-Host "=== Agencies Deployment ===" -ForegroundColor Cyan
Write-Host "Environment: $Environment" -ForegroundColor Yellow

# Build solution
if (-not $DeployOnly) {
    Write-Host "Building solution..." -ForegroundColor Yellow
    dotnet build -c Release
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        exit 1
    }
}

# Publish API
if (-not $BuildOnly -and -not $TestOnly) {
    Write-Host "Publishing API..." -ForegroundColor Yellow
    
    $apiOutput = "publish/api/$Environment"
    
    # Create directory
    if (Test-Path $apiOutput) {
        Remove-Item -Path $apiOutput -Recurse -Force
    }
    New-Item -ItemType Directory -Path $apiOutput -Force | Out-Null
    
    # Find API project
    $apiProject = Get-ChildItem -Filter "*.API.csproj" -Recurse | Select-Object -First 1
    
    if ($apiProject) {
        Write-Host "Found API project: $($apiProject.Name)" -ForegroundColor Green
        dotnet publish $apiProject.FullName -c Release -o $apiOutput
        
        Write-Host "API published to: $apiOutput" -ForegroundColor Green
    }
    else {
        Write-Host "No API project found!" -ForegroundColor Red
    }
}

Write-Host "=== Deployment completed ===" -ForegroundColor Green
