@echo off
title Arret du serveur - Eleona.exe

:: Vérifie si le script est lancé en administrateur
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Demande des droits administrateur...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

cd /d C:\Windows\System32

echo Arret de Eleona.exe...
taskkill /F /IM Eleona.exe

echo.
echo Verification des processus restants :
tasklist | findstr /I Eleona.exe
if %errorlevel% neq 0 (
    echo Aucun processus Eleona.exe en cours. Serveur arrete.
) else (
    echo ATTENTION : Eleona.exe tourne encore.
)

echo.
pause