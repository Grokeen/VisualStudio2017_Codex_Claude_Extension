#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : New-GuideMedia
desc         : 실제 WPF 안내 PNG 프레임을 GIF와 동일한 첫 장 PNG로 변환하고 프레임·해시를 검증한다.
author       : CodexCode
create date  : 2026-10-19
update date  : 2026-10-19 CodexCode : TASK024 합성 제품 예제만 사용, AI 이미지 생성·개인 화면 캡처 없음.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$FrameDirectory,[Parameter(Mandatory=$true)][string]$OutputDirectory,[Parameter(Mandatory=$true)][string]$ReportPath)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
foreach($taskFramework in @('WindowsBase','PresentationCore','PresentationFramework')){Add-Type -AssemblyName $taskFramework}
$FrameDirectory=(Resolve-Path -LiteralPath $FrameDirectory).Path
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
$null=New-Item -ItemType Directory -Path $OutputDirectory -Force
$taskManifest=Get-Content -LiteralPath (Join-Path $FrameDirectory 'capture-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$taskResults=@()
foreach($taskScene in @('01-connect','02-request','03-review','04-guide','05-install')){
 $taskPngs=@(Get-ChildItem -LiteralPath (Join-Path $FrameDirectory $taskScene) -File -Filter 'frame-*.png' | Sort-Object Name)
 if($taskPngs.Count -ne 4){throw ('Expected exactly four WPF frames: '+$taskScene)}
 $taskEncoder=New-Object Windows.Media.Imaging.GifBitmapEncoder
 foreach($taskPng in $taskPngs){
  $taskBitmap=New-Object Windows.Media.Imaging.BitmapImage
  $taskBitmap.BeginInit();$taskBitmap.CacheOption=[Windows.Media.Imaging.BitmapCacheOption]::OnLoad;$taskBitmap.UriSource=New-Object Uri($taskPng.FullName);$taskBitmap.EndInit();$taskBitmap.Freeze()
  if($taskBitmap.PixelWidth -ne 600 -or $taskBitmap.PixelHeight -ne 420){throw 'Unexpected WPF frame dimensions.'}
  $taskPalette=New-Object Windows.Media.Imaging.BitmapPalette($taskBitmap,256)
  $taskIndexed=New-Object Windows.Media.Imaging.FormatConvertedBitmap($taskBitmap,[Windows.Media.PixelFormats]::Indexed8,$taskPalette,0)
  $taskMetadata=New-Object Windows.Media.Imaging.BitmapMetadata('gif')
  $taskMetadata.SetQuery('/grctlext/Delay',[UInt16]110)
  $taskMetadata.SetQuery('/grctlext/Disposal',[byte]2)
  $taskFrame=[Windows.Media.Imaging.BitmapFrame]::Create($taskIndexed,$null,$taskMetadata,$null)
  $taskEncoder.Frames.Add($taskFrame)
 }
 $taskMemory=New-Object IO.MemoryStream
 try{$taskEncoder.Save($taskMemory);$taskBytes=$taskMemory.ToArray()}finally{$taskMemory.Dispose()}
 if($taskBytes.Length -lt 13 -or [Text.Encoding]::ASCII.GetString($taskBytes,0,3) -ne 'GIF' -or $taskBytes[$taskBytes.Length-1] -ne 0x3B){throw 'Invalid encoded GIF envelope.'}
 # Netscape repeat extension after the logical screen/global palette; pixel data remains the WPF encoder output.
 $taskOffset=13
 if($taskBytes[10] -band 0x80){$taskOffset+=3*[Math]::Pow(2,($taskBytes[10] -band 7)+1)}
 $taskLoop=[byte[]](0x21,0xFF,0x0B,0x4E,0x45,0x54,0x53,0x43,0x41,0x50,0x45,0x32,0x2E,0x30,0x03,0x01,0x00,0x00,0x00)
 $taskGif=New-Object IO.MemoryStream
 try{
  $taskGif.Write($taskBytes,0,$taskOffset);$taskGif.Write($taskLoop,0,$taskLoop.Length)
  $taskPosition=[int]$taskOffset;$taskImageCount=0
  # WIC의 GIF encoder는 입력 frame GCE metadata를 생략하므로 무손실 pixel block 앞에 명시한다.
  while($taskPosition -lt $taskBytes.Length){
   $taskStart=$taskPosition;$taskMarker=$taskBytes[$taskPosition]
   if($taskMarker -eq 0x3B){$taskGif.WriteByte(0x3B);$taskPosition++;break}
   $taskSkip=$false
   if($taskMarker -eq 0x21){
    if($taskPosition+2 -ge $taskBytes.Length){throw 'Truncated GIF extension.'}
    $taskSkip=$taskBytes[$taskPosition+1] -eq 0xF9;$taskPosition+=2
   }elseif($taskMarker -eq 0x2C){
    if($taskPosition+10 -ge $taskBytes.Length){throw 'Truncated GIF image descriptor.'}
    $taskGce=[byte[]](0x21,0xF9,0x04,0x08,0x6E,0x00,0x00,0x00);$taskGif.Write($taskGce,0,$taskGce.Length)
    $taskPacked=$taskBytes[$taskPosition+9];$taskPosition+=10
    if($taskPacked -band 0x80){$taskPosition+=[int](3*[Math]::Pow(2,($taskPacked -band 7)+1))}
    $taskPosition++;$taskImageCount++
   }else{throw 'Unexpected GIF block marker.'}
   do{
    if($taskPosition -ge $taskBytes.Length){throw 'Truncated GIF sub-block.'}
    $taskBlockLength=[int]$taskBytes[$taskPosition];$taskPosition+=1+$taskBlockLength
    if($taskPosition -gt $taskBytes.Length){throw 'GIF sub-block exceeds input.'}
   }while($taskBlockLength -ne 0)
   if(!$taskSkip){$taskGif.Write($taskBytes,$taskStart,$taskPosition-$taskStart)}
  }
  if($taskImageCount -ne 4 -or $taskPosition -ne $taskBytes.Length){throw 'GIF image block verification failed.'}
  $taskEncoded=$taskGif.ToArray()
 }finally{$taskGif.Dispose()}
 if($taskEncoded.Length -gt 2MB){throw 'Guide GIF exceeds the product media byte limit.'}
 $taskGifPath=Join-Path $OutputDirectory ($taskScene+'.gif');[IO.File]::WriteAllBytes($taskGifPath,$taskEncoded)
 $taskStream=[IO.File]::OpenRead($taskGifPath)
 try{
  $taskDecoder=New-Object Windows.Media.Imaging.GifBitmapDecoder($taskStream,[Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,[Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
  if($taskDecoder.Frames.Count -ne 4){throw 'Encoded GIF frame count differs.'}
  foreach($taskDecodedFrame in $taskDecoder.Frames){if($taskDecodedFrame.PixelWidth -ne 600 -or $taskDecodedFrame.PixelHeight -ne 420 -or [int]$taskDecodedFrame.Metadata.GetQuery('/grctlext/Delay') -ne 110){throw 'GIF delay/dimensions verification failed.'}}
 }finally{$taskStream.Dispose()}
 $taskFallback=Join-Path $OutputDirectory ($taskScene+'.png');Copy-Item -LiteralPath $taskPngs[0].FullName -Destination $taskFallback -Force
 $taskResults+=[pscustomobject]@{Name=$taskScene;Frames=4;PixelWidth=600;PixelHeight=420;DelayMilliseconds=1100;Repeats=$true;Bytes=$taskEncoded.Length;GifSha256=(Get-FileHash -LiteralPath $taskGifPath -Algorithm SHA256).Hash;PngSha256=(Get-FileHash -LiteralPath $taskFallback -Algorithm SHA256).Hash;SourceFrames=@($taskPngs | ForEach-Object{[pscustomobject]@{Name=$_.Name;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})}
}
$taskReport=[ordered]@{Succeeded=$true;SceneCount=5;FrameCount=20;SyntheticProductUi=$true;ActualAuthentication=$false;ActualInstallation=$false;FramesDirectory=$FrameDirectory;Media=$taskResults;CaptureManifest=$taskManifest;CreatedAt=[DateTimeOffset]::Now.ToString('o')}
$taskReport | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
$taskResults | Select-Object Name,Frames,Bytes,GifSha256 | ConvertTo-Json
