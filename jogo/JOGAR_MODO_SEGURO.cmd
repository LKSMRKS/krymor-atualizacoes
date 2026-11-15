@echo off
rem KRYMOR em MODO SEGURO (OpenGL): para quando o jogo fecha sozinho ou fica com tela preta.
rem Depois de usar uma vez, o Jogar.cmd normal continua no modo seguro (apague Tools\modo_seguro.txt para voltar).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\jogar.ps1" -ModoSeguro %*
if errorlevel 1 pause
