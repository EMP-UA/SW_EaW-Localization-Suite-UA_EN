; ==============================================================================
; Star Wars: Empire at War - Ukrainian Localization Installer Script
; Created by EMP_UA / Створено EMP_UA
; ==============================================================================

#define AppName "Star Wars: Empire at War Українізатор"
#define AppVersion "0.10"
#define AppPublisher "EMP_UA"
#define AppURL "https://t.me/EMP_UA"
; Унікальний ID проєкту (дві дужки {{ обов'язкові) / Unique project ID (two braces {{ are required)
#define AppId "{{796D07FB-4D8C-4C4D-9E81-FBDD9952F835}"
; Офіційний ID гри Star Wars: Empire at War у Steam / Official Steam AppID for Star Wars: Empire at War
#define SteamAppId "32470" 

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}

; --- Налаштування шляхів / Path settings ---
DefaultDirName={code:GetSteamPath}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes

; --- Дозволяємо вибір папки / Allow directory selection ---
DisableDirPage=no
DirExistsWarning=no

; --- Налаштування вихідного файлу / Output file settings ---
OutputDir=Output
OutputBaseFilename=SWEAW_UA_v{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

[Languages]
; Мови інсталятора / Installer languages
Name: "ukrainian"; MessagesFile: "compiler:Languages\Ukrainian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
; Тексти для вибору папки / Texts for directory selection
ukrainian.SelectDirDesc=Виберіть папку, у якій встановлено Star Wars: Empire at War.
ukrainian.SelectDirLabel3=Інсталятор встановить модифікацію локалізації у відповідні підпапки Mods.
english.SelectDirDesc=Select the folder where Star Wars: Empire at War is installed.
english.SelectDirLabel3=The installer will place the localization mod into the respective Mods subfolders.

[Files]
; Базова гра / Base game (GameData -> GameData\Mods\UA_EaW)
Source: "GameData\*"; DestDir: "{app}\GameData\Mods\UA_EaW"; Flags: ignoreversion recursesubdirs createallsubdirs

; Доповнення FOC / Expansion FOC (corruption -> corruption\Mods\UA_EaW_FOC)
Source: "corruption\*"; DestDir: "{app}\corruption\Mods\UA_EaW_FOC"; Flags: ignoreversion recursesubdirs createallsubdirs

; Інші файли (наприклад, Readme) / Other files (e.g., Readme)
Source: "Readme.txt"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

; Шрифти та батник / Fonts and batch script
Source: "Fonts\*"; DestDir: "{app}\UA_Fonts"; Flags: ignoreversion recursesubdirs

[Run]
; Пропонуємо запустити батник для шрифтів після встановлення / Offer to run font batch script post-install
Filename: "{app}\UA_Fonts\SetupFonts.bat"; Description: "Встановити шрифти / Install fonts (Файли збережено в підпапці / Files in: UA_Fonts)"; Flags: postinstall shellexec waituntilterminated

[Registry]
; Записуємо версію в реєстр користувача / Save version to user registry
Root: HKCU; Subkey: "Software\{#AppPublisher}\{#AppName}"; ValueType: string; ValueName: "Version"; ValueData: "{#AppVersion}"; Flags: uninsdeletekey

[Code]
// 1. Пошук гри в Steam / Steam game path detection
function GetSteamPath(Param: String): String;
var
  Path: String;
begin
  if RegQueryStringValue(HKEY_LOCAL_MACHINE, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {#SteamAppId}', 'InstallLocation', Path) or
     RegQueryStringValue(HKEY_CURRENT_USER, 'Software\Valve\Steam', 'SteamPath', Path) then
  begin
    if Pos('Star Wars Empire at War', Path) > 0 then
      Result := Path
    else
      Result := Path + '\steamapps\common\Star Wars Empire at War';
  end
  else
    Result := ExpandConstant('{pf32}\Steam\steamapps\common\Star Wars Empire at War');
end;

// 2. Перевірка версії перед початком / Version check before setup
function InitializeSetup(): Boolean;
var
  OldVersion: String;
begin
  Result := True;
  if RegQueryStringValue(HKCU, 'Software\{#AppPublisher}\{#AppName}', 'Version', OldVersion) then
  begin
    if OldVersion = '{#AppVersion}' then
    begin
      if MsgBox('Українізатор версії / Localization version ' + OldVersion + ' вже встановлено. Бажаєте перевстановити його? / Do you want to reinstall?', mbConfirmation, MB_YESNO) = IDNO then
        Result := False;
    end
    else
    begin
      // Інформація про оновлення або даунгрейд / Update or downgrade info
      MsgBox('Знайдено попередню версію / Found previous version: ' + OldVersion + #13#10 + 'Буде встановлено версію / Will install version: {#AppVersion}', mbInformation, MB_OK);
    end;
  end;
end;

// 3. Перевірка правильності папки (наявність .exe файлів) / Directory validation (check for .exe files)
function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = wpSelectDir then
  begin
    if not (FileExists(ExpandConstant('{app}\GameData\sweaw.exe')) or FileExists(ExpandConstant('{app}\corruption\swfoc.exe'))) then
    begin
      if MsgBox('У вказаній папці не знайдено файлів гри (sweaw.exe або swfoc.exe).' #13#10 #13#10 'Ви впевнені, що хочете встановити файли саме сюди?' #13#10 'Are you sure you want to install here?', mbConfirmation, MB_YESNO) = IDNO then
        Result := False;
    end;
  end;
end;

// 4. ІНФОРМАЦІЯ ПІСЛЯ ВСТАНОВЛЕННЯ / POST-INSTALL INFORMATION
procedure CurStepChanged(CurStep: TSetupStep);
begin
  // Спрацьовує одразу після успішного копіювання всіх файлів / Triggers right after files are copied
  if CurStep = ssPostInstall then
  begin
    MsgBox('Копіювання файлів завершено!' #13#10#13#10 +
           'Якщо ви захочете встановити або видалити шрифти вручну пізніше, спеціальна утиліта (SetupFonts.bat) та самі файли шрифтів збережені у папці гри, в підпапці:' #13#10 +
           '-> UA_Fonts' #13#10#13#10 +
           'File copying complete!' #13#10#13#10 +
           'If you want to manually install or remove fonts later, the utility (SetupFonts.bat) and font files are saved in the game directory, under the subfolder:' #13#10 +
           '-> UA_Fonts', 
           mbInformation, MB_OK);
  end;
end;

// 5. ЛОГІКА ВИДАЛЕННЯ / UNINSTALL LOGIC
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  // Крок 1: Запит на видалення шрифтів ПЕРЕД видаленням файлів / Font removal prompt BEFORE files are deleted
  if CurUninstallStep = usUninstall then
  begin
    if MsgBox('Бажаєте також видалити встановлені шрифти (Star Wars: EaW)?' #13#10#13#10 +
              'Do you also want to remove the installed fonts (Star Wars: EaW)?', 
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      // Запускаємо батник із параметром "remove_all" / Run the batch script with "remove_all" parameter
      Exec(ExpandConstant('{app}\UA_Fonts\SetupFonts.bat'), 'remove_all', '', SW_SHOW, ewWaitUntilTerminated, ResultCode);
    end;
  end;

  // Крок 2: Інформація після завершення / Info after uninstallation is complete
  if CurUninstallStep = usPostUninstall then
  begin
    MsgBox('Локалізацію (модифікацію) успішно видалено.' #13#10#13#10 +
           'Оригінальні файли гри не були змінені, тому гра працюватиме як зазвичай.' #13#10 +
           'Localization (mod) successfully removed. Original game files were untouched.', 
           mbInformation, MB_OK);
  end;
end;