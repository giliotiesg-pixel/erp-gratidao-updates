@echo off
setlocal EnableExtensions
cd /d "%~dp0"
REM Compilacao UNICA do Armazem Gratidao Novo, atualizador e reparo.
REM O fluxo antigo apontava para AMZG.App/AMZG.App.csproj (inexistente).
set "BUILD=592"
if not "%~1"=="" set "BUILD=%~1"
powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "novo-win8\build-unificado.ps1" -Build %BUILD%
set "RESULT=%ERRORLEVEL%"
if not "%RESULT%"=="0" (
  echo Falha na compilacao da build %BUILD%. Nenhum release sera publicado.
  exit /b %RESULT%
)
echo Instalador unico e atualizacao validados em dist\
exit /b 0
