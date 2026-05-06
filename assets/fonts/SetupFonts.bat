@echo off
:: Вмикаємо підтримку UTF-8 для коректного відображення української мови
chcp 65001 >nul

echo ===================================================
echo  Star Wars: EaW - Font Installer / Встановлення шрифтів
echo  Created by EMP_UA
echo ===================================================
echo.

:: Перевірка на наявність прав адміністратора (це обов'язково для шрифтів)
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] Цей скрипт потрібно запустити від імені Адміністратора!
    echo [ERROR] Please run this script as Administrator!
    echo.
    echo Натисніть правою кнопкою миші на файл та оберіть "Запуск від імені адміністратора".
    echo Right-click the file and select "Run as administrator".
    echo.
    pause
    exit /b
)

echo [INFO] Починаємо встановлення шрифтів... / Starting font installation...
echo.

:: Копіювання файлів до системної папки Windows (з приховуванням зайвого тексту)
copy /Y "%~dp0EaW-bold.ttf" "%WINDIR%\Fonts\" >nul
copy /Y "%~dp0EaW-light.ttf" "%WINDIR%\Fonts\" >nul
copy /Y "%~dp0EaW-medium.ttf" "%WINDIR%\Fonts\" >nul
copy /Y "%~dp0EaW-stencil.ttf" "%WINDIR%\Fonts\" >nul

:: Реєстрація шрифтів у реєстрі Windows, щоб система їх "побачила"
reg add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts" /v "EaW Bold (TrueType)" /t REG_SZ /d "EaW-bold.ttf" /f >nul
reg add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts" /v "EaW Light (TrueType)" /t REG_SZ /d "EaW-light.ttf" /f >nul
reg add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts" /v "EaW Medium (TrueType)" /t REG_SZ /d "EaW-medium.ttf" /f >nul
reg add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts" /v "EaW Stencil (TrueType)" /t REG_SZ /d "EaW-stencil.ttf" /f >nul

echo [OK] Шрифти успішно встановлено! / Fonts installed successfully!
echo [INFO] Можливо, знадобиться перезавантажити програми для їх відображення.
echo [INFO] You may need to restart your applications for changes to take effect.
echo.
pause