; Shared by full and incremental installers. No process is forcibly terminated.
Var UpdatePid
Var UpdateMutex
Var NoRestart

Function InitInAppUpdate
  StrCpy $UpdatePid ""
  StrCpy $UpdateMutex "0"
  StrCpy $NoRestart "0"
  ${GetParameters} $R0
  ClearErrors
  ${GetOptions} $R0 "/NORESTART" $R1
  ${IfNot} ${Errors}
    StrCpy $NoRestart "1"
  ${EndIf}
  ClearErrors
  ${GetOptions} $R0 "/INAPPUPDATE=" $UpdatePid
  ${If} ${Errors}
    StrCpy $UpdatePid ""
    Return
  ${EndIf}
  ${If} $UpdatePid <= 0
    SetErrorLevel 10
    Abort "${UPDATE_PID}"
  ${EndIf}
  System::Call 'kernel32::CreateMutexW(p 0, i 0, w "Global\${APP_ID}UpdateInProgress") p.r0 ?e'
  Pop $1
  StrCpy $UpdateMutex $0
  ${If} $0 P= 0
  ${OrIf} $1 = 183
    SetErrorLevel 11
    Abort "${UPDATE_BUSY}"
  ${EndIf}
  System::Call 'kernel32::OpenProcess(i 0x00100000, i 0, i $UpdatePid) p.r0'
  ${If} $0 P<> 0
    System::Call 'kernel32::WaitForSingleObject(p r0, i 30000) i.r1'
    System::Call 'kernel32::CloseHandle(p r0)'
    ${If} $1 != 0
      MessageBox MB_OK|MB_ICONEXCLAMATION "${UPDATE_TIMEOUT}" /SD IDOK
      SetErrorLevel 12
      Abort
    ${EndIf}
  ${EndIf}
FunctionEnd

Function ReleaseUpdateMutex
  ${If} $UpdateMutex P<> 0
    System::Call 'kernel32::CloseHandle(p $UpdateMutex)'
    StrCpy $UpdateMutex "0"
  ${EndIf}
FunctionEnd

Function RestartAfterUpdate
  Call ReleaseUpdateMutex
  ${If} $UpdatePid != ""
  ${AndIf} $NoRestart != "1"
    SetOutPath "$INSTDIR"
    ClearErrors
    Exec '"$INSTDIR\GetUpLock.exe" --after-update'
    ${If} ${Errors}
      MessageBox MB_OK|MB_ICONEXCLAMATION "${RESTART_FAILED}" /SD IDOK
    ${EndIf}
  ${EndIf}
FunctionEnd
