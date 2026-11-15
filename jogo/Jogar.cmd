@echo off
rem Cinzas da Mare: compila se preciso, abre o servidor e o cliente. Opcoes: -Quem bruno, -NovaConta nome, -CriarAtalho, -Parar
rem 14/11: se o jogar.ps1 vier quebrado (erro de digitacao numa atualizacao), no PC do amigo baixa o certo do GitHub
if exist "%~dp0Client\amigo.txt" powershell -NoProfile -ExecutionPolicy Bypass -Command "$e=$null; $t=$null; [void][System.Management.Automation.Language.Parser]::ParseFile('%~dp0Tools\jogar.ps1',[ref]$t,[ref]$e); if ($e.Count -gt 0) { Write-Host 'Consertando o jogar.ps1...'; [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -UseBasicParsing 'https://raw.githubusercontent.com/LKSMRKS/krymor-atualizacoes/main/jogo/Tools/jogar.ps1' -OutFile '%~dp0Tools\jogar.ps1' }"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Tools\jogar.ps1" %*
if errorlevel 1 pause
