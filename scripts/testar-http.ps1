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
    foreach ($path in @('/', '/coleta', '/historico', '/usuarios', '/cadastros', '/definicoes', '/cep-dia', '/cep-coleta', '/cep-cartas', '/cep-operacao', '/cp-coleta', '/cp-dia', '/cp-estudos')) {
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
    foreach ($path in @('/', '/coleta', '/historico', '/usuarios', '/cadastros', '/definicoes', '/cep-dia', '/cep-coleta', '/cep-cartas', '/cep-operacao', '/cp-coleta', '/cp-dia', '/cp-estudos')) {
        $response = Request $path $session
        Check "Administrador acessa: $path" ($response.StatusCode -eq 200)
    }
    $history = Request '/historico' $session
    $charts = Request '/cep-cartas' $session
    Check 'Menu sem módulos retirados' ($charts.Content -notmatch 'Gage R|Kappa')
    $settingsPage = Request '/definicoes' $session
    Check 'Definições sem módulos retirados' ($settingsPage.Content -notmatch 'Gage R|Kappa|Validade do MSA')
    Check 'Histórico mostra autenticação' ($history.Content -match 'login.success')
    $operationsPage = Request '/cep-operacao' $session
    Check 'CEP operacional renderiza' ($operationsPage.Content -match 'CEP operacional')
    Check 'Formulário de metas disponível' ($operationsPage.Content -match 'Meta de conjuntos')
    Check 'Aprovação de limites disponível ao administrador' ($operationsPage.Content -match 'Aprovar e registrar')
    $capabilityCollection = Request '/cp-coleta' $session
    Check 'Coleta Cp/Cpk mostra 33 amostras' ($capabilityCollection.Content -match '33' -and $capabilityCollection.Content -match 'Novo conjunto Cp/Cpk' -and $capabilityCollection.Content -notmatch 'Novo conjunto CEP')
    $capabilityStudies = Request '/cp-estudos' $session
    Check 'Estudos Cp/Cpk explicam o método e oferecem cálculo' ($capabilityStudies.Content -match '1,128' -and $capabilityStudies.Content -match 'Calcular e salvar estudo')
    $capabilityDaily = Request '/cp-dia' $session
    Check 'Progresso Cp/Cpk aponta para a coleta correta' ($capabilityDaily.Content -match 'href="/cp-coleta"')
    foreach ($file in @('Simulado_CEP_25x3.csv','Simulado_CEP_25x3.xlsx','Simulado_CP_CPK_33x3.csv','Simulado_CP_CPK_33x3.xlsx')) {
        $download = Request "/modelos/$file" $session
        Check "Download do simulado: $file" ($download.StatusCode -eq 200 -and $download.RawContentLength -gt 100)
    }

} finally {
    $results | ConvertTo-Json | Set-Content artifacts/http-smoke.json -Encoding utf8
    $results | Format-Table -AutoSize
}
