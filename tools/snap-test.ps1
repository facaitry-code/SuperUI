param(
    [string]$Exe = "E:\TestProject\SuperUI\samples\SuperUIDemo\bin\Debug\net8.0-windows\SuperUIDemo.exe",
    [string]$AppArgs = "--theme=Light",
    [int]$WaitMs = 2800
)

# 无边框窗口拖动手势验收：用真实鼠标输入驱动演示窗口，逐条打印 PASS / FAIL。
# 覆盖：触顶最大化、最大化后拖下还原、拖到顶部再拖回不误触、左右半屏、窗口按钮、双击标题栏、边缘缩放。
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

Add-Type -Namespace SnapTest -Name Native -MemberDefinition @'
[StructLayout(LayoutKind.Sequential)]
public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
[DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
[DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, uint data, IntPtr extra);
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
[DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr hWnd);
[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
'@

$script:LEFTDOWN = 0x0002
$script:LEFTUP = 0x0004
$script:failures = 0
$script:process = $null

function Get-Rect([IntPtr]$hwnd) {
    $rect = New-Object SnapTest.Native+RECT
    [void][SnapTest.Native]::GetWindowRect($hwnd, [ref]$rect)
    return [pscustomobject]@{
        Left   = [int]$rect.Left
        Top    = [int]$rect.Top
        Right  = [int]$rect.Right
        Bottom = [int]$rect.Bottom
        Width  = [int]($rect.Right - $rect.Left)
        Height = [int]($rect.Bottom - $rect.Top)
    }
}

function Assert([string]$name, [bool]$ok, [string]$detail) {
    if ($script:process.HasExited -or $script:process.MainWindowHandle -eq [IntPtr]::Zero) {
        Write-Output "FAIL  $name  (window gone) $detail"
        $script:failures = $script:failures + 1
        return
    }

    if ($ok) {
        Write-Output "PASS  $name  $detail"
    }
    else {
        Write-Output "FAIL  $name  $detail"
        $script:failures = $script:failures + 1
    }
}

function Move-MouseAlong([int]$fromX, [int]$fromY, [int]$toX, [int]$toY, [int]$steps = 16) {
    for ($i = 1; $i -le $steps; $i++) {
        $x = [int]($fromX + ($toX - $fromX) * $i / $steps)
        $y = [int]($fromY + ($toY - $fromY) * $i / $steps)
        [void][SnapTest.Native]::SetCursorPos($x, $y)
        Start-Sleep -Milliseconds 25
    }
}

function Start-Drag([int]$x, [int]$y) {
    [void][SnapTest.Native]::SetCursorPos($x, $y)
    Start-Sleep -Milliseconds 200
    [SnapTest.Native]::mouse_event($LEFTDOWN, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 260
}

function Complete-Drag {
    Start-Sleep -Milliseconds 320
    [SnapTest.Native]::mouse_event($LEFTUP, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 850
}

function Drag-To([int]$x, [int]$y, [int]$toX, [int]$toY) {
    Start-Drag $x $y
    Move-MouseAlong $x $y $toX $toY
    Complete-Drag
}

function Click-At([int]$x, [int]$y) {
    [void][SnapTest.Native]::SetCursorPos($x, $y)
    Start-Sleep -Milliseconds 180
    [SnapTest.Native]::mouse_event($LEFTDOWN, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 90
    [SnapTest.Native]::mouse_event($LEFTUP, 0, 0, 0, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 700
}

function DoubleClick-At([int]$x, [int]$y) {
    [void][SnapTest.Native]::SetCursorPos($x, $y)
    Start-Sleep -Milliseconds 180
    for ($i = 0; $i -lt 2; $i++) {
        [SnapTest.Native]::mouse_event($LEFTDOWN, 0, 0, 0, [IntPtr]::Zero)
        Start-Sleep -Milliseconds 45
        [SnapTest.Native]::mouse_event($LEFTUP, 0, 0, 0, [IntPtr]::Zero)
        Start-Sleep -Milliseconds 90
    }

    Start-Sleep -Milliseconds 750
}

function Reset-Window([IntPtr]$hwnd, [int]$x, [int]$y, [int]$w, [int]$h) {
    if ([bool][SnapTest.Native]::IsZoomed($hwnd)) {
        [void][SnapTest.Native]::ShowWindow($hwnd, 9)
        Start-Sleep -Milliseconds 450
    }

    [void][SnapTest.Native]::SetWindowPos($hwnd, [IntPtr]::new(-1), $x, $y, $w, $h, 0x0004)
    [void][SnapTest.Native]::SetForegroundWindow($hwnd)
    Start-Sleep -Milliseconds 600
}

$work = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea

$script:process = Start-Process -FilePath $Exe -ArgumentList $AppArgs.Split(" ") -PassThru
Start-Sleep -Milliseconds $WaitMs
$script:process.Refresh()
$hwnd = $script:process.MainWindowHandle
if ($hwnd -eq [IntPtr]::Zero) { $script:process.Kill(); throw "no window handle" }

[void][SnapTest.Native]::ShowWindow($hwnd, 5)
[void][SnapTest.Native]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 400

# 先点一下标题栏把窗口激活，避免第一次点击被「激活窗口」吃掉
Reset-Window $hwnd 260 320 900 620
$warmup = Get-Rect $hwnd
Click-At ($warmup.Left + 300) ($warmup.Top + 20)

# ---------- 1. 触顶最大化 ----------
Reset-Window $hwnd 260 320 900 620
$r0 = Get-Rect $hwnd
Drag-To ($r0.Left + 140) ($r0.Top + 20) ($r0.Left + 140) 0
$r = Get-Rect $hwnd
$zoomed = [bool][SnapTest.Native]::IsZoomed($hwnd)
$ok = $zoomed -and ($r.Top -eq $work.Top) -and ($r.Width -eq $work.Width) -and ($r.Height -eq $work.Height)
Assert "拖到屏幕顶部最大化" $ok "rect=$($r.Left),$($r.Top) $($r.Width)x$($r.Height) zoomed=$zoomed 工作区=$($work.Width)x$($work.Height)"

# 最大化时客户区必须满幅（没有多出来的系统边框条）
$client = New-Object SnapTest.Native+RECT
[void][SnapTest.Native]::GetClientRect($hwnd, [ref]$client)
$ok = ($client.Left -eq 0) -and ($client.Top -eq 0) -and ($client.Right -eq $work.Width) -and ($client.Bottom -eq $work.Height)
Assert "最大化客户区满幅" $ok "client=$($client.Left),$($client.Top)-$($client.Right),$($client.Bottom)"

# ---------- 2. 最大化后从标题栏拖下还原 ----------
$maxed = Get-Rect $hwnd
Drag-To ($maxed.Left + 220) 18 ($maxed.Left + 220) 430
$r = Get-Rect $hwnd
$zoomed = [bool][SnapTest.Native]::IsZoomed($hwnd)
$ok = (-not $zoomed) -and ($r.Height -lt $work.Height) -and ($r.Top -gt 200)
Assert "最大化后拖下还原并跟随" $ok "rect=$($r.Left),$($r.Top) $($r.Width)x$($r.Height) zoomed=$zoomed"

# ---------- 3. 触顶后又拖回来，不该最大化 ----------
Reset-Window $hwnd 320 340 900 620
$r0 = Get-Rect $hwnd
Start-Drag ($r0.Left + 140) ($r0.Top + 20)
Move-MouseAlong ($r0.Left + 140) ($r0.Top + 20) ($r0.Left + 140) 0
Move-MouseAlong ($r0.Left + 140) 0 ($r0.Left + 140) 300
Complete-Drag
$r = Get-Rect $hwnd
$zoomed = [bool][SnapTest.Native]::IsZoomed($hwnd)
Assert "触顶后拖回不误最大化" (-not $zoomed) "rect=$($r.Left),$($r.Top) $($r.Width)x$($r.Height) zoomed=$zoomed"

# ---------- 4. 左边缘半屏 ----------
Reset-Window $hwnd 500 340 900 620
$r0 = Get-Rect $hwnd
Drag-To ($r0.Left + 140) ($r0.Top + 20) 0 ($r0.Top + 20)
$r = Get-Rect $hwnd
$ok = ($r.Left -eq $work.Left) -and ([Math]::Abs($r.Width - $work.Width / 2) -le 2) -and ($r.Height -eq $work.Height)
Assert "拖到左边缘半屏" $ok "rect=$($r.Left),$($r.Top) $($r.Width)x$($r.Height)"

# ---------- 5. 右边缘半屏 ----------
Reset-Window $hwnd 300 340 900 620
$r0 = Get-Rect $hwnd
Drag-To ($r0.Left + 140) ($r0.Top + 20) ($work.Right - 1) ($r0.Top + 20)
$r = Get-Rect $hwnd
$ok = ([Math]::Abs($r.Width - $work.Width / 2) -le 2) -and ($r.Right -eq $work.Right) -and ($r.Height -eq $work.Height)
Assert "拖到右边缘半屏" $ok "rect=$($r.Left),$($r.Top) $($r.Width)x$($r.Height)"

# ---------- 6. 标题栏最大化 / 还原按钮 ----------
Reset-Window $hwnd 300 320 900 620
$r0 = Get-Rect $hwnd
Click-At ($r0.Right - 46 - 23) ($r0.Top + 20)
$zoomed = [bool][SnapTest.Native]::IsZoomed($hwnd)
Assert "标题栏最大化按钮" $zoomed "zoomed=$zoomed"

$maxed = Get-Rect $hwnd
Click-At ($maxed.Right - 46 - 23) ($maxed.Top + 20)
$r = Get-Rect $hwnd
$zoomed = [bool][SnapTest.Native]::IsZoomed($hwnd)
$ok = (-not $zoomed) -and ($r.Width -eq 900)
Assert "标题栏还原按钮" $ok "rect=$($r.Left),$($r.Top) $($r.Width)x$($r.Height) zoomed=$zoomed"

# ---------- 7. 最大化正好贴工作区 ----------
Reset-Window $hwnd 300 320 900 620
$r0 = Get-Rect $hwnd
Drag-To ($r0.Left + 140) ($r0.Top + 20) ($r0.Left + 140) 0
$r = Get-Rect $hwnd
$ok = ($r.Left -eq $work.Left) -and ($r.Top -eq $work.Top) -and ($r.Right -eq $work.Right) -and ($r.Bottom -eq $work.Bottom)
Assert "最大化贴合工作区（不盖任务栏）" $ok "窗口=$($r.Left),$($r.Top)-$($r.Right),$($r.Bottom) 工作区=$($work.Left),$($work.Top)-$($work.Right),$($work.Bottom)"

# ---------- 8. 右边缘拖拽缩放仍然可用 ----------
Reset-Window $hwnd 300 320 900 620
$r0 = Get-Rect $hwnd
$edgeY = $r0.Top + [int]($r0.Height / 2)
Drag-To ($r0.Right - 2) $edgeY ($r0.Right + 118) $edgeY
$r = Get-Rect $hwnd
$ok = ($r.Width -ge 990) -and ($r.Left -eq $r0.Left)
Assert "右边缘可缩放" $ok "宽 $($r0.Width) -> $($r.Width)"

# ---------- 9. 双击标题栏最大化 ----------
Reset-Window $hwnd 300 320 900 620
$r0 = Get-Rect $hwnd
DoubleClick-At ($r0.Left + 300) ($r0.Top + 20)
$zoomed = [bool][SnapTest.Native]::IsZoomed($hwnd)
Assert "双击标题栏最大化" $zoomed "zoomed=$zoomed"

# ---------- 10. 左上角贴角 ----------
# 演示窗口最小高度 760 > 四分之一屏高度 520，系统会把高度夹到最小尺寸，所以只校验左上角与半宽。
Reset-Window $hwnd 500 360 900 620
$r0 = Get-Rect $hwnd
Drag-To ($r0.Left + 140) ($r0.Top + 20) 0 0
$r = Get-Rect $hwnd
$ok = ($r.Left -eq $work.Left) -and ($r.Top -eq $work.Top) -and ([Math]::Abs($r.Width - $work.Width / 2) -le 2)
Assert "拖到左上角贴角" $ok "rect=$($r.Left),$($r.Top) $($r.Width)x$($r.Height)"

Start-Sleep -Milliseconds 200
$script:process.CloseMainWindow() | Out-Null
Start-Sleep -Milliseconds 900
if (-not $script:process.HasExited) { $script:process.Kill() }

Write-Output ""
if ($script:failures -eq 0) { Write-Output "ALL PASS" } else { Write-Output "$($script:failures) FAILED"; exit 1 }
