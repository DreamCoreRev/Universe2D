@echo off
title Refresh Cache - Universe2D

:: Vérifie si le script est lancé en administrateur
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Demande des droits administrateur...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

cd /d C:\Servers\Sources\Universe2D\BuildPC

echo [1/2] Lancement en batchmode...
start "" RPG.exe -batchmode -nographics -logFile C:\Servers\Sources\Universe2D\server.log

echo [2/2] Lancement du client...
start "" RPG.exe -logFile C:\Servers\Sources\Universe2D\client.log

echo.
echo Termine.
pause