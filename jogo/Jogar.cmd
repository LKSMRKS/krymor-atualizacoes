@echo off
rem Cinzas da Mare: compila se preciso, abre o servidor e o cliente. Opcoes: -Quem bruno, -NovaConta nome, -CriarAtalho, -Parar
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\jogar.ps1" %*
if errorlevel 1 pause
