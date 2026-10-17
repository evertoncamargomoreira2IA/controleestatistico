@echo off
rem Cria um usuario administrador. Rode com o iniciar.bat ja executado ao menos uma vez.
cd /d "%~dp0"
if not exist ".venv\Scripts\python.exe" (
    echo Rode primeiro o iniciar.bat para criar o ambiente.
    pause
    exit /b 1
)
".venv\Scripts\python.exe" -m alembic upgrade head
".venv\Scripts\python.exe" scripts\create_admin.py
pause
