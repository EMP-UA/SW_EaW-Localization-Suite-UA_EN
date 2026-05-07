@echo off
chcp 65001 >nul
title Star Wars: EaW - Font Tool by EMP_UA

:: Перехоплення параметрів для автоматичного продовження після запиту UAC
:: Intercept parameters to automatically continue after UAC prompt
if "%~1"=="install_global" goto :DoInstallGlobal
if "%~1"=="remove_all" goto :DoRemoveAll

:: --- 1. ОТРИМАННЯ ДАНИХ СИСТЕМИ / GET SYSTEM DATA ---
net session >nul 2>&1
if %errorLevel% == 0 (set "IS_ADMIN_VAR=YES") else (set "IS_ADMIN_VAR=NO")

:: Зчитуємо версію Windows / Read Windows version (e.g., 10.0.19045)
for /f "tokens=4,5,6 delims=[]. " %%a in ('ver') do (
    set "WIN_VER_MAJOR=%%a"
    set "WIN_VER_MINOR=%%b"
    set "WIN_VER_BUILD=%%c"
)

:: Визначаємо комерційну назву Windows (Win 11 починається зі збірки 22000)
:: Determine Windows commercial name (Win 11 starts at build 22000)
set "WIN_NAME=Windows %WIN_VER_MAJOR%.%WIN_VER_MINOR%"
if %WIN_VER_MAJOR% EQU 10 (
    if %WIN_VER_BUILD% GEQ 22000 (
        set "WIN_NAME=Windows 11"
    ) else (
        set "WIN_NAME=Windows 10"
    )
)

:: Перевіряємо, чи підтримується встановлення без прав адміна (збірка 17763+)
:: Check if installation without admin rights is supported (build 17763+)
set "IS_NEW_WIN=NO"
if %WIN_VER_MAJOR% GTR 10 set "IS_NEW_WIN=YES"
if %WIN_VER_MAJOR% EQU 10 if %WIN_VER_BUILD% GEQ 17763 set "IS_NEW_WIN=YES"

:: --- 2. ГОЛОВНЕ МЕНЮ / MAIN MENU ---
:MainMenu
cls
echo ===================================================
echo  Star Wars: EaW - Font Tool / Утиліта для шрифтів
echo  Created by EMP_UA
echo ===================================================
echo [INFO] Запущено з правами Адмін. / Run as Admin:
echo        =^> %IS_ADMIN_VAR%
echo [INFO] Операційна система / Operating System:
echo        =^> %WIN_NAME% (Збірка/Build: %WIN_VER_BUILD%)
echo ===================================================
echo.
echo Оберіть дію / Choose an action:
echo 1 - Встановити шрифти / Install fonts
echo 2 - Видалити шрифти / Remove fonts
echo 3 - Вихід / Exit
echo.
set /p main_choice="Ваш вибір / Your choice (1-3): "

if "%main_choice%"=="1" goto :InstallFlow
if "%main_choice%"=="2" goto :RemoveFlow
if "%main_choice%"=="3" exit /b
goto :MainMenu

:: --- 3. ЛОГІКА ВСТАНОВЛЕННЯ / INSTALL LOGIC ---
:InstallFlow
echo.
echo --- ВСТАНОВЛЕННЯ ШРИФТІВ / FONT INSTALLATION ---
echo [INFO] Ручне встановлення (Windows 10/11):
echo [INFO] Manual installation (Windows 10/11):
echo  - ПКМ по файлу -^> Встановити / Інсталювати для всіх
echo  - Right-click file -^> Install / Install for all users
echo [INFO] Ручне встановлення (Windows 7/8):
echo [INFO] Manual installation (Windows 7/8):
echo  - ПКМ по файлу -^> Встановити АБО скопіювати у Панель керування -^> Шрифти
echo  - Right-click -^> Install OR copy to Control Panel -^> Fonts
echo Детальніше / More info: https://support.microsoft.com/windows/manage-fonts-in-windows-f12d0657-2fc8-7613-c76f-88d043b334b8
echo.

if "%IS_NEW_WIN%"=="YES" goto :PromptNewWin
goto :PromptOldWin

:PromptNewWin
echo [INFO] Ваша версія ОС (збірка %WIN_VER_BUILD%^) ПІДТРИМУЄ встановлення без прав адміна.
echo [INFO] Your OS version (build %WIN_VER_BUILD%^) SUPPORTS installation without admin rights.
echo.
echo 1 - ТАК, локальному користувачу / YES, for local user (Без адмін. прав / No admin rights)
echo 2 - НІ, для всіх користувачів / NO, for all users (Потребує адмін. прав / Requires admin rights)
echo 3 - Скасувати / Cancel (Вихід у меню / Return to menu)
echo.
set /p inst_choice="Ваш вибір / Your choice (1-3): "

if "%inst_choice%"=="1" goto :DoInstallLocal
if "%inst_choice%"=="2" goto :AskGlobalInstall
if "%inst_choice%"=="3" goto :MainMenu
goto :PromptNewWin

:PromptOldWin
echo [INFO] Ваша версія ОС (збірка %WIN_VER_BUILD%^) застаріла. / Your OS version is outdated.
echo [INFO] Встановлення можливе ТІЛЬКИ для всіх користувачів (потребує прав Адміністратора).
echo [INFO] Installation is ONLY possible for all users (requires Administrator rights).
goto :AskGlobalInstall

:AskGlobalInstall
echo.
echo [УВАГА / WARNING] Встановлення для всіх користувачів потребує прав Адміністратора!
echo [УВАГА / WARNING] Installation for all users requires Administrator rights!
set /p confirm="Продовжити з правами адміна? / Proceed as Admin? (Y / N): "
if /i not "%confirm%"=="Y" goto :MainMenu

if "%IS_ADMIN_VAR%"=="YES" (
    goto :DoInstallGlobal
) else (
    echo.
    echo [INFO] Запитуємо права адміністратора... Підтвердіть у вікні UAC.
    echo [INFO] Requesting Administrator rights... Please confirm in the UAC prompt.
    powershell -Command "Start-Process cmd -ArgumentList '/c \"\"%~dpnx0\"\" install_global' -Verb RunAs"
    exit /b
)

:DoInstallLocal
echo.
echo [INFO] Встановлюємо локально / Installing locally...
if not exist "%LOCALAPPDATA%\Microsoft\Windows\Fonts" mkdir "%LOCALAPPDATA%\Microsoft\Windows\Fonts"
copy /Y "%~dp0EaW-bold.ttf" "%LOCALAPPDATA%\Microsoft\Windows\Fonts\" >nul
copy /Y "%~dp0EaW-light.ttf" "%LOCALAPPDATA%\Microsoft\Windows\Fonts\" >nul
copy /Y "%~dp0EaW-medium.ttf" "%LOCALAPPDATA%\Microsoft\Windows\Fonts\" >nul
copy /Y "%~dp0EaW-stencil.ttf" "%LOCALAPPDATA%\Microsoft\Windows\Fonts\" >nul

reg add "HKCU\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts" /v "EaW Bold (TrueType)" /t REG_SZ /d "%LOCALAPPDATA%\Microsoft\Windows\Fonts\EaW-bold.ttf" /f >nul
reg add "HKCU\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts" /v "EaW Light (TrueType)" /t REG_SZ /d "%LOCALAPPDATA%\Microsoft\Windows\Fonts\EaW-light.ttf" /f >nul
reg add "HKCU\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts" /v "EaW Medium (TrueType)" /t REG_SZ /d "%LOCALAPPDATA%\Microsoft\Windows\Fonts\EaW-medium.ttf" /f >nul
reg add "HKCU\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts" /v "EaW Stencil (TrueType)" /t REG_SZ /d "%LOCALAPPDATA%\Microsoft\Windows\Fonts\EaW-stencil.ttf" /f >nul
echo [OK] Шрифти успішно встановлено ДЛЯ ВАС! / Fonts successfully installed FOR YOU!
echo [УВАГА / WARNING] Можливо, знадобиться перезавантажити програму або ПК для їх відображення.
echo [УВАГА / WARNING] You may need to restart the application or PC for fonts to appear.
pause
goto :MainMenu

:DoInstallGlobal
echo.
echo [INFO] Встановлюємо глобально / Installing globally...
copy /Y "%~dp0EaW-bold.ttf" "%WINDIR%\Fonts\" >nul
copy /Y "%~dp0EaW-light.ttf" "%WINDIR%\Fonts\" >nul
copy /Y "%~dp0EaW-medium.ttf" "%WINDIR%\Fonts\" >nul
copy /Y "%~dp0EaW-stencil.ttf" "%WINDIR%\Fonts\" >nul

reg add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts" /v "EaW Bold (TrueType)" /t REG_SZ /d "EaW-bold.ttf" /f >nul
reg add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts" /v "EaW Light (TrueType)" /t REG_SZ /d "EaW-light.ttf" /f >nul
reg add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts" /v "EaW Medium (TrueType)" /t REG_SZ /d "EaW-medium.ttf" /f >nul
reg add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts" /v "EaW Stencil (TrueType)" /t REG_SZ /d "EaW-stencil.ttf" /f >nul
echo [OK] Шрифти успішно встановлено ДЛЯ ВСІХ! / Fonts successfully installed FOR ALL USERS!
echo [УВАГА / WARNING] Можливо, знадобиться перезавантажити програму або ПК для їх відображення.
echo [УВАГА / WARNING] You may need to restart the application or PC for fonts to appear.
pause
if "%~1"=="install_global" exit /b
goto :MainMenu

:: --- 4. ЛОГІКА ВИДАЛЕННЯ / REMOVE LOGIC ---
:RemoveFlow
echo.
echo --- ВИДАЛЕННЯ ШРИФТІВ / FONT REMOVAL ---
echo [INFO] Ручне видалення (Windows 10/11):
echo [INFO] Manual removal (Windows 10/11):
echo  - Параметри -^> Персоналізація -^> Шрифти -^> Знайти "EaW" -^> Видалити
echo  - Settings -^> Personalization -^> Fonts -^> Search for "EaW" -^> Uninstall
echo [INFO] Ручне видалення (Windows 7/8):
echo [INFO] Manual removal (Windows 7/8):
echo  - Панель керування -^> Шрифти -^> Знайти "EaW" -^> Видалити
echo  - Control Panel -^> Fonts -^> Search for "EaW" -^> Delete
echo Детальніше / More info: https://support.microsoft.com/windows/manage-fonts-in-windows-f12d0657-2fc8-7613-c76f-88d043b334b8
echo.
echo [УВАГА / WARNING] Для повного очищення скриптом потрібні права Адміністратора.
echo [УВАГА / WARNING] Full cleanup via script requires Administrator rights.
set /p confirm="Продовжити видалення? / Continue with removal? (Y / N): "
if /i not "%confirm%"=="Y" goto :MainMenu

if "%IS_ADMIN_VAR%"=="YES" (
    goto :DoRemoveAll
) else (
    echo.
    echo [INFO] Запитуємо права адміністратора... / Requesting Administrator rights...
    powershell -Command "Start-Process cmd -ArgumentList '/c \"\"%~dpnx0\"\" remove_all' -Verb RunAs"
    exit /b
)

:DoRemoveAll
echo.
echo [INFO] Видаляємо записи з реєстру / Removing registry entries...
reg delete "HKCU\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts" /v "EaW Bold (TrueType)" /f >nul 2>&1
reg delete "HKCU\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts" /v "EaW Light (TrueType)" /f >nul 2>&1
reg delete "HKCU\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts" /v "EaW Medium (TrueType)" /f >nul 2>&1
reg delete "HKCU\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts" /v "EaW Stencil (TrueType)" /f >nul 2>&1

reg delete "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts" /v "EaW Bold (TrueType)" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts" /v "EaW Light (TrueType)" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts" /v "EaW Medium (TrueType)" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts" /v "EaW Stencil (TrueType)" /f >nul 2>&1

echo [INFO] Видаляємо файли шрифтів / Deleting font files...
del /f /q "%LOCALAPPDATA%\Microsoft\Windows\Fonts\EaW-*.ttf" >nul 2>&1
del /f /q "%WINDIR%\Fonts\EaW-*.ttf" >nul 2>&1

echo [OK] Записи реєстру очищено! / Registry entries cleared!
echo [УВАГА / WARNING] Якщо файли використовуються системою (in use), 
echo [УВАГА / WARNING] If the files are currently in use by the system,
echo вони зникнуть з папки Fonts лише ПІСЛЯ ПЕРЕЗАВАНТАЖЕННЯ ПК.
echo they will disappear from the Fonts folder ONLY AFTER A PC RESTART.
pause
if "%~1"=="remove_all" exit /b
goto :MainMenu