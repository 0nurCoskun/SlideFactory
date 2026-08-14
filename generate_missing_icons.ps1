Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = "Continue"
$csvPath = "C:\CSharpProjects\SlideFactory\missing_icons.csv"
$workflowPath = Join-Path $env:USERPROFILE "Downloads\slidefactory.json"
$iconsDir = "C:\CSharpProjects\SlideFactory\Assets\_Project\Textures\CardIcons"
$logPath = "C:\CSharpProjects\SlideFactory\generate_missing_icons.log"
$serverUrl = "http://127.0.0.1:8188"

function Log($msg) {
    $line = "$(Get-Date -Format 'HH:mm:ss')  $msg"
    Write-Output $line
    Add-Content -Path $logPath -Value $line
}

function Remove-WhiteBackground {
    param([string]$path)
    $bytes = [System.IO.File]::ReadAllBytes($path)
    $ms = New-Object System.IO.MemoryStream(,$bytes)
    $bmp = New-Object System.Drawing.Bitmap($ms)
    $out = New-Object System.Drawing.Bitmap($bmp.Width, $bmp.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for ($y = 0; $y -lt $bmp.Height; $y++) {
        for ($x = 0; $x -lt $bmp.Width; $x++) {
            $p = $bmp.GetPixel($x, $y)
            $minC = [Math]::Min([Math]::Min($p.R, $p.G), $p.B)
            if ($minC -ge 248) { $a = 0 }
            elseif ($minC -le 210) { $a = 255 }
            else { $a = [int](255 * (248 - $minC) / (248 - 210)) }
            $out.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($a, $p.R, $p.G, $p.B))
        }
    }
    $bmp.Dispose(); $ms.Dispose()
    $out.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $out.Dispose()
}

function Generate-Icon {
    param([string]$cardId, [string]$displayName)

    $outPath = Join-Path $iconsDir "$cardId.png"
    if (Test-Path $outPath) { Log "SKIP $cardId (already exists)"; return }

    $lower = $displayName.ToLower()
    $positive = "pixel art game icon, single isolated $lower, one object floating on plain flat background, item icon composition, centered, clean outline, limited palette"
    $negative = "blurry, anti-aliasing, smooth gradient, photorealistic, 3d render, text, watermark, multiple objects, cropped, tileable, seamless pattern, texture, wallpaper, frame, border, book, window"

    if (-not (Test-Path $workflowPath)) {
        Log "FATAL: workflow JSON not found at $workflowPath"
        throw "workflow JSON not found"
    }
    $workflow = Get-Content -Raw $workflowPath | ConvertFrom-Json
    $workflow.'4'.inputs.text_g = $negative
    $workflow.'4'.inputs.text_l = $negative
    $workflow.'5'.inputs.text_g = $positive
    $workflow.'5'.inputs.text_l = $positive
    $workflow.'7'.inputs.seed = Get-Random -Minimum 0 -Maximum 2147483647

    $body = @{ prompt = $workflow; client_id = [guid]::NewGuid().ToString() } | ConvertTo-Json -Depth 20

    try {
        $resp = Invoke-RestMethod -Uri "$serverUrl/prompt" -Method Post -Body $body -ContentType "application/json"
    } catch {
        Log "FAIL $cardId (queue error: $($_.Exception.Message))"
        return
    }
    $promptId = $resp.prompt_id
    if (-not $promptId) { Log "FAIL $cardId (no prompt_id)"; return }

    $history = $null
    for ($i = 0; $i -lt 90; $i++) {
        Start-Sleep -Seconds 2
        try { $h = Invoke-RestMethod -Uri "$serverUrl/history/$promptId" -Method Get } catch { continue }
        if ($h.$promptId) { $history = $h.$promptId; break }
    }
    if ($null -eq $history) { Log "FAIL $cardId (timeout waiting for generation)"; return }

    $imgInfo = $history.outputs.'9'.images[0]
    if ($null -eq $imgInfo) { Log "FAIL $cardId (no image in output)"; return }

    $viewUrl = "$serverUrl/view?filename=$($imgInfo.filename)&subfolder=$($imgInfo.subfolder)&type=$($imgInfo.type)"
    $rawPath = Join-Path $iconsDir "_raw_$cardId.png"
    try {
        Invoke-WebRequest -Uri $viewUrl -OutFile $rawPath
    } catch {
        Log "FAIL $cardId (download error: $($_.Exception.Message))"
        return
    }

    $src = [System.Drawing.Image]::FromFile($rawPath)
    $dst = New-Object System.Drawing.Bitmap 256, 256
    $g = [System.Drawing.Graphics]::FromImage($dst)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.DrawImage($src, 0, 0, 256, 256)
    $g.Dispose(); $src.Dispose()
    $dst.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $dst.Dispose()
    Remove-Item $rawPath

    Remove-WhiteBackground $outPath
    Log "OK   $cardId"
}

Log "=== Batch start: $((Get-Content $csvPath).Count) cards ==="
Get-Content $csvPath | ForEach-Object {
    $parts = $_ -split '\|', 2
    if ($parts.Count -eq 2) {
        Generate-Icon -cardId $parts[0] -displayName $parts[1]
    }
}
Log "=== Batch complete ==="
