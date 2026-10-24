#requires -Version 5.1
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
$executableName = if ($Namespace) { 'kubectl' } else { 'dotnet' }
$executable = Get-Command $executableName -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $executable) {
    if ($Namespace) {
        throw 'No se encontró kubectl en PATH. El modo -Namespace requiere kubectl y acceso al clúster. Para aprovisionar desde el proyecto local con su conexión configurada, omite -Namespace y usa ASPNETCORE_ENVIRONMENT=Development.'
    }
    throw 'No se encontró dotnet en PATH. El modo local requiere el SDK .NET 10; instala o agrega dotnet al PATH antes de continuar.'
}
$securePassword = Read-Host 'Contraseña del administrador dueño' -AsSecureString
$passwordPointer = [IntPtr]::Zero
$process = $null
try {
    $passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
    $password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
    if ([string]::IsNullOrEmpty($password)) {
        throw 'La contraseña es obligatoria.'
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
    $startInfo.FileName = $executable.Source
    if ($Namespace) {
        $arguments = @('exec', '-i', '-n', $Namespace, 'deployment/admin-backend', '--',
            'dotnet', 'MS.Cobroup.BackendAdmin.Api.dll', '--provision-organization')
    } else {
        # Local Windows execution reads the API project's Development User Secrets.
        $startInfo.EnvironmentVariables['ASPNETCORE_ENVIRONMENT'] = 'Development'
        $projectPath = Join-Path $PSScriptRoot '../MS.Cobroup.BackendAdmin.Api/MS.Cobroup.BackendAdmin.Api.csproj'
        $arguments = @('run', '--no-launch-profile', '--project', $projectPath, '--', '--provision-organization')
    }
    if ($startInfo.PSObject.Properties.Name -contains 'ArgumentList') {
        foreach ($argument in $arguments) { $startInfo.ArgumentList.Add($argument) }
    } else {
        # .NET Framework (Windows PowerShell 5.1) requires a quoted native command line.
        $quotedArguments = foreach ($argument in $arguments) {
            $escaped = [regex]::Replace($argument, '(\\*)"', '$1$1\"')
            $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
            '"' + $escaped + '"'
        }
        $startInfo.Arguments = $quotedArguments -join ' '
    }
    $process = [Diagnostics.Process]::Start($startInfo)
    # Write UTF-8 directly: StandardInputEncoding is unavailable on .NET Framework.
    $payloadBytes = [Text.UTF8Encoding]::new($false).GetBytes($payload)
    $process.StandardInput.BaseStream.Write($payloadBytes, 0, $payloadBytes.Length)
    $process.StandardInput.Close()
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "El aprovisionamiento terminó con código $($process.ExitCode)." }
} finally {
    $password = $null
    $payload = $null
    if ($null -ne $payloadBytes) { [Array]::Clear($payloadBytes, 0, $payloadBytes.Length) }
    if ($passwordPointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer) }
    $securePassword.Dispose()
    if ($null -ne $process) { $process.Dispose() }
}
