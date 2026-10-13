@echo off
title BPKMCH Hospital Records AI - IIS Deployment
echo ==========================================================
echo  Deploying Hospital Digitization System to IIS Server
echo  Target Domain: http://www.digital.com
echo ==========================================================
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup-iis-deployment.ps1"

pause
