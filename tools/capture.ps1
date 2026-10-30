param(
    [string]$Exe = "E:\TestProject\SuperUI\samples\SuperUIDemo\bin\Debug\net8.0-windows\SuperUIDemo.exe",
    [string]$Out = "E:\TestProject\SuperUI\artifacts\shot.png",
    [string]$AppArgs = "",
    [int]$WaitMs = 2500
)

Add-Type -AssemblyName System.Drawing

Add-Type -Namespace Win32Shot -Name Native -MemberDefinition @'
[StructLayout(LayoutKind.Sequential)]
public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

[DllImport("user32.dll")]
public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

[DllImport("user32.dll")]
public static extern bool SetForegroundWindow(IntPtr hWnd);

[DllImport("user32.dll")]
public static extern bool SetProcessDPIAware();

[DllImport("user32.dll")]
public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
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

if ($handle -eq [IntPtr]::Zero) {
    Write-Output "no main window handle"
    $process.Kill()
    exit 1
}

[Win32Shot.Native]::SetWindowPos($handle, [IntPtr](-1), 0, 0, 0, 0, 0x0001 -bor 0x0002) | Out-Null
Start-Sleep -Milliseconds 250
[Win32Shot.Native]::SetWindowPos($handle, [IntPtr](-2), 0, 0, 0, 0, 0x0001 -bor 0x0002) | Out-Null
[Win32Shot.Native]::SetWindowPos($handle, [IntPtr]::Zero, 80, 70, 0, 0, 0x0001 -bor 0x0004) | Out-Null
[Win32Shot.Native]::SetForegroundWindow($handle) | Out-Null
Start-Sleep -Milliseconds 700

$rect = New-Object Win32Shot.Native+RECT
[Win32Shot.Native]::GetWindowRect($handle, [ref]$rect) | Out-Null
$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top
Write-Output "rect=$($rect.Left),$($rect.Top) size=${width}x${height}"

# 四周留白，方便看到圆角与阴影
$pad = 24
$bitmap = New-Object System.Drawing.Bitmap ($width + $pad * 2), ($height + $pad * 2)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($rect.Left - $pad, $rect.Top - $pad, 0, 0, $bitmap.Size)
$graphics.Dispose()
$bitmap.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()

Write-Output "saved $Out"

Start-Sleep -Milliseconds 200
$process.CloseMainWindow() | Out-Null
Start-Sleep -Milliseconds 800
if (-not $process.HasExited) { $process.Kill() }
