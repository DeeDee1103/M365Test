# Test Script for Hybrid eDiscovery Collector

## Run all tests
Write-Host "Running all tests..." -ForegroundColor Green

# Run unit tests
dotnet test tests/EDiscovery.Shared.Tests --logger "console;verbosity=normal"
dotnet test tests/EDiscoveryIntakeApi.Tests --logger "console;verbosity=normal" 
dotnet test tests/HybridGraphCollectorWorker.Tests --logger "console;verbosity=normal"

# Run all tests with coverage (if you have coverlet installed)
# dotnet test --collect:"XPlat Code Coverage"

Write-Host "Test execution complete!" -ForegroundColor Green