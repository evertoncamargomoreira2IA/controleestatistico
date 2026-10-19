param([string]$BaseUrl = 'http://127.0.0.1:5188', [string]$TestPassword = '', [switch]$RequirePasswordChange)
# Executar apenas contra a instância Development com banco descartável.
# Com -RequirePasswordChange, testa também a troca da senha provisória. Nunca usar em produção.
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$results = [System.Collections.Generic.List[object]]::new()
function Check($name, $condition) {
    $results.Add([pscustomobject]@{ Test = $name; Passed = [bool]$condition })
    if (-not $condition) { throw "Verificação falhou: $name" }
}
function Token($html) {
    $match = [regex]::Match($html, 'name="__RequestVerificationToken" value="([^"]+)"')
    if (-not $match.Success) { throw 'Token antiforgery ausente.' }
    [System.Net.WebUtility]::HtmlDecode($match.Groups[1].Value)
}
function Request($path, $session) {
    Invoke-WebRequest "$BaseUrl$path" -WebSession $session -MaximumRedirection 0 -SkipHttpErrorCheck -ErrorAction SilentlyContinue
}
try {
    $session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
    $login = Request '/Account/Login' $session
    Check 'Página de login responde 200' ($login.StatusCode -eq 200)
    foreach ($path in @('/', '/coleta', '/historico', '/usuarios', '/cadastros', '/definicoes', '/cep-dia', '/cep-coleta', '/cep-cartas')) {
        $response = Request $path $session
        Check "Anônimo bloqueado: $path" ($response.StatusCode -eq 302 -and "$($response.Headers.Location)" -match '/Account/Login')
    }
    $invalid = Invoke-WebRequest "$BaseUrl/Account/Login" -Method Post -WebSession $session -Body @{ '_handler'='login'; 'Input.Email'='admin'; 'Input.Password'='admin' } -SkipHttpErrorCheck
    Check 'POST sem token antiforgery é rejeitado' ($invalid.StatusCode -eq 400)
    $login = Request '/Account/Login' $session
    $response = Invoke-WebRequest "$BaseUrl/Account/Login" -Method Post -WebSession $session -Body @{ '_handler'='login'; '__RequestVerificationToken'=(Token $login.Content); 'Input.Email'='admin'; 'Input.Password'='admin' } -MaximumRedirection 0 -SkipHttpErrorCheck -ErrorAction SilentlyContinue
    Check 'Login de desenvolvimento autentica' ($response.StatusCode -eq 302)
    $response = Request '/' $session
    if ($RequirePasswordChange) {
    Check 'Primeiro acesso exige troca de senha' ($response.StatusCode -eq 302 -and "$($response.Headers.Location)" -match '/Account/Manage/ChangePassword')
    $change = Request '/Account/Manage/ChangePassword' $session
    $temporaryPassword = if ($TestPassword) { $TestPassword } else { 'Sprint0-' + [guid]::NewGuid().ToString('N') + 'Aa1' }
    $response = Invoke-WebRequest "$BaseUrl/Account/Manage/ChangePassword" -Method Post -WebSession $session -Body @{ '_handler'='change-password'; '__RequestVerificationToken'=(Token $change.Content); 'Input.OldPassword'='admin'; 'Input.NewPassword'=$temporaryPassword; 'Input.ConfirmPassword'=$temporaryPassword } -MaximumRedirection 0 -SkipHttpErrorCheck -ErrorAction SilentlyContinue
    Check 'Troca de senha aceita' ($response.StatusCode -eq 302)
    } else {
        Check 'Demonstração mantém admin/admin sem troca obrigatória' ($response.StatusCode -eq 200)
    }
    foreach ($path in @('/', '/coleta', '/historico', '/usuarios', '/cadastros', '/definicoes', '/cep-dia', '/cep-coleta', '/cep-cartas')) {
        $response = Request $path $session
        Check "Administrador acessa: $path" ($response.StatusCode -eq 200)
    }
    $history = Request '/historico' $session
    $charts = Request '/cep-cartas' $session
    Check 'Menu sem módulos retirados' ($charts.Content -notmatch 'Gage R|Kappa')
    $settingsPage = Request '/definicoes' $session
    Check 'Definições sem módulos retirados' ($settingsPage.Content -notmatch 'Gage R|Kappa|Validade do MSA')
    Check 'Histórico mostra autenticação' ($history.Content -match 'login.success')
} finally {
    $results | ConvertTo-Json | Set-Content artifacts/http-smoke.json -Encoding utf8
    $results | Format-Table -AutoSize
}
