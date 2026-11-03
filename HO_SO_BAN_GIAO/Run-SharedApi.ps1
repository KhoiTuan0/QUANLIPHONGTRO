param(
    [string]$SqlServer = '.\SQLEXPRESS',
    [string]$Database = 'KLCN052_QuanLyPhongTro',
    [ValidateRange(1024,65535)][int]$Port = 5180,
    [switch]$Lan
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$previousConnection = $env:ConnectionStrings__DefaultConnection
if (Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue) {
    throw "Port $Port is already in use. Use the existing API or select another port."
}
$connection = [System.Data.SqlClient.SqlConnectionStringBuilder]::new()
$connection.DataSource = $SqlServer
$connection.InitialCatalog = $Database
$connection.IntegratedSecurity = $true
$connection.TrustServerCertificate = $true
$bindAddress = if ($Lan) { '0.0.0.0' } else { '127.0.0.1' }
try {
    $env:ConnectionStrings__DefaultConnection = $connection.ConnectionString
    Write-Host "API: http://localhost:$Port/api/health | Database: $Database"
    & dotnet run --project (Join-Path $projectRoot 'QLPT.API') --no-launch-profile --urls "http://${bindAddress}:$Port" --environment Development
    if ($LASTEXITCODE -ne 0) { throw "API exited with code $LASTEXITCODE" }
} finally {
    $env:ConnectionStrings__DefaultConnection = $previousConnection
}
