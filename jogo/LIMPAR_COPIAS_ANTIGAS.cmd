@echo off
rem 13/11 - Apaga so copias repetidas (pedido do dono). Mostra a lista e pergunta antes.
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\limpar_copias.ps1"
pause
