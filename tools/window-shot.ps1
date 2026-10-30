param(
    [string]$Exe = "E:\TestProject\SuperUI\samples\SuperUIDemo\bin\Debug\net8.0-windows\SuperUIDemo.exe",
    [string]$Out = "E:\TestProject\SuperUI\docs\shot.png",
    [string]$AppArgs = "",
    [int]$WaitMs = 3500
)

Add-Type -AssemblyName System.Drawing

Add-Type -Namespace Win32Shot -Name Native -MemberDefinition @'
[StructLayout(LayoutKind.Sequential)]
public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

[DllImport("user32.dll")]
public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

[DllImport("user32.dll")]
public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);

[DllImport("user32.dll")]
public static extern bool SetProcessDPIAware();
'@

[Win32Shot.Native]::SetProcessDPIAware() | Out-Null

$dir = Split-Path -Parent $Out
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }

$arguments = @()
if ($AppArgs -ne "") { $arguments = $AppArgs.Split(" ") }

$process = Start-Process -FilePath $Exe -ArgumentList $arguments -PassThru
Start-Sleep -Milliseconds $WaitMs
$process.Refresh()

$handle = $process.MainWindowHandle
Write-Output "pid=$($process.Id) hwnd=$handle"
if ($handle -eq [IntPtr]::Zero) { $process.Kill(); exit 1 }

$rect = New-Object Win32Shot.Native+RECT
[Win32Shot.Native]::GetWindowRect($handle, [ref]$rect) | Out-Null
$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top
Write-Output "size=${width}x${height}"

$bitmap = New-Object System.Drawing.Bitmap $width, $height
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$hdc = $graphics.GetHdc()
# PW_RENDERFULLCONTENT = 0x00000002，能把 DWM 合成内容一起抓下来
$ok = [Win32Shot.Native]::PrintWindow($handle, $hdc, 2)
$graphics.ReleaseHdc($hdc)
$graphics.Dispose()
$bitmap.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()
Write-Output "printwindow=$ok saved $Out"

Start-Sleep -Milliseconds 200
$process.CloseMainWindow() | Out-Null
Start-Sleep -Milliseconds 700
if (-not $process.HasExited) { $process.Kill() }
