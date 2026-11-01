@echo off
echo ====================================================
echo  TextureSwapper - ProTanki StandaloneLoader Patcher
echo ====================================================
echo.

set "PROTANKI_DIR=C:\Program Files\ProTanki Online"
set "LOADER_SWF=%PROTANKI_DIR%\StandaloneLoader.swf"
set "LOADER_BAK=%PROTANKI_DIR%\StandaloneLoader.swf.bak"
set "PATCHED_SWF=%~dp0StandaloneLoader_patched.swf"

if not exist "%PROTANKI_DIR%" (
    echo [ERROR] ProTanki directory not found at: "%PROTANKI_DIR%"
    pause
    exit /b 1
)

if not exist "%LOADER_BAK%" (
    echo [*] Creating backup: StandaloneLoader.swf.bak
    copy /Y "%LOADER_SWF%" "%LOADER_BAK%" >nul
)

echo [*] Installing patched StandaloneLoader.swf...
copy /Y "%PATCHED_SWF%" "%LOADER_SWF%" >nul

if %errorlevel% equ 0 (
    echo [OK] ProTanki StandaloneLoader successfully patched!
    echo Local HUD overrides in %%APPDATA%%\StandaloneLoader\Local Store\ are now enabled.
) else (
    echo [ERROR] Failed to copy StandaloneLoader.swf. Please make sure this script was Run as Administrator.
)

echo.
pause
