#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OrganizationCode,
    [Parameter(Mandatory)][string]$OrganizationName,
    [Parameter(Mandatory)][string]$Username,
    [Parameter(Mandatory)][string]$DisplayName,
    [Parameter(Mandatory)][string]$PhoneNumber,
    [ValidatePattern('^cobroup-(dev|qa|prod)$')][string]$Namespace
)

$ErrorActionPreference = 'Stop'
$securePassword = Read-Host 'Contraseña del administrador dueño (12 a 128 caracteres)' -AsSecureString
$passwordPointer = [IntPtr]::Zero
$process = $null
try {
    $passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
    $password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
    if ([string]::IsNullOrWhiteSpace($password) -or $password.Length -lt 12 -or $password.Length -gt 128) {
        throw 'La contraseña debe tener entre 12 y 128 caracteres.'
    }
    $payload = @{
        organizationCode = $OrganizationCode
        organizationName = $OrganizationName
        username = $Username
        displayName = $DisplayName
        phoneNumber = $PhoneNumber
        password = $password
    } | ConvertTo-Json -Compress

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardInput = $true
    $startInfo.StandardInputEncoding = [Text.UTF8Encoding]::new($false)
    if ($Namespace) {
        $startInfo.FileName = 'kubectl'
        $arguments = @('exec', '-i', '-n', $Namespace, 'deployment/admin-backend', '--',
            'dotnet', 'MS.Cobroup.BackendAdmin.Api.dll', '--provision-organization')
    } else {
        $startInfo.FileName = 'dotnet'
        $projectPath = Join-Path $PSScriptRoot '../MS.Cobroup.BackendAdmin.Api/MS.Cobroup.BackendAdmin.Api.csproj'
        $arguments = @('run', '--no-launch-profile', '--project', $projectPath, '--', '--provision-organization')
    }
    foreach ($argument in $arguments) { $startInfo.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($startInfo)
    $process.StandardInput.Write($payload)
    $process.StandardInput.Close()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "El aprovisionamiento terminó con código $($process.ExitCode)." }
} finally {
    $password = $null
    $payload = $null
    if ($passwordPointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer) }
    $securePassword.Dispose()
    if ($null -ne $process) { $process.Dispose() }
}
