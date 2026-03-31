@echo off
:: ============================================================
:: Stop Unneeded Windows Services
:: Run as Administrator
:: ============================================================

echo Stopping unneeded services...
echo.

:: ---- Telemetry / Diagnostics / Tracking ----
net stop "DiagTrack"           2>nul & echo [OK] Connected User Experiences and Telemetry
net stop "DPS"                 2>nul & echo [OK] Diagnostic Policy Service
net stop "WdiSystemHost"       2>nul & echo [OK] Diagnostic System Host
net stop "dmwappushservice"    2>nul & echo [OK] WAP Push Message Routing (telemetry)
net stop "MSFT_DUCS"           2>nul & echo [OK] Microsoft Usage and Quality Insights

:: ---- Windows Update / Delivery Optimization ----
:: net stop "DoSvc"               2>nul & echo [OK] Delivery Optimization
:: net stop "UsoSvc"              2>nul & echo [OK] Update Orchestrator Service
:: net stop "TrustedInstaller"    2>nul & echo [OK] Windows Modules Installer
:: net stop "BITS"                2>nul & echo [OK] Background Intelligent Transfer Service
:: net stop "wuauserv"            2>nul & echo [OK] Windows Update

:: ---- Windows Search / Indexing ----
net stop "WSearch"             2>nul & echo [OK] Windows Search

:: ---- Microsoft Store / AppX ----
net stop "WSAIFabricSvc"       2>nul & echo [OK] WSAIFabricSvc
net stop "AppXSVC"             2>nul & echo [OK] AppX Deployment Service
net stop "ClipSVC"             2>nul & echo [OK] Client License Service (ClipSVC)
net stop "InstallService"      2>nul & echo [OK] Microsoft Store Install Service

:: ---- Microsoft Account / Passport ----
:: net stop "wlidsvc"             2>nul & echo [OK] Microsoft Account Sign-in Assistant
:: net stop "NgcSvc"              2>nul & echo [OK] Microsoft Passport
:: net stop "NgcCtnrSvc"          2>nul & echo [OK] Microsoft Passport Container

:: ---- Print Spooler (disable if no printer) ----
net stop "Spooler"             2>nul & echo [OK] Print Spooler

:: ---- Geolocation ----
:: net stop "lfsvc"               2>nul & echo [OK] Geolocation Service

:: ---- Fax / ICS / SSTP (rarely needed) ----
net stop "RasMan"              2>nul & echo [OK] Remote Access Connection Manager
net stop "SharedAccess"        2>nul & echo [OK] Internet Connection Sharing (ICS)
net stop "SstpSvc"             2>nul & echo [OK] Secure Socket Tunneling Protocol Service

:: ---- Phone / Mobile device link services ----
net stop "PhoneSvc"            2>nul & echo [OK] Phone Service (if present)

:: ---- SSDP / Discovery (UPnP, rarely needed on desktop) ----
net stop "SSDPSRV"             2>nul & echo [OK] SSDP Discovery
net stop "FDResPub"            2>nul & echo [OK] Function Discovery Resource Publication
net stop "fdPHost"             2>nul & echo [OK] Function Discovery Provider Host

:: ---- Windows Health / Compatibility Assistant ----
net stop "PcaSvc"              2>nul & echo [OK] Program Compatibility Assistant Service
net stop "WHS"                 2>nul & echo [OK] Windows Health and Optimized Experiences
net stop "IAS"                 2>nul & echo [OK] Inventory and Compatibility Appraisal

:: ---- HV Host (only needed for Hyper-V hosts) ----
net stop "HvHost"              2>nul & echo [OK] HV Host Service

:: ---- SysMain (Superfetch — optional, often safe to stop on SSDs) ----
net stop "SysMain"             2>nul & echo [OK] SysMain (Superfetch)

:: ---- Volume Shadow Copy / Software Shadow Copy ----
net stop "VSS"                 2>nul & echo [OK] Volume Shadow Copy
net stop "swprv"               2>nul & echo [OK] Microsoft Software Shadow Copy Provider

:: ---- TCP/IP NetBIOS Helper (legacy, rarely needed) ----
net stop "lmhosts"             2>nul & echo [OK] TCP/IP NetBIOS Helper

:: ---- WMI Performance Adapter (only needed during benchmarks) ----
net stop "wmiApSrv"            2>nul & echo [OK] WMI Performance Adapter

:: ---- Radio Management (disable if no wireless/BT concerns) ----
net stop "RmSvc"               2>nul & echo [OK] Radio Management Service

:: ---- Shell Hardware Detection (autoplay) ----
net stop "ShellHWDetection"    2>nul & echo [OK] Shell Hardware Detection

echo.
echo ============================================================
echo Done. Some services may have already been stopped (errors
echo above are normal and can be ignored).
echo ============================================================
pause
