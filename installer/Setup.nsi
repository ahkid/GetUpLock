# -*- coding: utf-8 -*-
Unicode true
!include "MUI2.nsh"
!include "FileFunc.nsh"
!include "LogicLib.nsh"
!include "x64.nsh"
!include "WinVer.nsh"

!include "${WORKSPACE}\locale\${EDITION}\installer.nsh"
!define REGKEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_ID}"
Name "${PRODUCT}"
OutFile "${RELEASE_DIR}\${APP_ID}-Setup-${APP_VERSION}-${APP_BUILD}.exe"
InstallDir "$LOCALAPPDATA\Programs\${APP_ID}"
RequestExecutionLevel user
SetCompressor /SOLID lzma
SetCompressorDictSize 32
SetDatablockOptimize on
BrandingText "${PRODUCT}"
Icon "${WORKSPACE}\assets\app.ico"
UninstallIcon "${WORKSPACE}\assets\app.ico"
VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey /LANG=${LANG_ID} "ProductName" "${PRODUCT}"
VIAddVersionKey /LANG=${LANG_ID} "FileDescription" "${INSTALLER_TITLE}"
VIAddVersionKey /LANG=${LANG_ID} "FileVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=${LANG_ID} "LegalCopyright" "新疆慧软科技有限公司"
ShowInstDetails show
ShowUninstDetails show
!define MUI_ABORTWARNING
!define MUI_ICON "${WORKSPACE}\assets\app.ico"
!define MUI_UNICON "${WORKSPACE}\assets\app.ico"
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\GetUpLock.exe"
!define MUI_FINISHPAGE_RUN_PARAMETERS "--after-install"
!define MUI_FINISHPAGE_RUN_TEXT "${RUN_TEXT}"
!define MUI_FINISHPAGE_TEXT "${FINISHED}"
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH
!insertmacro MUI_LANGUAGE "${WIZARD_LANG}"

Var Portable
Var Parameters
!include "${WORKSPACE}\installer\InAppUpdate.nsh"

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_OK|MB_ICONSTOP "${REQUIRE_64}" /SD IDOK
    SetErrorLevel 2
    Abort
  ${EndIf}
  ${IfNot} ${AtLeastWin10}
    MessageBox MB_OK|MB_ICONSTOP "${REQUIRE_WIN}" /SD IDOK
    SetErrorLevel 2
    Abort
  ${EndIf}
  SetShellVarContext current
  SetRegView 64
  Call InitInAppUpdate
  ; $CMDLINE omits NSIS /D=; inspect the original command line to preserve an explicit destination.
  System::Call 'kernel32::GetCommandLineW() w.r1'
  ClearErrors
  ${GetOptions} $1 "/D=" $2
  ${If} ${Errors}
    ReadRegStr $0 HKCU "${REGKEY}" "InstallLocation"
    ${If} $0 != ""
      StrCpy $INSTDIR $0
    ${EndIf}
  ${EndIf}
  StrCpy $Portable "0"
  ${GetParameters} $Parameters
  ClearErrors
  ${GetOptions} $Parameters "/PORTABLE" $0
  ${IfNot} ${Errors}
    StrCpy $Portable "1"
  ${EndIf}
  ${If} $Portable == "0"
    System::Call 'kernel32::OpenMutexW(i 0x00100000, i 0, w "Global\${INSTANCE_ID}") p.r0'
    ${If} $0 P<> 0
      System::Call 'kernel32::CloseHandle(p r0)'
      MessageBox MB_OK|MB_ICONEXCLAMATION "${CLOSE_INSTALL}" /SD IDOK
      SetErrorLevel 3
      Abort
    ${EndIf}
  ${EndIf}
FunctionEnd

Function .onInstSuccess
  Call RestartAfterUpdate
FunctionEnd

Function .onGUIEnd
  Call ReleaseUpdateMutex
FunctionEnd

Section "${PRODUCT}" SEC_MAIN
  SetOverwrite on

  SetOutPath "$INSTDIR"
  File "${PAYLOAD}\GetUpLock.exe"
  File "${PAYLOAD}\GetUpLock.exe.config"
  File "${PAYLOAD}\libmediapipe.dll"
  SetOverwrite off
  File "${PAYLOAD}\config.json"
  SetOverwrite on
  SetOutPath "$INSTDIR\assets"
  File "${PAYLOAD}\assets\alert.wav"
  File "${PAYLOAD}\assets\pose_landmarker_lite.task"
  SetOutPath "$INSTDIR\licenses"
  File "${PAYLOAD}\licenses\MediaPipe-LICENSE.txt"
  File "${PAYLOAD}\licenses\MediaPipe-NOTICE.txt"
  SetOutPath "$INSTDIR"
  WriteINIStr "$INSTDIR\install-mode.ini" "Install" "Portable" "$Portable"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  Delete "$INSTDIR\SquatLock.exe"
  Delete "$INSTDIR\SquatLock.exe.config"
  ${If} $Portable == "0"
    ClearErrors
    ExecWait '"$INSTDIR\GetUpLock.exe" --configure-installed-startup' $0
    ${If} ${Errors}
    ${OrIf} $0 != 0
      DetailPrint "${STARTUP_FAILED}"
      MessageBox MB_OK|MB_ICONEXCLAMATION "${STARTUP_FAILED}" /SD IDOK
    ${EndIf}
    CreateDirectory "$SMPROGRAMS\${PRODUCT}"
    CreateShortcut "$SMPROGRAMS\${PRODUCT}\${PRODUCT}.lnk" "$INSTDIR\GetUpLock.exe"
    CreateShortcut "$SMPROGRAMS\${PRODUCT}\${UNINSTALL_TITLE}.lnk" "$INSTDIR\Uninstall.exe"
    CreateShortcut "$DESKTOP\${PRODUCT}.lnk" "$INSTDIR\GetUpLock.exe"
    WriteRegStr HKCU "${REGKEY}" "DisplayName" "${PRODUCT}"
    WriteRegStr HKCU "${REGKEY}" "DisplayVersion" "${APP_VERSION}"
    WriteRegStr HKCU "${REGKEY}" "InstallLocation" "$INSTDIR"
    WriteRegStr HKCU "${REGKEY}" "DisplayIcon" "$INSTDIR\GetUpLock.exe"
    WriteRegStr HKCU "${REGKEY}" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
    WriteRegStr HKCU "${REGKEY}" "QuietUninstallString" '$\"$INSTDIR\Uninstall.exe$\" /S'
    WriteRegDWORD HKCU "${REGKEY}" "NoModify" 1
    WriteRegDWORD HKCU "${REGKEY}" "NoRepair" 1
    ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
    WriteRegDWORD HKCU "${REGKEY}" "EstimatedSize" $0
  ${EndIf}
SectionEnd

Function un.onInit
  SetShellVarContext current
  SetRegView 64
  ReadINIStr $Portable "$INSTDIR\install-mode.ini" "Install" "Portable"
  ${If} $Portable != "1"
    System::Call 'kernel32::OpenMutexW(i 0x00100000, i 0, w "Global\${INSTANCE_ID}") p.r0'
    ${If} $0 P<> 0
      System::Call 'kernel32::CloseHandle(p r0)'
      MessageBox MB_OK|MB_ICONEXCLAMATION "${CLOSE_UNINSTALL}" /SD IDOK
      SetErrorLevel 3
      Abort
    ${EndIf}
  ${EndIf}
FunctionEnd

Section "Uninstall"
  Delete "$INSTDIR\GetUpLock.exe"
  Delete "$INSTDIR\GetUpLock.exe.config"
  Delete "$INSTDIR\libmediapipe.dll"
  Delete "$INSTDIR\assets\alert.wav"
  Delete "$INSTDIR\assets\pose_landmarker_lite.task"
  Delete "$INSTDIR\licenses\MediaPipe-LICENSE.txt"
  Delete "$INSTDIR\licenses\MediaPipe-NOTICE.txt"
  RMDir "$INSTDIR\assets"
  RMDir "$INSTDIR\licenses"
  ${If} $Portable != "1"
    ReadRegStr $0 HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "${APP_ID}"
    ${If} $0 == '$\"$INSTDIR\GetUpLock.exe$\"'
      DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "${APP_ID}"
    ${EndIf}
    Delete "$DESKTOP\${PRODUCT}.lnk"
    Delete "$SMPROGRAMS\${PRODUCT}\${PRODUCT}.lnk"
    Delete "$SMPROGRAMS\${PRODUCT}\${UNINSTALL_TITLE}.lnk"
    RMDir "$SMPROGRAMS\${PRODUCT}"
    DeleteRegKey HKCU "${REGKEY}"
  ${EndIf}
  Delete "$INSTDIR\install-mode.ini"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  ; 保留用户设置。
SectionEnd
