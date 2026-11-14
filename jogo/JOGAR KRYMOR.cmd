@echo off
rem KRYMOR: fecha o servidor antigo e abre o jogo de novo (ja com a atualizacao). Clique duas vezes.
cd /d "%~dp0"
echo Fechando o servidor antigo...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\jogar.ps1" -Parar
echo Abrindo o KRYMOR...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\jogar.ps1" -Rede
if errorlevel 1 pause
