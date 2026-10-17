@echo off
rem Cria a hierarquia de exemplo (Montagem / Produto A / ... / Altura) e as Definicoes iniciais.
cd /d "%~dp0"
if not exist ".venv\Scripts\python.exe" (
    echo Rode primeiro o iniciar.bat para criar o ambiente.
    pause
    exit /b 1
)
".venv\Scripts\python.exe" -m alembic upgrade head
".venv\Scripts\python.exe" scripts\seed_demo.py
echo Dados de exemplo criados.
pause
