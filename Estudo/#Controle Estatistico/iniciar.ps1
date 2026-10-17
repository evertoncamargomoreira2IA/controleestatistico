[CmdletBinding()]
param(
    [int]$Port = 8000,
    [switch]$NoBrowser
)

$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot

$python = Join-Path $PSScriptRoot '.venv\Scripts\python.exe'

try {
    if (-not (Test-Path -LiteralPath $python)) {
        Write-Host '[1/4] Criando ambiente virtual...'
        python -m venv .venv
        if ($LASTEXITCODE -ne 0) { throw 'Falha ao criar o ambiente virtual.' }
        $python = Join-Path $PSScriptRoot '.venv\Scripts\python.exe'

        Write-Host '[2/4] Instalando dependencias...'
        & $python -m pip install -r requirements.txt
        if ($LASTEXITCODE -ne 0) { throw 'Falha ao instalar as dependencias.' }
    }
    else {
        Write-Host '[1-2/4] Ambiente virtual e dependencias existentes.'
    }

    if (-not (Test-Path -LiteralPath '.env')) {
        Write-Host 'Criando .env com chave secreta aleatoria...'
        & $python -c "import secrets,pathlib; p=pathlib.Path('.env.example'); t=p.read_text(encoding='utf-8'); pathlib.Path('.env').write_text(t.replace('SECRET_KEY=','SECRET_KEY='+secrets.token_urlsafe(48),1),encoding='utf-8')"
        if ($LASTEXITCODE -ne 0) { throw 'Falha ao criar o arquivo .env.' }
    }

    Write-Host '[3/4] Atualizando banco de dados e administrador inicial...'
    & $python -m alembic upgrade head
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao atualizar o banco de dados.' }
    & $python scripts\ensure_default_admin.py
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao preparar o administrador inicial.' }

    Write-Host ''
    Write-Host "[4/4] Aplicacao disponivel em http://127.0.0.1:$Port"
    Write-Host 'Pressione Ctrl+C para parar o servidor.'

    if (-not $NoBrowser) {
        Start-Job -ArgumentList $Port -ScriptBlock {
            param($TargetPort)
            Start-Sleep -Seconds 3
            Start-Process "http://127.0.0.1:$TargetPort"
        } | Out-Null
    }

    & $python -m uvicorn app.main:app --host 127.0.0.1 --port $Port
    if ($LASTEXITCODE -ne 0) { throw 'O servidor terminou com erro.' }
}
catch {
    Write-Error $_
    exit 1
}
