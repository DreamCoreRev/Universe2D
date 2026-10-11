@echo off
title Arret du serveur - RPG.exe

:: Vérifie si le script est lancé en administrateur
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Demande des droits administrateur...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

cd /d C:\Windows\System32

echo Arret de RPG.exe...
taskkill /F /IM RPG.exe

echo.
echo Verification des processus restants :
tasklist | findstr /I RPG.exe
if %errorlevel% neq 0 (
    echo Aucun processus RPG.exe en cours. Serveur arrete.
) else (
    echo ATTENTION : RPG.exe tourne encore.
)

echo.
pause