#define MyAppName "EasyPOS Gateway"
#define MyAppVersion "1.0.3"
#define MyAppPublisher "ZAZ"
#define MyAppExeName "EasyPOS_Gateway.exe"
#define MyServiceName "EasyPOS.PaymentGateway"

[Setup]
AppId={{9D02A4F4-543A-48DA-89CA-7B587913B373}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\EasyPOS Payment Gateway
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=output
OutputBaseFilename=EasyPOS-Gateway-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#MyAppName}
SetupLogging=yes

[Files]
Source: "..\bin\Release\net6.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\Install-Service.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Uninstall-Service.ps1"; DestDir: "{app}"; Flags: ignoreversion

[Dirs]
Name: "{app}\logs"; Permissions: users-modify

[Run]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Install-Service.ps1"" -Provider ""{code:GetProvider}"" -QueueDestination ""{code:GetQueueDestination}"" -ExecutablePath ""{app}\{#MyAppExeName}""{code:GetStartServiceSwitch}"; StatusMsg: "Registrando el servicio de EasyPOS..."; Flags: runhidden waituntilterminated

[UninstallRun]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Uninstall-Service.ps1"""; Flags: runhidden waituntilterminated; RunOnceId: "RemoveEasyPOSService"

[Code]
var
  ProviderPage: TInputOptionWizardPage;
  DestinationPage: TInputQueryWizardPage;
  StartPage: TInputOptionWizardPage;

procedure InitializeWizard;
begin
  ProviderPage := CreateInputOptionPage(
    wpSelectDir,
    'Proveedor de pagos',
    'Seleccione el proveedor que procesara esta computadora.',
    'Cada instalacion atiende exclusivamente un proveedor.',
    True,
    False);
  ProviderPage.Add('Azul');
  ProviderPage.Add('Cardnet');
  ProviderPage.SelectedValueIndex := 0;

  DestinationPage := CreateInputQueryPage(
    ProviderPage.ID,
    'Destino de la cola SQL',
    'Indique la direccion utilizada por Cardnet.',
    'Para Cardnet identifica la cola SQL y la IP de la terminal. Azul detecta automaticamente la IP LAN del equipo.');
  DestinationPage.Add('Destino:', False);
  DestinationPage.Values[0] := '192.168.10.200';

  StartPage := CreateInputOptionPage(
    DestinationPage.ID,
    'Inicio del servicio',
    'El servicio se configurara con inicio automatico.',
    'Seleccione si desea iniciarlo al completar la instalacion. Confirme primero que las colas esten controladas.',
    False,
    False);
  StartPage.Add('Iniciar el servicio al finalizar');
  StartPage.Values[0] := False;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = DestinationPage.ID then
  begin
    if Trim(DestinationPage.Values[0]) = '' then
    begin
      MsgBox('Debe indicar el destino de la cola SQL.', mbError, MB_OK);
      Result := False;
    end
    else if Pos('"', DestinationPage.Values[0]) > 0 then
    begin
      MsgBox('El destino no puede contener comillas.', mbError, MB_OK);
      Result := False;
    end;
  end;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (PageID = DestinationPage.ID) and (ProviderPage.SelectedValueIndex = 0);
end;

function GetProvider(Param: String): String;
begin
  if ProviderPage.SelectedValueIndex = 0 then
    Result := 'Azul'
  else
    Result := 'Cardnet';
end;

function GetQueueDestination(Param: String): String;
begin
  Result := Trim(DestinationPage.Values[0]);
end;

function GetStartServiceSwitch(Param: String): String;
begin
  if StartPage.Values[0] then
    Result := ' -StartService'
  else
    Result := '';
end;
