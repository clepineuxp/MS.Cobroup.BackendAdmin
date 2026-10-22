param(
    [string]$BaseUrl = "http://localhost:5037",
    [string]$Database = ""
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http

# 1. Localizar User Secrets
$projectPath = Join-Path $PSScriptRoot "..\MS.Cobroup.BackendAdmin.Api\MS.Cobroup.BackendAdmin.Api.csproj"
[xml]$project = Get-Content -LiteralPath $projectPath
$userSecretsId = ($project.Project.PropertyGroup.UserSecretsId | Where-Object { $_ } | Select-Object -First 1).ToString()
if ([string]::IsNullOrWhiteSpace($userSecretsId)) {
    throw "El proyecto MS.Cobroup.BackendAdmin.Api no tiene un UserSecretsId configurado."
}

$secretsPath = Join-Path ([Environment]::GetFolderPath('ApplicationData')) "Microsoft\UserSecrets\$userSecretsId\secrets.json"
if (-not (Test-Path -LiteralPath $secretsPath)) {
    throw "Los user secrets locales no estan disponibles en: $secretsPath"
}

# Tolerar lineas comentadas con // dentro de secrets.json
$rawLines = Get-Content -LiteralPath $secretsPath
$cleanJson = ($rawLines | Where-Object { $_ -notmatch '^\s*//' }) -join "`n"
$secrets = $cleanJson | ConvertFrom-Json

$bootstrapKey = $secrets.'Bootstrap:Key'
$adminPassword = $secrets.'LocalBootstrap:AdminPassword'
$supervisorPassword = $secrets.'LocalBootstrap:SupervisorPassword'
$collectorPassword = $secrets.'LocalBootstrap:CollectorPassword'

if ([string]::IsNullOrWhiteSpace($bootstrapKey)) { throw "Bootstrap:Key no esta configurado en User Secrets." }
if ([string]::IsNullOrWhiteSpace($adminPassword)) { throw "LocalBootstrap:AdminPassword no esta configurado en User Secrets." }
if ([string]::IsNullOrWhiteSpace($supervisorPassword)) { throw "LocalBootstrap:SupervisorPassword no esta configurado en User Secrets." }
if ([string]::IsNullOrWhiteSpace($collectorPassword)) { throw "LocalBootstrap:CollectorPassword no esta configurado en User Secrets." }

$process = $null

try {
    # 2. Iniciar API con SeedClientsOnStartup=true y base de datos opcional
    Write-Host "Iniciando MS.Cobroup.BackendAdmin.Api (SeedClientsOnStartup=true) en $BaseUrl..." -ForegroundColor Yellow
    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = "dotnet"
    $psi.Arguments = "run --project `"$projectPath`" --no-build --launch-profile http"
    $psi.WorkingDirectory = (Split-Path -Parent $projectPath)
    $psi.EnvironmentVariables["Identity__SeedClientsOnStartup"] = "true"
    $psi.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Development"
    
    if (-not [string]::IsNullOrWhiteSpace($Database)) {
        Write-Host "Sobrescribiendo base de datos destino a: $Database" -ForegroundColor Cyan
        $customConn = "Host=13.140.36.249;Port=5432;Database=$Database;Username=cobroup_app;Password=59tbKtevwQM/G8BpceP3+D/bDjWUK3L/GQyVZWovS+w="
        $psi.EnvironmentVariables["ConnectionStrings__IdentityDatabase"] = $customConn
    }
    
    $psi.UseShellExecute = $false

    $process = [System.Diagnostics.Process]::Start($psi)

    $retries = 35
    while ($retries -gt 0) {
        Start-Sleep -Seconds 1
        try {
            $ping = Invoke-WebRequest -Uri "$BaseUrl/health/live" -UseBasicParsing -TimeoutSec 2 -ErrorAction SilentlyContinue
            if ($ping.StatusCode -eq 200) {
                Write-Host "API lista y respondiendo en $BaseUrl." -ForegroundColor Green
                break
            }
        } catch {}
        $retries--
    }

    if ($retries -eq 0) {
        throw "Tiempo de espera agotado esperando que la API inicie en $BaseUrl."
    }

    # 3. Enviar peticion de Bootstrap
    Write-Host "Ejecutando POST /api/v1/bootstrap..." -ForegroundColor Yellow
    $bootstrapPayload = @{
        organizationCode = "COBROUP"
        organizationName = "Cobroup"
        administrator = @{
            username = "admin"
            displayName = "Administrador Principal"
            password = $adminPassword
        }
        supervisor = @{
            username = "supervisor"
            displayName = "Supervisor Principal"
            password = $supervisorPassword
        }
        collector = @{
            username = "collector"
            displayName = "Cobrador Principal"
            password = $collectorPassword
        }
    } | ConvertTo-Json -Depth 5

    $headers = @{
        "X-Bootstrap-Key" = $bootstrapKey
        "Content-Type" = "application/json"
    }

    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/v1/bootstrap" -Method Post -Headers $headers -Body $bootstrapPayload
        Write-Host "=== BOOTSTRAP COMPLETADO EXITOSAMENTE (201 Created) ===" -ForegroundColor Green
        $resJson = $response | ConvertTo-Json -Compress
        Write-Host "Respuesta: $resJson" -ForegroundColor Green
    } catch {
        if ($_.Exception.Response.StatusCode.value__ -eq 409) {
            Write-Host "=== EL BOOTSTRAP YA HABIA SIDO COMPLETADO (409 Conflict) ===" -ForegroundColor Yellow
        } else {
            throw $_
        }
    }

} finally {
    if ($process -ne $null -and -not $process.HasExited) {
        Write-Host "Deteniendo proceso temporal de la API..." -ForegroundColor Gray
        $process.Kill()
        $process.WaitForExit(5000)
    }
}
