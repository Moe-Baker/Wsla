@echo off

:: BatchGotAdmin
:-------------------------------------
REM  --> Check for permissions
    IF "%PROCESSOR_ARCHITECTURE%" EQU "amd64" (
>nul 2>&1 "%SYSTEMROOT%\SysWOW64\cacls.exe" "%SYSTEMROOT%\SysWOW64\config\system"
) ELSE (
>nul 2>&1 "%SYSTEMROOT%\system32\cacls.exe" "%SYSTEMROOT%\system32\config\system"
)

REM --> If error flag set, we do not have admin.
if '%errorlevel%' NEQ '0' (
    echo Requesting administrative privileges...
    goto UACPrompt
) else ( goto gotAdmin )

:UACPrompt
    echo Set UAC = CreateObject^("Shell.Application"^) > "%temp%\getadmin.vbs"
    set params= %*
    echo UAC.ShellExecute "cmd.exe", "/c ""%~s0"" %params:"=""%", "", "runas", 1 >> "%temp%\getadmin.vbs"

    "%temp%\getadmin.vbs"
    del "%temp%\getadmin.vbs"
    exit /B

:gotAdmin
    pushd "%CD%"
    CD /D "%~dp0"
	
Rem -----------------------------------------------------------------------------------

cd "%~dp0"
cd ..

rmdir "%cd%\Wsla-Core\Shared-Global\Source"
mklink /D "%cd%\Wsla-Core\Shared-Global\Source" "%cd%\Wsla-Shared"
echo:

rmdir "%cd%\Wsla-Unity\Assets\Systems\Wsla\Shared"
mklink /D "%cd%\Wsla-Unity\Assets\Systems\Wsla\Shared" "%cd%\Wsla-Shared"
echo:

rmdir "%cd%\Wsla-Core\Unity-Client\Source\Wsla-Runtime"
mklink /D "%cd%\Wsla-Core\Unity-Client\Source\Wsla-Runtime" "%cd%\Wsla-Unity\Assets\Systems\Wsla\Runtime"
echo:

rmdir "%cd%\Wsla-Core\Unity-Client\Source\Wsla-Sample"
mklink /D "%cd%\Wsla-Core\Unity-Client\Source\Wsla-Sample" "%cd%\Wsla-Unity\Assets\Systems\Wsla-Sample"
echo:

Pause