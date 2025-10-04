# Hybrid eDiscovery Collector - POC Setup Script
# This script sets up the POC environment for testing

Write-Host "🏗️  Setting up Hybrid eDiscovery Collector POC..." -ForegroundColor Green

# Check if .env file exists
if (-not (Test-Path ".env")) {
    Write-Host "📄 Creating .env file from template..." -ForegroundColor Yellow
    Copy-Item ".env.template" ".env"
    Write-Host "⚠️  Please edit .env file with your Azure AD credentials before running!" -ForegroundColor Red
    Write-Host "   Required values:" -ForegroundColor Yellow
    Write-Host "   - AZURE_TENANT_ID" -ForegroundColor Yellow
    Write-Host "   - AZURE_CLIENT_ID" -ForegroundColor Yellow
    Write-Host "   - AZURE_CLIENT_SECRET" -ForegroundColor Yellow
    Write-Host ""
}

# Create output directories
Write-Host "📁 Creating output directories..." -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path ".\data" | Out-Null
New-Item -ItemType Directory -Force -Path ".\output" | Out-Null

# Build the solution
Write-Host "🔨 Building solution..." -ForegroundColor Yellow
dotnet build
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Setup complete!" -ForegroundColor Green
Write-Host ""
Write-Host "🚀 To run the POC:" -ForegroundColor Cyan
Write-Host "   Option 1 - Docker Compose (Recommended):" -ForegroundColor White
Write-Host "     docker-compose up --build" -ForegroundColor Gray
Write-Host ""
Write-Host "   Option 2 - Local Development:" -ForegroundColor White
Write-Host "     Terminal 1: dotnet run --project src/EDiscoveryIntakeApi" -ForegroundColor Gray
Write-Host "     Terminal 2: dotnet run --project src/HybridGraphCollectorWorker" -ForegroundColor Gray
Write-Host ""
Write-Host "📖 Documentation: README.md" -ForegroundColor Cyan
Write-Host "🧪 Test API: http://localhost:5230/swagger" -ForegroundColor Cyan
Write-Host "📋 Sample requests: test-requests.http" -ForegroundColor Cyan