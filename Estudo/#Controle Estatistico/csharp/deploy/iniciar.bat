@echo off
rem QMS / CEP (versao C#) - ambiente de teste local.
rem Usa o .NET 10 ja instalado no Windows; os dados ficam em app\data\qms.db.
setlocal
cd /d "%~dp0app"

set ASPNETCORE_ENVIRONMENT=Development
set ASPNETCORE_URLS=http://127.0.0.1:8080

echo.
echo  QMS - CEP (C#)  -  http://127.0.0.1:8080
echo  Primeiro acesso: usuario admin / senha admin (o sistema pede para trocar a senha)
echo  NAO feche esta janela enquanto estiver usando o sistema (Ctrl+C para parar).
echo.

rem Abre o navegador alguns segundos depois, quando o servidor ja estiver no ar.
start "" /min powershell -NoProfile -Command "Start-Sleep -Seconds 5; Start-Process 'http://127.0.0.1:8080'"
Qms.Web.exe
if errorlevel 1 (
    echo.
    echo Ocorreu um erro. Veja a mensagem acima.
    pause
)
