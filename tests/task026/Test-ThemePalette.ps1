#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-ThemePalette
desc         : 실제 팔레트 소스의 양 모드 대비·동적 자원 완전성·중복 스타일을 검사한다.
author       : CodexOrig4
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig4 : GUI·공유 빌드 없이 고정 소스 사본과 원래 실패 근거 보존.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$OutputDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(!$OutputDirectory){$OutputDirectory=Join-Path $repository ('artifacts/task026/palette-'+[Guid]::NewGuid().ToString('N'))}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if((Test-Path -LiteralPath $OutputDirectory) -and @(Get-ChildItem -LiteralPath $OutputDirectory -Force).Count){throw '새 출력 폴더를 지정하세요.'}
$null=New-Item -ItemType Directory -Path $OutputDirectory -Force
Add-Type -AssemblyName WindowsBase,PresentationCore,PresentationFramework,System.Xaml
$inputs=@()
foreach($name in @('AssistantThemePolicy.cs','AssistantPalette.cs','AssistantTheme.xaml','AssistantControl.xaml')){
    $inputPath=Join-Path $repository ('src/VS2017.AIAssistant/Views/'+$name)
    $hash=(Get-FileHash -LiteralPath $inputPath).Hash
    $target=Join-Path $OutputDirectory $name
    Copy-Item -LiteralPath $inputPath -Destination $target
    if((Get-FileHash -LiteralPath $target).Hash -ne $hash){throw '소스 사본 해시 불일치'}
    $inputs+=[pscustomobject]@{Path=$inputPath;Sha256=$hash}
}
$compiler='C:/Program Files (x86)/Microsoft Visual Studio/2017/Community/MSBuild/15.0/Bin/Roslyn/csc.exe'
$references='C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.6.2'
$assembly=Join-Path $OutputDirectory 'ThemePalette.SourceOnly.dll'
& $compiler '/nologo' '/target:library' '/langversion:7' '/codepage:65001' '/warnaserror+' '/nostdlib+' ('/out:'+$assembly) ('/lib:'+$references) '/reference:mscorlib.dll' '/reference:System.dll' '/reference:WindowsBase.dll' '/reference:PresentationCore.dll' '/reference:PresentationFramework.dll' (Join-Path $OutputDirectory 'AssistantThemePolicy.cs') (Join-Path $OutputDirectory 'AssistantPalette.cs') 2>&1 | Tee-Object -FilePath (Join-Path $OutputDirectory 'compile.log')
if($LASTEXITCODE -ne 0){throw '팔레트 독립 컴파일 실패'}
[Reflection.Assembly]::LoadFrom($assembly)|Out-Null
$script:checks=New-Object 'Collections.Generic.List[object]'
function Add-ThemeCheck([string]$name,[bool]$passed,[string]$detail){$script:checks.Add([pscustomobject]@{Name=$name;Passed=$passed;Detail=$detail})}
function Get-ThemeLuminance([Windows.Media.Color]$color){
    $channels=@($color.R,$color.G,$color.B)|ForEach-Object{$v=$_/255.0;if($v -le 0.04045){$v/12.92}else{[Math]::Pow(($v+0.055)/1.055,2.4)}}
    return 0.2126*$channels[0]+0.7152*$channels[1]+0.0722*$channels[2]
}
$dicts=@{}
foreach($mode in @('Dark','Light')){
    $resources=New-Object Windows.ResourceDictionary
    [VS2017.AIAssistant.Views.AssistantPalette]::Apply($resources,$mode -eq 'Light')
    $dicts[$mode]=$resources
    foreach($surface in @('AssistantBackgroundBrush','AssistantSurfaceBrush','AssistantPopupBackgroundBrush','AssistantCodeBackgroundBrush','AssistantSelectionBrush','AssistantControlBackgroundBrush','AssistantHoverBrush','AssistantPressedBrush')){
        foreach($text in @('AssistantForegroundBrush','AssistantMutedForegroundBrush','AssistantLinkBrush')){
            $a=Get-ThemeLuminance $resources[$surface].Color;$b=Get-ThemeLuminance $resources[$text].Color
            $contrast=([Math]::Max($a,$b)+0.05)/([Math]::Min($a,$b)+0.05)
            Add-ThemeCheck "$mode $text / $surface" ($contrast -ge 4.5) ('Contrast='+[Math]::Round($contrast,3))
        }
    }
    foreach($text in @('AssistantAddedBrush','AssistantRemovedBrush','AssistantWarningBrush','AssistantErrorBrush')){
        $a=Get-ThemeLuminance $resources['AssistantBackgroundBrush'].Color;$b=Get-ThemeLuminance $resources[$text].Color
        $contrast=([Math]::Max($a,$b)+0.05)/([Math]::Min($a,$b)+0.05)
        Add-ThemeCheck "$mode semantic $text" ($contrast -ge 4.5) ('Contrast='+[Math]::Round($contrast,3))
    }
    foreach($surface in @('AssistantPrimaryBrush','AssistantPrimaryHoverBrush')){
        $a=Get-ThemeLuminance $resources[$surface].Color;$b=Get-ThemeLuminance $resources['AssistantOnPrimaryBrush'].Color
        $contrast=([Math]::Max($a,$b)+0.05)/([Math]::Min($a,$b)+0.05)
        Add-ThemeCheck "$mode primary label / $surface" ($contrast -ge 4.5) ('Contrast='+[Math]::Round($contrast,3))
    }
}
$background=$dicts['Light']['AssistantBackgroundBrush'].Color
Add-ThemeCheck 'Light main surface is bright and nearly neutral' ((Get-ThemeLuminance $background) -gt 0.8 -and ([Math]::Abs([int]$background.R-$background.B)) -le 10) $background.ToString()
Add-Type -Path (Join-Path $PSScriptRoot 'PaletteDocumentProbe.cs') -ReferencedAssemblies @('System.dll',[Windows.DependencyObject].Assembly.Location,[Windows.Media.Visual].Assembly.Location,[Windows.Window].Assembly.Location,[System.Xaml.XamlReader].Assembly.Location)
$publication=New-Object Windows.ResourceDictionary
[VS2017.AIAssistant.Views.AssistantPalette]::Apply($publication,$false)
$observer=New-Object PaletteDocumentProbe -ArgumentList $publication
foreach($mode in @('Light','Dark')){
    [VS2017.AIAssistant.Views.AssistantPalette]::Apply($publication,$mode -eq 'Light')
    Add-ThemeCheck "$mode publishes complete code colors before document refresh" ($observer.CachedCode -eq $publication['AssistantCodeBackgroundBrush'].Color) ''
    Add-ThemeCheck "$mode publishes complete link colors before document refresh" ($observer.CachedLink -eq $publication['AssistantLinkBrush'].Color) ''
}
$allKeys=@{}
foreach($key in $dicts['Light'].Keys){$allKeys[[string]$key]=$true}
foreach($name in @('AssistantTheme.xaml','AssistantControl.xaml')){
    $raw=[IO.File]::ReadAllText((Join-Path $OutputDirectory $name))
    [xml]$xml=$raw
    if($name -eq 'AssistantTheme.xaml'){
        $keys=@()
        foreach($node in $xml.DocumentElement.ChildNodes){
            if($node.NodeType -ne 'Element'){continue}
            $key=$node.GetAttribute('Key','http://schemas.microsoft.com/winfx/2006/xaml')
            if(!$key -and $node.LocalName -eq 'Style'){$key='implicit:'+$node.GetAttribute('TargetType')}
            if($key){$keys+=$key;$allKeys[$key]=$true}
        }
        $duplicates=@($keys|Group-Object|Where-Object Count -gt 1)
        Add-ThemeCheck 'Shared theme has no duplicate resource keys' ($duplicates.Count -eq 0) (($duplicates|ForEach-Object Name)-join ', ')
    }
    $references=@([regex]::Matches($raw,'\{DynamicResource (Assistant[A-Za-z0-9]+)\}')|ForEach-Object{$_.Groups[1].Value}|Sort-Object -Unique)
    foreach($key in $references){Add-ThemeCheck "$name resolves $key" ($allKeys.ContainsKey($key)) ''}
    $fixed=@(foreach($node in $xml.SelectNodes('//*')) {
        if($node.LocalName -eq 'SolidColorBrush'){continue}
        foreach($attribute in $node.Attributes){
            $brushAttribute=$attribute.Name -in @('Background','Foreground','BorderBrush','Fill','Stroke','Color')
            if($node.LocalName -eq 'Setter' -and $attribute.Name -eq 'Value' -and $node.GetAttribute('Property') -in @('Background','Foreground','BorderBrush','Fill','Stroke','Color')){$brushAttribute=$true}
            if($brushAttribute -and $attribute.Value -match '^(#[0-9A-Fa-f]{6,8}|White|Black|Silver|Gainsboro)$'){$node.OuterXml}
        }
    })
    Add-ThemeCheck "$name has no fixed palette colors" ($fixed.Count -eq 0) ('FixedColorCount='+$fixed.Count)
}
$failed=@($checks|Where-Object{!$_.Passed}).Count
$result=[pscustomobject]@{Succeeded=$failed -eq 0;Passed=$checks.Count-$failed;Failed=$failed;Checks=$checks.ToArray();SourceOnly=$true;ProductAssemblyTested=$false;Inputs=$inputs;WindowShown=$false;CheckedAt=[DateTimeOffset]::Now.ToString('o')}
$result|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $OutputDirectory 'result.json') -Encoding UTF8
[pscustomobject]@{Succeeded=$result.Succeeded;Passed=$result.Passed;Failed=$failed;Errors=@($checks|Where-Object{!$_.Passed});OutputDirectory=$OutputDirectory}|ConvertTo-Json -Depth 5
if($failed){throw '팔레트 검사 실패: 원본 결과를 보존합니다.'}
