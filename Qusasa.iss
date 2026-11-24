#define AppName "قصاصة"
#define AppVersion "1.29.0"
#define DisplayVersion "1.29"
#define Publisher "صلاح الدين الدروبي"
#define AppExe "Qusasa.exe"
#define RunKey "Software\Microsoft\Windows\CurrentVersion\Run"
#define StartupName "Qusasa"
#define RunningMutex "Local\SalahDin.Qusasa.Setup"

[Setup]
AppId={{CF398BDC-5347-4D5D-8F60-AF48B8EEAE38}
AppName={cm:AppName}
AppVersion={#AppVersion}
AppPublisher={#Publisher}
AppPublisherURL=https://github.com/salahadoroobi
AppSupportURL=https://github.com/salahadoroobi
DefaultDirName={localappdata}\Programs\Qusasa
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
MinVersion=10.0
SetupIconFile=Qusasa.ico
UninstallDisplayIcon={app}\{#AppExe}
VersionInfoCompany={#Publisher}
VersionInfoCopyright=© 2026 {#Publisher}. جميع الحقوق محفوظة.
VersionInfoDescription=مثبّت قصاصة
VersionInfoProductName=قصاصة | Qusasa
VersionInfoVersion={#AppVersion}
OutputDir=bin\Installer
OutputBaseFilename=Qusasa-1.29-Engine
WizardStyle=modern dark windows11 hidebevels
WizardSizePercent=142,137
WizardBackColor=#18151F
WizardImageFile=SetupSide.png
WizardImageBackColor=#211932
WizardSmallImageFile=
WizardSmallImageBackColor=#241B35
DisableWelcomePage=yes
ShowLanguageDialog=no
CloseApplications=no
RestartApplications=no

[Languages]
Name: "ar"; MessagesFile: "compiler:Languages\Arabic.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "ja"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "zh"; MessagesFile: "ChineseSimplified.isl"

[CustomMessages]
ar.DesktopShortcut=إنشاء اختصار «قصاصة» على سطح المكتب
ar.StartWithWindows=تشغيل «قصاصة» في الخلفية عند تسجيل الدخول إلى ويندوز
ar.LaunchQusasa=فتح قصاصة
ar.RuntimeMissing=يتطلب «قصاصة» تثبيت ‎.NET Framework 4.8 أو إصدار أحدث.
ar.ConsentClose=قصاصة قيد التشغيل. احفظ أي تعديل مفتوح. هل توافق على إغلاق قصاصة تلقائيًا لمتابعة التحديث؟
ar.Postponed=بقي تطبيق قصاصة مفتوحًا. يمكنك إعادة محاولة التحديث متى أردت.
ar.ClosureFailed=تعذّر إغلاق قصاصة تلقائيًا. اختر «إنهاء قصاصة» من منطقة الإعلام ثم أعد المحاولة.
ar.CloseBeforeUninstall=اختر «إنهاء قصاصة» من منطقة الإعلام قبل إزالة التطبيق.
en.DesktopShortcut=Create a Qusasa desktop shortcut
fr.DesktopShortcut=Créer un raccourci Qusasa sur le bureau
ja.DesktopShortcut=デスクトップに Qusasa のショートカットを作成
zh.DesktopShortcut=创建 Qusasa 桌面快捷方式
en.StartWithWindows=Start Qusasa in the background when you sign in
fr.StartWithWindows=Lancer Qusasa en arrière-plan à la connexion
ja.StartWithWindows=サインイン時に Qusasa をバックグラウンドで起動
zh.StartWithWindows=登录 Windows 时在后台启动 Qusasa
en.LaunchQusasa=Open Qusasa
fr.LaunchQusasa=Ouvrir Qusasa
ja.LaunchQusasa=Qusasa を開く
zh.LaunchQusasa=打开 Qusasa
en.RuntimeMissing=Qusasa requires .NET Framework 4.8 or later.
fr.RuntimeMissing=Qusasa nécessite .NET Framework 4.8 ou version ultérieure.
ja.RuntimeMissing=Qusasa には .NET Framework 4.8 以降が必要です。
zh.RuntimeMissing=Qusasa 需要 .NET Framework 4.8 或更高版本。
en.ConsentClose=Qusasa is running. Save any open edits. Close Qusasa to continue the update?
fr.ConsentClose=Qusasa est ouvert. Enregistrez vos modifications. Fermer Qusasa pour poursuivre la mise à jour ?
ja.ConsentClose=Qusasa は実行中です。変更を保存してください。更新のために Qusasa を閉じますか？
zh.ConsentClose=Qusasa 正在运行。请先保存修改。是否关闭 Qusasa 以继续更新？
en.Postponed=Qusasa is still running. You can retry the update later.
fr.Postponed=Qusasa est toujours ouvert. Vous pourrez réessayer plus tard.
ja.Postponed=Qusasa はまだ実行中です。後で再試行できます。
zh.Postponed=Qusasa 仍在运行。您可以稍后重试更新。
en.ClosureFailed=Could not close Qusasa. Quit it from the notification area and try again.
fr.ClosureFailed=Impossible de fermer Qusasa. Quittez-le depuis la zone de notification, puis réessayez.
ja.ClosureFailed=Qusasa を閉じられませんでした。通知領域から終了して再試行してください。
zh.ClosureFailed=无法关闭 Qusasa。请从通知区域退出后重试。
en.CloseBeforeUninstall=Quit Qusasa from the notification area before uninstalling.
fr.CloseBeforeUninstall=Quittez Qusasa depuis la zone de notification avant de le désinstaller.
ja.CloseBeforeUninstall=アンインストールの前に通知領域から Qusasa を終了してください。
zh.CloseBeforeUninstall=卸载前，请从通知区域退出 Qusasa。
en.IntroTitle=Your clipboard, organized
fr.IntroTitle=Votre presse-papiers, organisé
ja.IntroTitle=クリップボードを整理
zh.IntroTitle=井然有序的剪贴板
ar.IntroTitle=حافظتك في مكان واحد
en.IntroSubtitle=Discover Qusasa for Windows 10 and 11
fr.IntroSubtitle=Découvrez Qusasa pour Windows 10 et 11
ja.IntroSubtitle=Windows 10 / 11 用 Qusasa
zh.IntroSubtitle=了解适用于 Windows 10 和 11 的 Qusasa
ar.IntroSubtitle=تعرّف على قصاصة لويندوز 10 و11
en.IntroBody=Find copied text, images, links and paths in your local history.%nCapture a window and extract Arabic or English text with on-device OCR.
fr.IntroBody=Retrouvez vos textes, images, liens et chemins dans l’historique local.%nCapturez une fenêtre et extrayez le texte arabe ou anglais sur votre appareil.
ja.IntroBody=テキスト、画像、リンク、パスをローカル履歴から検索できます。%nウィンドウを撮影し、端末上でアラビア語や英語の文字を認識できます。
zh.IntroBody=在本机历史记录中查找文本、图片、链接和路径。%n捕获窗口，并在本机识别阿拉伯语或英语文本。
ar.IntroBody=استعد النصوص والصور والروابط والمسارات من سجلك المحلي.%nالتقط نافذة واستخرج النص العربي أو الإنجليزي على جهازك.

en.StageOptions=Choose how to launch Qusasa
fr.StageOptions=Choisissez comment lancer Qusasa
ja.StageOptions=起動方法を選択
zh.StageOptions=选择启动方式
ar.StageOptions=اختر طريقة فتح قصاصة
en.StageReady=Ready to install
fr.StageReady=Prêt à installer
ja.StageReady=インストールの準備完了
zh.StageReady=准备安装
ar.StageReady=جاهز للتثبيت
en.StageInstall=Installing Qusasa
fr.StageInstall=Installation de Qusasa
ja.StageInstall=クサーサをインストール中
zh.StageInstall=正在安装库萨萨
ar.StageInstall=جارٍ تثبيت قصاصة
en.StageDone=Qusasa is ready
fr.StageDone=Qusasa est prêt
ja.StageDone=クサーサの準備ができました
zh.StageDone=库萨萨已就绪
ar.StageDone=قصاصة جاهز للانطلاق
en.AppName=Qusasa
fr.AppName=Qusasa
ja.AppName=クサーサ
zh.AppName=库萨萨
ar.AppName=قصاصة
en.StepIntro=Introduction
fr.StepIntro=Découverte
ja.StepIntro=はじめに
zh.StepIntro=了解应用
ar.StepIntro=التعريف
en.StepOptions=Options
fr.StepOptions=Options
ja.StepOptions=設定
zh.StepOptions=设置
ar.StepOptions=الخيارات
en.StepReview=Review
fr.StepReview=Vérification
ja.StepReview=確認
zh.StepReview=确认
ar.StepReview=المراجعة
en.StepInstall=Installation
fr.StepInstall=Installation
ja.StepInstall=インストール
zh.StepInstall=安装
ar.StepInstall=التثبيت
en.StepDone=Finish
fr.StepDone=Terminer
ja.StepDone=完了
zh.StepDone=完成
ar.StepDone=الإكمال
en.OptionsLead=Choose the shortcuts that fit the way you work.
fr.OptionsLead=Choisissez les raccourcis adaptés à votre usage.
ja.OptionsLead=使い方に合ったショートカットを選択してください。
zh.OptionsLead=选择适合您工作方式的快捷方式。
ar.OptionsLead=حدّد الاختصارات التي تناسب طريقة عملك.
en.ReviewLead=Check your choices before installing.
fr.ReviewLead=Vérifiez vos choix avant l’installation.
ja.ReviewLead=インストール前に設定を確認してください。
zh.ReviewLead=安装前请确认您的选择。
ar.ReviewLead=راجع خياراتك قبل التثبيت.
en.InstallLead=Preparing the clipboard history, screen capture and offline scanner.
fr.InstallLead=Préparation de l’historique, de la capture d’écran et de la reconnaissance hors ligne.
ja.InstallLead=履歴、画面キャプチャ、オフライン文字認識を準備しています。
zh.InstallLead=正在准备剪贴板历史、截图和离线文字识别。
ar.InstallLead=يُجهَّز سجل الحافظة وأداة الالتقاط والماسح المحلي.
en.DoneLead=Open Qusasa and copy your first clip. Press Ctrl + Shift + Space to open your history.
fr.DoneLead=Ouvrez Qusasa et copiez votre premier élément. Ctrl + Maj + Espace ouvre l’historique.
ja.DoneLead=クサーサを開いて最初の項目をコピーしましょう。Ctrl + Shift + Space で履歴を開けます。
zh.DoneLead=打开库萨萨并复制第一个条目。按 Ctrl + Shift + Space 打开历史。
ar.DoneLead=افتح قصاصة وانسخ أول عنصر. استخدم Ctrl + Shift + Space لفتح السجل.
en.ConsentTitle=Close Qusasa to update?
fr.ConsentTitle=Fermer Qusasa pour la mise à jour ?
ja.ConsentTitle=更新のためにクサーサを閉じますか？
zh.ConsentTitle=关闭库萨萨以更新？
ar.ConsentTitle=إغلاق قصاصة لمتابعة التحديث؟
en.ConsentAccept=Close and update
fr.ConsentAccept=Fermer et mettre à jour
ja.ConsentAccept=閉じて更新
zh.ConsentAccept=关闭并更新
ar.ConsentAccept=إغلاق وتحديث
en.ConsentLater=Later
fr.ConsentLater=Plus tard
ja.ConsentLater=後で
zh.ConsentLater=稍后
ar.ConsentLater=لاحقًا
en.Enabled=On
fr.Enabled=Activé
ja.Enabled=オン
zh.Enabled=开启
ar.Enabled=مفعّل
en.Disabled=Off
fr.Disabled=Désactivé
ja.Disabled=オフ
zh.Disabled=关闭
ar.Disabled=متوقّف

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopShortcut}"
Name: "startup"; Description: "{cm:StartWithWindows}"; Flags: unchecked

[Files]
Source: "SetupFeature.bmp"; Flags: dontcopy noencryption
Source: "bin\Release\net48\Qusasa.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net48\Qusasa.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net48\ocr\*"; DestDir: "{app}\ocr"; Flags: ignoreversion recursesubdirs
Source: "bin\Release\net48\tessdata\*"; DestDir: "{app}\tessdata"; Flags: ignoreversion recursesubdirs
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon

[InstallDelete]
Type: files; Name: "{autodesktop}\{#AppName}.lnk"; Tasks: not desktopicon; Check: OwnsDesktopShortcut

[Registry]
Root: HKCU; Subkey: "{#RunKey}"; ValueType: string; ValueName: "{#StartupName}"; ValueData: "{code:StartupCommand}"; Tasks: startup; Check: StartupTaskIsExplicit
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"; ValueType: none; ValueName: "{#StartupName}"; Flags: deletevalue noerror; Tasks: startup; Check: StartupTaskIsExplicit
Root: HKCU; Subkey: "{#RunKey}"; ValueType: none; ValueName: "{#StartupName}"; Flags: deletevalue; Tasks: not startup; Check: ShouldRemoveStartup

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchQusasa}"; Flags: nowait postinstall skipifsilent

[Code]
var
  StartupTaskInitialized, InstallingUpdate: Boolean;
  ProductPage: TWizardPage;
  ProductArt: TBitmapImage;
  ProductDescription, StepTrack, ReadyPath, ReadyDesktop, ReadyStartup, InstallLead: TNewStaticText;

function SetupText(Parent: TWinControl; Left, Top, Width, Height, PointSize: Integer; Bold: Boolean): TNewStaticText;
begin
  Result := TNewStaticText.Create(WizardForm);
  Result.Parent := Parent;
  Result.AutoSize := False;
  Result.WordWrap := True;
  Result.SetBounds(Left, Top, Width, Height);
  Result.Font.Size := PointSize;
  if Bold then Result.Font.Style := [fsBold];
end;

function StepCaption(Current: Integer): String;
var
  Labels: TArrayOfString;
  I: Integer;
begin
  Labels := [CustomMessage('StepIntro'), CustomMessage('StepOptions'), CustomMessage('StepReview'),
    CustomMessage('StepInstall'), CustomMessage('StepDone')];
  Result := '';
  for I := 0 to 4 do begin
    if I > 0 then Result := Result + '    ·    ';
    if I = Current then Result := Result + '[' + Labels[I] + ']'
    else Result := Result + Labels[I];
  end;
end;

procedure InitializeWizard;
var
  W: Integer;
begin
  ProductPage := CreateCustomPage(wpWelcome, CustomMessage('IntroTitle'), CustomMessage('IntroSubtitle'));
  ExtractTemporaryFile('SetupFeature.bmp');
  ProductArt := TBitmapImage.Create(WizardForm);
  ProductArt.Parent := ProductPage.Surface;
  ProductArt.SetBounds(0, ScaleY(8), ProductPage.SurfaceWidth, ScaleY(169));
  ProductArt.Stretch := True;
  ProductArt.Bitmap.LoadFromFile(ExpandConstant('{tmp}\SetupFeature.bmp'));
  ProductDescription := SetupText(ProductPage.Surface, 0, ScaleY(190), ProductPage.SurfaceWidth, ScaleY(84), 11, False);
  ProductDescription.Caption := CustomMessage('IntroBody');

  WizardForm.PageNameLabel.Font.Size := 14;
  WizardForm.PageNameLabel.Font.Style := [fsBold];
  WizardForm.PageNameLabel.Height := ScaleY(23);
  WizardForm.PageDescriptionLabel.Top := WizardForm.PageNameLabel.Top + ScaleY(24);
  WizardForm.PageDescriptionLabel.Font.Size := 9;
  StepTrack := WizardForm.PageDescriptionLabel;

  W := WizardForm.InnerNotebook.ClientWidth;
  WizardForm.SelectTasksLabel.Caption := CustomMessage('OptionsLead');
  WizardForm.SelectTasksLabel.Font.Size := 11;
  WizardForm.TasksList.Top := WizardForm.SelectTasksLabel.Top + ScaleY(43);
  WizardForm.TasksList.Height := ScaleY(104);
  WizardForm.TasksList.Font.Size := 11;

  WizardForm.ReadyLabel.Caption := CustomMessage('ReviewLead');
  WizardForm.ReadyLabel.Font.Size := 11;
  WizardForm.ReadyMemo.Visible := False;
  ReadyPath := SetupText(WizardForm.ReadyPage, 0, ScaleY(58), W, ScaleY(57), 11, True);
  ReadyDesktop := SetupText(WizardForm.ReadyPage, 0, ScaleY(131), W, ScaleY(40), 11, False);
  ReadyStartup := SetupText(WizardForm.ReadyPage, 0, ScaleY(180), W, ScaleY(40), 11, False);

  InstallLead := SetupText(WizardForm.InstallingPage, 0, ScaleY(80), W, ScaleY(70), 11, False);
  InstallLead.Caption := CustomMessage('InstallLead');
end;

function TaskState(const Name: String): String;
begin
  if WizardIsTaskSelected(Name) then Result := CustomMessage('Enabled')
  else Result := CustomMessage('Disabled');
end;

function InitializeSetup: Boolean;
begin
  Result := IsDotNetInstalled(net48, 0);
  if not Result then
    SuppressibleMsgBox(CustomMessage('RuntimeMissing'), mbError, MB_OK, IDOK);
end;

function InitializeUninstall: Boolean;
begin
  Result := not CheckForMutexes('{#RunningMutex}');
  if not Result then
    SuppressibleMsgBox(CustomMessage('CloseBeforeUninstall'), mbInformation, MB_OK, IDOK);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Target, Helper, PreviousVersion: String;
  ExitCode: Integer;
begin
  Result := '';
  Target := ExpandConstant('{app}\{#AppExe}');
  if not FileExists(Target) then exit;
  InstallingUpdate := GetVersionNumbersString(Target, PreviousVersion) and
    (CompareText(PreviousVersion, '{#AppVersion}.0') <> 0);
  Helper := ExpandConstant('{tmp}\{#AppExe}');
  if not FileExists(Helper) then ExtractTemporaryFile('{#AppExe}');

  if not Exec(Helper, '--probe-update "' + Target + '"', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then begin
    Result := CustomMessage('ClosureFailed');
    exit;
  end;
  if ExitCode = 0 then exit;
  if ExitCode <> 10 then begin
    Result := CustomMessage('ClosureFailed');
    exit;
  end;
  if WizardSilent then begin
    Log('Qusasa is running. Interactive consent is required before closing it.');
    Result := CustomMessage('Postponed');
    exit;
  end;
  if TaskDialogMsgBox(CustomMessage('ConsentTitle'), CustomMessage('ConsentClose'), mbConfirmation,
    MB_YESNO or MB_DEFBUTTON2, [CustomMessage('ConsentAccept'), CustomMessage('ConsentLater')], 0) <> IDYES then begin
    Result := CustomMessage('Postponed');
    exit;
  end;
  Log('User approved closing the running Qusasa instance for the update.');
  if not Exec(Helper, '--close-for-update "' + Target + '"', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) or
    (ExitCode <> 0) then
    Result := CustomMessage('ClosureFailed');
end;

procedure CurInstallProgressChanged(CurProgress, MaxProgress: Integer);
var
  ProgressFile: String;
begin
  ProgressFile := ExpandConstant('{param:QPROGRESS|}');
  if (ProgressFile <> '') and (MaxProgress > 0) then
    SaveStringToFile(ProgressFile, IntToStr(Round(100.0 * CurProgress / MaxProgress)), False);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and not InstallingUpdate then
    RegWriteStringValue(HKCU, 'Software\Qusasa', 'InstallerLanguage', ActiveLanguage);
  if (CurStep = ssPostInstall) and InstallingUpdate then
    RegWriteStringValue(HKCU, 'Software\Qusasa', 'PendingReleaseNotes', '{#DisplayVersion}');
end;

function StartupCommand(Param: String): String;
begin
  Result := '"' + ExpandConstant('{app}\{#AppExe}') + '" --background';
end;

function OwnsStartupRegistration: Boolean;
var
  CurrentCommand: String;
begin
  Result := RegQueryStringValue(HKCU, '{#RunKey}', '{#StartupName}', CurrentCommand) and
    (CompareText(CurrentCommand, StartupCommand('')) = 0);
end;

function HasTaskArguments: Boolean;
begin
  Result := (ExpandConstant('{param:TASKS|~}') <> '~') or
    (ExpandConstant('{param:MERGETASKS|~}') <> '~');
end;

function StartupTaskIsExplicit: Boolean;
begin
  Result := not WizardSilent or HasTaskArguments;
end;

function ShouldRemoveStartup: Boolean;
begin
  Result := StartupTaskIsExplicit and OwnsStartupRegistration;
end;

function OwnsDesktopShortcut: Boolean;
var
  Shell, Shortcut: Variant;
  ShortcutPath: String;
begin
  Result := False;
  ShortcutPath := ExpandConstant('{autodesktop}\{#AppName}.lnk');
  if not FileExists(ShortcutPath) then
    exit;
  try
    Shell := CreateOleObject('WScript.Shell');
    Shortcut := Shell.CreateShortcut(ShortcutPath);
    Result := CompareText(Shortcut.TargetPath, ExpandConstant('{app}\{#AppExe}')) = 0;
  except
    Log('Could not check desktop shortcut ownership.');
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
var
  Tasks: String;
begin
  if CurPageID = ProductPage.ID then StepTrack.Caption := StepCaption(0);
  case CurPageID of
    wpSelectTasks: begin
      WizardForm.PageNameLabel.Caption := CustomMessage('StageOptions');
      StepTrack.Caption := StepCaption(1);
    end;
    wpReady: begin
      WizardForm.PageNameLabel.Caption := CustomMessage('StageReady');
      StepTrack.Caption := StepCaption(2);
      WizardForm.ReadyMemo.Visible := False;
      ReadyPath.Caption := ExpandConstant('{app}');
      ReadyDesktop.Caption := TaskState('desktopicon') + '  ·  ' + CustomMessage('DesktopShortcut');
      ReadyStartup.Caption := TaskState('startup') + '  ·  ' + CustomMessage('StartWithWindows');
    end;
    wpPreparing, wpInstalling: begin
      WizardForm.PageNameLabel.Caption := CustomMessage('StageInstall');
      StepTrack.Caption := StepCaption(3);
    end;
    wpFinished: begin
      WizardForm.FinishedHeadingLabel.Caption := CustomMessage('StageDone');
      WizardForm.FinishedLabel.Caption := CustomMessage('DoneLead');
    end;
  end;
  if (CurPageID = wpSelectTasks) and not StartupTaskInitialized then begin
    StartupTaskInitialized := True;
    if HasTaskArguments then
      exit;
    Tasks := '';
    if WizardIsTaskSelected('desktopicon') then
      Tasks := 'desktopicon';
    if OwnsStartupRegistration then begin
      if Tasks <> '' then
        Tasks := Tasks + ',';
      Tasks := Tasks + 'startup';
    end;
    WizardSelectTasks(Tasks);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if (CurUninstallStep = usPostUninstall) and OwnsStartupRegistration then
    RegDeleteValue(HKCU, '{#RunKey}', '{#StartupName}');
end;
