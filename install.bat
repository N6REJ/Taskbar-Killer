@echo off
echo Installing Taskbar Killer...

REM Create installation directory
if not exist "C:\Program Files\N6REJ\Taskbar Killer" mkdir "C:\Program Files\N6REJ\Taskbar Killer"

REM Copy the working executable and dependencies
copy "bin\Release\net6.0-windows\win-x64\Taskbar Killer.exe" "C:\Program Files\N6REJ\Taskbar Killer\"
copy "bin\Release\net6.0-windows\win-x64\taskbar.ico" "C:\Program Files\N6REJ\Taskbar Killer\"
xcopy "bin\Release\net6.0-windows\win-x64\icons" "C:\Program Files\N6REJ\Taskbar Killer\icons\" /E /I /Y

REM Copy all runtime dependencies
xcopy "bin\Release\net6.0-windows\win-x64\*.dll" "C:\Program Files\N6REJ\Taskbar Killer\" /Y
copy "bin\Release\net6.0-windows\win-x64\*.json" "C:\Program Files\N6REJ\Taskbar Killer\"

REM Create desktop shortcut
echo Creating desktop shortcut...
powershell -Command "$WshShell = New-Object -comObject WScript.Shell; $Shortcut = $WshShell.CreateShortcut('C:\Users\Public\Desktop\Taskbar Killer.lnk'); $Shortcut.TargetPath = 'C:\Program Files\N6REJ\Taskbar Killer\Taskbar Killer.exe'; $Shortcut.WorkingDirectory = 'C:\Program Files\N6REJ\Taskbar Killer'; $Shortcut.Save()"

REM Create start menu shortcut
if not exist "C:\ProgramData\Microsoft\Windows\Start Menu\Programs\N6REJ" mkdir "C:\ProgramData\Microsoft\Windows\Start Menu\Programs\N6REJ"
powershell -Command "$WshShell = New-Object -comObject WScript.Shell; $Shortcut = $WshShell.CreateShortcut('C:\ProgramData\Microsoft\Windows\Start Menu\Programs\N6REJ\Taskbar Killer.lnk'); $Shortcut.TargetPath = 'C:\Program Files\N6REJ\Taskbar Killer\Taskbar Killer.exe'; $Shortcut.WorkingDirectory = 'C:\Program Files\N6REJ\Taskbar Killer'; $Shortcut.Save()"

echo Installation complete!
echo You can now run Taskbar Killer from the Start Menu or Desktop shortcut.
pause
