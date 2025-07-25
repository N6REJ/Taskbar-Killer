# Taskbar Killer Installer Script
# Run this script as Administrator

param(
    [switch]$Uninstall
)

$AppName = "Taskbar Killer"
$Publisher = "N6REJ"
$InstallPath = "C:\Program Files\$Publisher\$AppName"
$SourcePath = "$PSScriptRoot\bin\Release\net6.0-windows\win-x64"

function Test-Administrator {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Install-Application {
    Write-Host "Installing $AppName..." -ForegroundColor Green
    
    # Create installation directory
    if (!(Test-Path $InstallPath)) {
        New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
        Write-Host "Created installation directory: $InstallPath"
    }
    
    # Stop any running instances
    Get-Process -Name "Taskbar Killer" -ErrorAction SilentlyContinue | Stop-Process -Force
    Write-Host "Stopped any running instances"
    
    # Copy all files from source directory
    Write-Host "Copying application files..."
    Copy-Item "$SourcePath\*" -Destination $InstallPath -Recurse -Force
    Write-Host "Application files copied successfully"
    
    # Create desktop shortcut
    $DesktopPath = [Environment]::GetFolderPath("CommonDesktopDirectory")
    $ShortcutPath = Join-Path $DesktopPath "$AppName.lnk"
    $WshShell = New-Object -ComObject WScript.Shell
    $Shortcut = $WshShell.CreateShortcut($ShortcutPath)
    $Shortcut.TargetPath = Join-Path $InstallPath "$AppName.exe"
    $Shortcut.WorkingDirectory = $InstallPath
    $Shortcut.Description = "A lightweight Windows utility that automatically manages taskbar auto-hide functionality"
    $Shortcut.Save()
    Write-Host "Desktop shortcut created"
    
    # Create start menu shortcut
    $StartMenuPath = [Environment]::GetFolderPath("CommonPrograms")
    $PublisherFolder = Join-Path $StartMenuPath $Publisher
    if (!(Test-Path $PublisherFolder)) {
        New-Item -ItemType Directory -Path $PublisherFolder -Force | Out-Null
    }
    $StartMenuShortcut = Join-Path $PublisherFolder "$AppName.lnk"
    $Shortcut2 = $WshShell.CreateShortcut($StartMenuShortcut)
    $Shortcut2.TargetPath = Join-Path $InstallPath "$AppName.exe"
    $Shortcut2.WorkingDirectory = $InstallPath
    $Shortcut2.Description = "A lightweight Windows utility that automatically manages taskbar auto-hide functionality"
    $Shortcut2.Save()
    Write-Host "Start menu shortcut created"
    
    # Add to Windows Programs list
    $UninstallKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\$AppName"
    New-Item -Path $UninstallKey -Force | Out-Null
    Set-ItemProperty -Path $UninstallKey -Name "DisplayName" -Value $AppName
    Set-ItemProperty -Path $UninstallKey -Name "Publisher" -Value $Publisher
    Set-ItemProperty -Path $UninstallKey -Name "DisplayVersion" -Value "1.0.8"
    Set-ItemProperty -Path $UninstallKey -Name "InstallLocation" -Value $InstallPath
    Set-ItemProperty -Path $UninstallKey -Name "UninstallString" -Value "powershell.exe -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Uninstall"
    Set-ItemProperty -Path $UninstallKey -Name "NoModify" -Value 1
    Set-ItemProperty -Path $UninstallKey -Name "NoRepair" -Value 1
    Write-Host "Added to Windows Programs list"
    
    Write-Host "`n$AppName has been installed successfully!" -ForegroundColor Green
    Write-Host "You can find it in the Start Menu under $Publisher or use the desktop shortcut."
    Write-Host "`nTo start the application now, run: Start-Process '$InstallPath\$AppName.exe'"
}

function Uninstall-Application {
    Write-Host "Uninstalling $AppName..." -ForegroundColor Yellow
    
    # Stop any running instances
    Get-Process -Name "Taskbar Killer" -ErrorAction SilentlyContinue | Stop-Process -Force
    Write-Host "Stopped any running instances"
    
    # Remove desktop shortcut
    $DesktopPath = [Environment]::GetFolderPath("CommonDesktopDirectory")
    $ShortcutPath = Join-Path $DesktopPath "$AppName.lnk"
    if (Test-Path $ShortcutPath) {
        Remove-Item $ShortcutPath -Force
        Write-Host "Desktop shortcut removed"
    }
    
    # Remove start menu shortcut
    $StartMenuPath = [Environment]::GetFolderPath("CommonPrograms")
    $PublisherFolder = Join-Path $StartMenuPath $Publisher
    $StartMenuShortcut = Join-Path $PublisherFolder "$AppName.lnk"
    if (Test-Path $StartMenuShortcut) {
        Remove-Item $StartMenuShortcut -Force
        Write-Host "Start menu shortcut removed"
    }
    
    # Remove publisher folder if empty
    if ((Test-Path $PublisherFolder) -and ((Get-ChildItem $PublisherFolder).Count -eq 0)) {
        Remove-Item $PublisherFolder -Force
        Write-Host "Publisher folder removed"
    }
    
    # Remove from Windows Programs list
    $UninstallKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\$AppName"
    if (Test-Path $UninstallKey) {
        Remove-Item $UninstallKey -Force
        Write-Host "Removed from Windows Programs list"
    }
    
    # Remove installation directory
    if (Test-Path $InstallPath) {
        Remove-Item $InstallPath -Recurse -Force
        Write-Host "Installation directory removed"
    }
    
    Write-Host "`n$AppName has been uninstalled successfully!" -ForegroundColor Green
}

# Main execution
if (!(Test-Administrator)) {
    Write-Error "This script must be run as Administrator. Please run PowerShell as Administrator and try again."
    exit 1
}

if ($Uninstall) {
    Uninstall-Application
} else {
    # Check if source files exist
    if (!(Test-Path "$SourcePath\Taskbar Killer.exe")) {
        Write-Error "Source files not found. Please ensure the application has been built first."
        Write-Host "Run: dotnet build 'Taskbar Killer.csproj' -c Release"
        exit 1
    }
    
    Install-Application
}

Write-Host "`nPress any key to continue..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
