@echo off
setlocal EnableExtensions
cd /d "%~dp0"
REM Uma unica numeração em novo-win8\VERSAO.txt: 1.1.593, 1.2.594...
REM O gerador valida versao de PDV, instalador, atualizador, ZIP e manifesto.
powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "novo-win8\build-unificado.ps1" -Build 0
set "RESULT=%ERRORLEVEL%"
if not "%RESULT%"=="0" (
  echo ERRO: arquivos com numeracao diferente ou falha ao compilar. Nenhum release deve ser publicado.
  exit /b %RESULT%
)
echo Pacote unificado pronto em dist\. Todos os arquivos usam a versao de VERSAO.txt.
exit /b 0
