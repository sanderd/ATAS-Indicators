# Build and deploy KeyLevels indicator
Push-Location solution\sadnerd.io.ATAS.KeyLevels
dotnet build -c Debug
Copy-Item "bin\Debug\net8.0-windows\sadnerd.io.ATAS.KeyLevels.dll" "$env:APPDATA\ATAS\Indicators\" -Force
Pop-Location
Write-Host "Deployed to $env:APPDATA\ATAS\Indicators\" -ForegroundColor Green
