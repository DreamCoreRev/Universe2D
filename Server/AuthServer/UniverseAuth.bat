@echo off
title AuthServer - Universe2D

:: Vérifie si le script est lancé en administrateur
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Demande des droits administrateur...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

:: Lancement du serveur
cd /d C:\Servers\Sources\Universe2D\Server\AuthServer
dotnet run

pause