@echo off
setlocal

set "CONFIGURATION=Release"
set "FRAMEWORK=net9.0"
set "RUNTIME=win-x64"
set "PROJECT_ROOT=%~dp0"
set "PROJECT_FILE=%PROJECT_ROOT%Test-00002-Modelos3D.csproj"
set "APP_NAME=Test-00002-Modelos3D"
set "DIST_ROOT=%PROJECT_ROOT%dist"
set "EXE_NAME=%APP_NAME%.exe"
set "GAME_FOLDER=%DIST_ROOT%\%APP_NAME% Windows"
set "PUBLISH_TEMP=%PROJECT_ROOT%bin\%CONFIGURATION%\publish-temp"

echo ===================================================
echo COMPILADOR DE TEST-00002-MODELOS3D (Windows)
echo ===================================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
    echo ERROR: No se encontro el comando 'dotnet'.
    echo Instala el SDK de .NET 9.0 desde: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo.
echo Compilando para Windows (%RUNTIME%)...
echo.

echo Limpiando archivos temporales...
taskkill /f /im "%APP_NAME%.exe" /t >nul 2>nul
timeout /t 1 /nobreak >nul

if exist "%DIST_ROOT%" rmdir /s /q "%DIST_ROOT%"
if exist "%PROJECT_ROOT%bin" rmdir /s /q "%PROJECT_ROOT%bin"
if exist "%PROJECT_ROOT%obj" rmdir /s /q "%PROJECT_ROOT%obj"

mkdir "%PUBLISH_TEMP%"
mkdir "%GAME_FOLDER%"

echo Restaurando proyecto...
dotnet restore "%PROJECT_FILE%" --runtime %RUNTIME%
if errorlevel 1 goto error

echo Generando carpeta portable (Modo estable para MonoGame)...
dotnet publish "%PROJECT_FILE%" -c %CONFIGURATION% -f %FRAMEWORK% --runtime %RUNTIME% --self-contained false -p:PublishSingleFile=false -o "%PUBLISH_TEMP%"
if errorlevel 1 goto error

echo Organizando archivos en 'dist'...
xcopy "%PUBLISH_TEMP%\*" "%GAME_FOLDER%\" /E /I /Y >nul
if errorlevel 1 goto error

if exist "%PROJECT_ROOT%Resources" (
    xcopy "%PROJECT_ROOT%Resources" "%GAME_FOLDER%\Resources\" /E /I /H /Y >nul
    if errorlevel 1 goto error
)

rmdir /s /q "%PUBLISH_TEMP%"

echo.
echo Compilacion lista para Windows:
echo "%GAME_FOLDER%"
echo.
echo Ejecutable del juego:
start "" "%GAME_FOLDER%\%EXE_NAME%"
echo.
exit


:error
echo.
echo Error al compilar el juego.
pause
exit /b 1
