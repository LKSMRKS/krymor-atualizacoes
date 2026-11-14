@echo off
rem 13/11 - Tres cidades novas (Vila das Mos, Astralia, Brasaforte) + Merlin em Astralia.
rem Rode com o JOGO FECHADO (o editor do jogo regrava os mapas).
cd /d "%~dp0"
set B=backups\cidades_%date:~6,4%%date:~3,2%%date:~0,2%_%time:~0,2%%time:~3,2%
set B=%B: =0%
mkdir "%B%" 2>nul
for %%F in (world.json shops.json map_porto.json map_vale.json map_mata_dos_enforcados.json map_canions_suplicantes.json map_brejo_da_viuva.json map_coroa_da_geada.json map_mar_de_escoria.json) do copy /y "Content\%%F" "%B%\" >nul
echo Copia de seguranca em %B%
set PY=python
where python >nul 2>nul || set PY=py -3
%PY% Tools\gerar_cidades.py || goto erro
%PY% Tools\gerar_merlin.py || goto erro
echo.
echo Pronto. Agora: dotnet build -c Release  e  .\Jogar.cmd
pause
exit /b 0
:erro
echo ERRO ao gerar. Nada foi perdido: os originais estao em %B%
pause
exit /b 1
