@echo off
rem Inicia a plataforma QMS/CEP localmente (ambiente de teste).
setlocal
cd /d "%~dp0"

if not exist ".venv\Scripts\python.exe" (
    echo [1/4] Criando ambiente Python .venv ...
    python -m venv .venv || goto :erro
    echo [2/4] Instalando dependencias ...
    ".venv\Scripts\python.exe" -m pip install -r requirements.txt || goto :erro
) else (
    echo [1-2/4] Ambiente .venv ja existe.
)

if not exist ".env" (
    echo Criando .env com chave secreta aleatoria ...
    ".venv\Scripts\python.exe" -c "import secrets,pathlib;t=pathlib.Path('.env.example').read_text(encoding='utf-8');pathlib.Path('.env').write_text(t.replace('SECRET_KEY=','SECRET_KEY='+secrets.token_urlsafe(48),1),encoding='utf-8')" || goto :erro
)

echo [3/4] Atualizando banco de dados ...
".venv\Scripts\python.exe" -m alembic upgrade head || goto :erro
".venv\Scripts\python.exe" scripts\ensure_default_admin.py || goto :erro

echo.
echo Primeiro acesso: usuario admin / senha admin (o sistema pede para trocar a senha)
echo     dados_exemplo.bat   (opcional: cria dados de exemplo)
echo.
echo [4/4] Servidor em http://127.0.0.1:8000  - NAO feche esta janela (Ctrl+C para parar)
rem Abre o navegador alguns segundos depois, quando o servidor ja estiver no ar.
start "" /min powershell -NoProfile -Command "Start-Sleep -Seconds 5; Start-Process 'http://127.0.0.1:8000'"
".venv\Scripts\python.exe" -m uvicorn app.main:app --host 127.0.0.1 --port 8000
goto :eof

:erro
echo.
echo Ocorreu um erro. Veja a mensagem acima.
pause
