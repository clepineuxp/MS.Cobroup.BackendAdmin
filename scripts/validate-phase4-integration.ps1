param(
    [string]$IdentityBaseUrl = "https://localhost:7119",
    [string]$OperationsBaseUrl = "https://localhost:7088"
)

$ErrorActionPreference = "Stop"

function ConvertTo-Base64Url([byte[]]$Value) {
    return [Convert]::ToBase64String($Value).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

$projectPath = Join-Path $PSScriptRoot "..\MS.Cobroup.BackendAdmin.Api\MS.Cobroup.BackendAdmin.Api.csproj"
[xml]$project = Get-Content -LiteralPath $projectPath
$userSecretsId = ($project.Project.PropertyGroup.UserSecretsId | Where-Object { $_ } | Select-Object -First 1).ToString()
if ([string]::IsNullOrWhiteSpace($userSecretsId)) {
    throw "The administrative API does not declare a UserSecretsId."
}

$secretsPath = Join-Path ([Environment]::GetFolderPath('ApplicationData')) "Microsoft\UserSecrets\$userSecretsId\secrets.json"
if (-not (Test-Path -LiteralPath $secretsPath)) {
    throw "Local user secrets are unavailable."
}

$secrets = Get-Content -LiteralPath $secretsPath -Raw | ConvertFrom-Json
$adminPassword = $secrets.'LocalBootstrap:AdminPassword'
if ([string]::IsNullOrWhiteSpace($adminPassword)) {
    throw "LocalBootstrap:AdminPassword is unavailable."
}

$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$handler.CookieContainer = [System.Net.CookieContainer]::new()
$handler.ServerCertificateCustomValidationCallback = [System.Net.Http.HttpClientHandler]::DangerousAcceptAnyServerCertificateValidator
$client = [System.Net.Http.HttpClient]::new($handler)

try {
    $antiforgeryResponse = $client.GetAsync("$IdentityBaseUrl/api/v1/auth/antiforgery").GetAwaiter().GetResult()
    [void]$antiforgeryResponse.EnsureSuccessStatusCode()
    $antiforgery = ($antiforgeryResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json).requestToken

    $loginMessage = [System.Net.Http.HttpRequestMessage]::new(
        [System.Net.Http.HttpMethod]::Post,
        "$IdentityBaseUrl/api/v1/auth/session")
    $loginMessage.Headers.Add("X-XSRF-TOKEN", $antiforgery)
    $loginJson = @{ username = "admin"; password = $adminPassword } | ConvertTo-Json -Compress
    $loginMessage.Content = [System.Net.Http.StringContent]::new(
        $loginJson,
        [Text.Encoding]::UTF8,
        "application/json")
    $loginResponse = $client.SendAsync($loginMessage).GetAwaiter().GetResult()
    [void]$loginResponse.EnsureSuccessStatusCode()

    $verifier = ConvertTo-Base64Url ([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
    $challenge = ConvertTo-Base64Url ([Security.Cryptography.SHA256]::HashData([Text.Encoding]::ASCII.GetBytes($verifier)))
    $state = [Guid]::NewGuid().ToString('N')
    $authorizeUrl = "$IdentityBaseUrl/connect/authorize?client_id=cobroup-admin-spa&redirect_uri=$([Uri]::EscapeDataString('http://localhost:5300/auth/callback'))&response_type=code&scope=$([Uri]::EscapeDataString('openid profile cobroup-api'))&code_challenge=$challenge&code_challenge_method=S256&state=$state"
    $authorizeResponse = $client.GetAsync($authorizeUrl).GetAwaiter().GetResult()
    if ($authorizeResponse.StatusCode -ne [System.Net.HttpStatusCode]::Redirect) {
        throw "Authorization did not produce the expected redirect: $($authorizeResponse.StatusCode)."
    }

    $callback = $authorizeResponse.Headers.Location
    $query = [System.Web.HttpUtility]::ParseQueryString($callback.Query)
    if ($query['state'] -ne $state -or [string]::IsNullOrWhiteSpace($query['code'])) {
        throw "The authorization callback is invalid."
    }

    $tokenFields = [System.Collections.Generic.Dictionary[string,string]]::new()
    $tokenFields['grant_type'] = 'authorization_code'
    $tokenFields['client_id'] = 'cobroup-admin-spa'
    $tokenFields['redirect_uri'] = 'http://localhost:5300/auth/callback'
    $tokenFields['code'] = $query['code']
    $tokenFields['code_verifier'] = $verifier
    $tokenResponse = $client.PostAsync(
        "$IdentityBaseUrl/connect/token",
        [System.Net.Http.FormUrlEncodedContent]::new($tokenFields)).GetAwaiter().GetResult()
    [void]$tokenResponse.EnsureSuccessStatusCode()
    $accessToken = ($tokenResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json).access_token

    $client.DefaultRequestHeaders.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $accessToken)
    $operationsResponse = $client.GetAsync("$OperationsBaseUrl/api/operations/v1/customers?page=1&pageSize=1").GetAwaiter().GetResult()
    $facadeResponse = $client.GetAsync("$IdentityBaseUrl/api/v1/customers?page=1&pageSize=1").GetAwaiter().GetResult()

    [pscustomobject]@{
        Login = [int]$loginResponse.StatusCode
        Token = [int]$tokenResponse.StatusCode
        OperationsCustomers = [int]$operationsResponse.StatusCode
        AdministrativeFacadeCustomers = [int]$facadeResponse.StatusCode
    }
}
finally {
    $adminPassword = $null
    $client.Dispose()
    $handler.Dispose()
}
