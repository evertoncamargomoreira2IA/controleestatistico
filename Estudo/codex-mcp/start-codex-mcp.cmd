@echo off
setlocal
set "NODE="
for /f "delims=" %%P in ('where node 2^>nul') do if not defined NODE set "NODE=%%P"
if not defined NODE for /f "delims=" %%P in ('dir /b /s /o:-d "%LOCALAPPDATA%\OpenAI\Codex\runtimes\node.exe" 2^>nul') do if not defined NODE set "NODE=%%P"
if not defined NODE (
  echo [codex-bridge] node.exe nao encontrado 1>&2
  exit /b 1
)
"%NODE%" "%~dp0codex-mcp.js"
