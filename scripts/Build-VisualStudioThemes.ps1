#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Build-VisualStudioThemes
desc         : 고정 MIT 기준 테마를 변환하고 AI 확장과 독립된 세 버전의 VSIX를 격리 생성한다.
author       : CodexOrig
create date  : 2026-10-20
update date  : 2026-10-20 CodexOrig : 설치·레지스트리·현재 사용자 테마 변경 없이 색상 컴파일과 패키지만 생성.
update date  : 2026-10-20 CodexOrig4 : TASK069 Dracula 스크롤 손잡이 대비 및 Kakao 탭 상태 대비 보정.
               2026-10-20 CodexOrig : TASK069 IDE 기본 우클릭 메뉴 표면·경계·선택 항목 대비 보정.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version='0.14.1'
)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$themeRoot=Join-Path $repo 'themes/VisualStudio'
$output=[IO.Path]::GetFullPath($OutputDirectory)
if((Test-Path -LiteralPath $output) -and @(Get-ChildItem -LiteralPath $output -Force).Count){throw 'Use an empty output directory; preserve previous package evidence.'}
$null=New-Item -ItemType Directory -Path $output -Force
$compiler='C:/Program Files (x86)/Microsoft Visual Studio/2017/Community/VSSDK/VisualStudioIntegration/Tools/Bin/VsixColorCompiler.exe'
$msbuild='C:/Program Files (x86)/Microsoft Visual Studio/2017/Community/MSBuild/15.0/Bin/MSBuild.exe'
if(!(Test-Path -LiteralPath $compiler)){throw 'VS2017 VSSDK Color Compiler is required to build (not to install) these themes.'}
$utf8=New-Object Text.UTF8Encoding($true)
$definitions=@(
    @{Key='Kakao';Name='Grokeen Kakao Inspired';Guid='{1d41c4a3-cf75-4f0a-b2ff-9a8c2d431b92}';Baseline='PlainDark.xml';Dark=$false},
    @{Key='Dracula';Name='Grokeen Dracula Inspired';Guid='{8ec84919-8a34-422a-abe1-cc238ad4d4b1}';Baseline='PlainDark.xml';Dark=$true}
)
function Set-ThemeColor {
    param([xml]$Document,[string]$Category,[string]$Name,[string]$Hex,[string]$Property='Background')
    $entry=$Document.SelectSingleNode("//Category[@Name='$Category']/Color[@Name='$Name']")
    if(!$entry){throw "Pinned Visual Studio baseline is missing $Category/$Name."}
    $color=$entry.SelectSingleNode($Property)
    if(!$color){$color=$Document.CreateElement($Property);$null=$entry.AppendChild($color)}
    $color.SetAttribute('Type','CT_RAW');$color.SetAttribute('Source',('FF'+$Hex))
}
$generated=Join-Path $output 'definitions';$null=New-Item -ItemType Directory -Path $generated
$registration=New-Object Text.StringBuilder
$definitionRows=@()
foreach($definition in $definitions){
    $source=Join-Path $themeRoot ('Baselines/'+$definition.Baseline)
    if((Get-FileHash -LiteralPath $source).Hash -ne 'B0FCFA9F65736D73175E75E5370D0A9AE27D4EFCB591373D590E1DD7466C9769'){throw 'The pinned Microsoft baseline differs.'}
    [xml]$xml=[IO.File]::ReadAllText($source)
    $theme=$xml.Themes.Theme
    $theme.SetAttribute('Name',$definition.Name);$theme.SetAttribute('GUID',$definition.Guid)
    foreach($systemColor in $xml.SelectNodes('//Background[@Type="CT_SYSCOLOR"] | //Foreground[@Type="CT_SYSCOLOR"]')){
        if([Convert]::ToUInt32($systemColor.Source,16) -gt 255){$systemColor.SetAttribute('Type','CT_RAW')}
    }
    foreach($node in $xml.SelectNodes('//Background[@Type="CT_RAW"] | //Foreground[@Type="CT_RAW"]')){
        $original=$node.GetAttribute('Source');if($original.Length -ne 8){throw 'Invalid source color.'}
        if($original.Substring(0,2) -eq '00'){continue}
        $r=[Convert]::ToInt32($original.Substring(2,2),16);$g=[Convert]::ToInt32($original.Substring(4,2),16);$b=[Convert]::ToInt32($original.Substring(6,2),16)
        $color=[Drawing.Color]::FromArgb($r,$g,$b);$hue=$color.GetHue();$sat=$color.GetSaturation();$brightness=($r*299+$g*587+$b*114)/255000.0
        if(!$definition.Dark -and $sat -lt .18){$brightness=1-$brightness}
        if($definition.Dark){
            if($sat -lt .18){$mapped=if($brightness -lt .22){'282A36'}elseif($brightness -lt .45){'44475A'}elseif($brightness -lt .7){'B9B9C4'}else{'F8F8F2'}}
            else{$mapped=if($hue -lt 18 -or $hue -ge 345){'FF5555'}elseif($hue -lt 45){'FFB86C'}elseif($hue -lt 75){'F1FA8C'}elseif($hue -lt 165){'50FA7B'}elseif($hue -lt 215){'8BE9FD'}elseif($hue -lt 280){'BD93F9'}else{'FF79C6'}}
        }else{
            # 같은 Dark 기준의 중립 명암을 반전하고 파란 선택 표면은 노랑으로 변환한다.
            if($node.LocalName -eq 'Background' -and (($hue -ge 25 -and $hue -le 70 -and $brightness -gt .45) -or ($hue -ge 190 -and $hue -le 250 -and $sat -gt .35))){$mapped=if($brightness -gt .82 -and $hue -lt 100){'F5F5F5'}else{'FEE500'}}
            elseif($sat -lt .2 -or ($hue -ge 25 -and $hue -le 70)){$mapped=if($brightness -lt .3){'191919'}elseif($brightness -lt .62){'5F6368'}elseif($brightness -lt .87){'D9DDE3'}elseif($brightness -lt .98){'F5F5F5'}else{'FFFFFF'}}
            else{$mapped=if($hue -lt 25 -or $hue -ge 340){'B32632'}elseif($hue -lt 75){'8C4A0D'}elseif($hue -lt 165){'19743C'}elseif($hue -lt 255){'1558A6'}else{'6F42C1'}}
        }
        $node.SetAttribute('Source',$original.Substring(0,2)+$mapped)
    }
    # 기본 편집·도구 표면 및 선택 글자는 대비가 분명한 색으로 고정한다.
    foreach($category in $theme.Category){
        foreach($entry in $category.Color){
            if($entry.Name -eq 'Plain Text'){
                foreach($side in @('Background','Foreground')){
                    $part=$entry.SelectSingleNode($side);if(!$part){$part=$xml.CreateElement($side);$null=$entry.AppendChild($part)}
                    $part.SetAttribute('Type','CT_RAW')
                    $value=if($definition.Dark){if($side -eq 'Background'){'FF282A36'}else{'FFF8F8F2'}}else{if($side -eq 'Background'){'FFFFFFFF'}else{'FF191919'}}
                    $part.SetAttribute('Source',$value)
                }
            }
            if($entry.Background -and $entry.Foreground -and $entry.Background.Type -eq 'CT_RAW' -and $entry.Foreground.Type -eq 'CT_RAW'){
                $bg=$entry.Background.Source.Substring(2)
                if($bg -in @('FEE500','BD93F9','8BE9FD','50FA7B','F1FA8C','FFB86C','FF79C6','FF5555')){$entry.Foreground.SetAttribute('Source',$(if($definition.Dark){'FF282A36'}else{'FF191919'}))}
            }
        }
    }
    if($definition.Key -eq 'Dracula'){
        # Keep the scrollbar track dark and the thumb distinct; the grayscale mapper otherwise
        # collapses both Visual Studio tokens to the same Dracula gray and hides the scroll position.
        foreach($name in @('ScrollBar','ScrollBarBackground','ScrollBarArrowBackground','ScrollBarArrowDisabledBackground','ScrollBarArrowMouseOverBackground','ScrollBarArrowPressedBackground','ScrollBarDisabledBackground')){Set-ThemeColor $xml 'Environment' $name '282A36'}
        foreach($name in @('ScrollBarThumbBackground','ScrollBarThumbBorder','ScrollBarThumbGlyph','ScrollBarThumbDisabled')){Set-ThemeColor $xml 'Environment' $name '6272A4'}
        foreach($name in @('ScrollBarThumbMouseOverBackground','ScrollBarThumbMouseOverBorder','ScrollBarThumbGlyphMouseOverBorder')){Set-ThemeColor $xml 'Environment' $name 'BD93F9'}
        foreach($name in @('ScrollBarThumbPressedBackground','ScrollBarThumbPressedBorder','ScrollBarThumbGlyphPressedBorder')){Set-ThemeColor $xml 'Environment' $name 'FF79C6'}
        Set-ThemeColor $xml 'Environment' 'ScrollBarArrowGlyph' 'F8F8F2'
        Set-ThemeColor $xml 'Environment' 'ScrollBarArrowGlyphMouseOver' 'BD93F9'
        Set-ThemeColor $xml 'Environment' 'ScrollBarArrowGlyphPressed' 'FF79C6'
        Set-ThemeColor $xml 'Environment' 'ScrollBarArrowGlyphDisabled' '6272A4'
        # Visual Studio's own context menus use these IDE tokens, separately from WPF assistant menus.
        foreach($name in @('CommandBarMenuBackgroundGradientBegin','CommandBarMenuBackgroundGradientEnd','CommandBarMenuIconBackground')){Set-ThemeColor $xml 'Environment' $name '343746'}
        foreach($name in @('CommandBarMenuBorder','CommandBarMenuSeparator')){Set-ThemeColor $xml 'Environment' $name '7586B5'}
        Set-ThemeColor $xml 'Environment' 'CommandBarMenuItemMouseOver' 'BD93F9'
        Set-ThemeColor $xml 'Environment' 'CommandBarMenuItemMouseOver' '282A36' 'Foreground'
        Set-ThemeColor $xml 'Environment' 'CommandBarMenuItemMouseOverBorder' 'BD93F9'
        foreach($name in @('CommandBarMenuMouseOverSubmenuGlyph','CommandBarMenuSubmenuGlyphHover','CommandBarMenuMouseOverGlyph')){Set-ThemeColor $xml 'Environment' $name '282A36'}
        # Preserve visible hover feedback on standard IDE toolbars, tabs and controls.
        foreach($name in @('CommandBarHover','CommandBarHoverOverSelected','CommandBarHoverOverSelectedIcon','CommandBarMouseOverBackgroundBegin','CommandBarMouseOverBackgroundMiddle1','CommandBarMouseOverBackgroundMiddle2','CommandBarMouseOverBackgroundEnd','CommandBarMouseOverUnfocused')){Set-ThemeColor $xml 'Environment' $name 'BD93F9'}
        foreach($name in @('CommandBarTextHover','CommandBarTextHoverOverSelected')){Set-ThemeColor $xml 'Environment' $name '282A36'}
        Set-ThemeColor $xml 'Environment' 'CommandBarHoverOverSelectedIconBorder' 'BD93F9'
        foreach($name in @('ToolWindowTabMouseOverBackgroundBegin','ToolWindowTabMouseOverBackgroundEnd','AutoHideTabMouseOverBackgroundBegin','AutoHideTabMouseOverBackgroundEnd','FileTabHotGradientTop','FileTabHotGradientBottom')){Set-ThemeColor $xml 'Environment' $name '44475A'}
        foreach($name in @('ToolWindowTabMouseOverText','AutoHideTabMouseOverText','FileTabHotText')){Set-ThemeColor $xml 'Environment' $name 'F8F8F2'}
        foreach($name in @('ToolWindowTabMouseOverBorder','AutoHideTabMouseOverBorder','FileTabHotBorder')){Set-ThemeColor $xml 'Environment' $name 'BD93F9'}
        Set-ThemeColor $xml 'CommonControls' 'ButtonHover' '44475A'
        Set-ThemeColor $xml 'CommonControls' 'ButtonHover' 'F8F8F2' 'Foreground'
        Set-ThemeColor $xml 'CommonControls' 'ButtonBorderHover' 'BD93F9'
        Set-ThemeColor $xml 'CommonControls' 'ComboBoxListItemBackgroundHover' 'BD93F9'
        Set-ThemeColor $xml 'CommonControls' 'ComboBoxListItemBorderHover' 'BD93F9'
        Set-ThemeColor $xml 'CommonControls' 'ComboBoxListItemTextHover' '282A36'
        # Purple breakpoints and pink diagnostics use different visible overview markers.
        Set-ThemeColor $xml 'Text Editor Text Manager Items' 'Indicator Margin' '343746'
        Set-ThemeColor $xml 'ColorizedSignatureHelp colors' 'OverviewMarginBackground' '282A36'
        Set-ThemeColor $xml 'ColorizedSignatureHelp colors' 'OverviewMarginVisible' '343746'
        Set-ThemeColor $xml 'ColorizedSignatureHelp colors' 'OverviewMarginVisible' '7586B5' 'Foreground'
        Set-ThemeColor $xml 'ColorizedSignatureHelp colors' 'MarkerFormatDefinition/BreakpointInScrollBar' 'BD93F9'
        Set-ThemeColor $xml 'ColorizedSignatureHelp colors' 'CurrentLineActiveFormat' '7586B5' 'Foreground'
        foreach($entry in $xml.SelectNodes("//Category[@Name='Text Editor Text Marker Items']/Color[starts-with(@Name,'Breakpoint') and contains(@Name,'Enabled')]")){
            Set-ThemeColor $xml 'Text Editor Text Marker Items' $entry.Name 'BD93F9'
            Set-ThemeColor $xml 'Text Editor Text Marker Items' $entry.Name '282A36' 'Foreground'
        }
        foreach($entry in $xml.SelectNodes("//Category[@Name='Text Editor Text Marker Items']/Color[starts-with(@Name,'Breakpoint') and contains(@Name,'Disabled')]")){
            Set-ThemeColor $xml 'Text Editor Text Marker Items' $entry.Name '7586B5' 'Foreground'
        }
        Set-ThemeColor $xml 'Text Editor Text Marker Items' 'Breakpoint - Selected' 'BD93F9' 'Foreground'
        foreach($name in @('compiler error','syntax error','other error','Error Message')){Set-ThemeColor $xml 'Text Editor Text Marker Items' $name 'FF79C6' 'Foreground'}
        Set-ThemeColor $xml 'Text Editor Text Marker Items' 'compiler warning' 'FFB86C' 'Foreground'
        Set-ThemeColor $xml 'Text Editor Language Service Items' 'Error' 'FF79C6' 'Foreground'
        # WPF error margins read MEF format definitions in category 75A, not legacy TextMarker FF34.
        # Supply only the four requested error/warning colors; preserve suggestion severity defaults.
        $mefCategory=$xml.SelectSingleNode("//Category[@Name='ColorizedSignatureHelp colors']")
        if(!$mefCategory -or $mefCategory.GetAttribute('GUID') -ine '{75a05685-00a8-4ded-bae5-e7a50bfa929a}'){throw 'Pinned MEF font/color category differs.'}
        foreach($name in @('syntax error','compiler error','other error','compiler warning')){
            $color=$mefCategory.SelectSingleNode("Color[@Name='$name']")
            if(!$color){$color=$xml.CreateElement('Color');$color.SetAttribute('Name',$name);$null=$mefCategory.AppendChild($color)}
            $foreground=$color.SelectSingleNode('Foreground')
            if(!$foreground){$foreground=$xml.CreateElement('Foreground');$null=$color.AppendChild($foreground)}
            $foreground.SetAttribute('Type','CT_RAW');$foreground.SetAttribute('Source',$(if($name -eq 'compiler warning'){'FFFFB86C'}else{'FFFF79C6'}))
        }
        foreach($entry in $xml.SelectNodes("//Category[@Name='Text Editor Text Marker Items']/Color[starts-with(@Name,'Breakpoint') and (contains(@Name,'Warning') or contains(@Name,'Error'))]")){
            Set-ThemeColor $xml 'Text Editor Text Marker Items' $entry.Name $(if($entry.Name.Contains('Error')){'FF79C6'}else{'FFB86C'})
            Set-ThemeColor $xml 'Text Editor Text Marker Items' $entry.Name '282A36' 'Foreground'
        }
    }
    if($definition.Key -eq 'Kakao'){
        # Give normal, hovered and selected tabs separate surfaces with a consistent dark label.
        Set-ThemeColor $xml 'Cider' 'TabItem' 'D9DDE3'
        Set-ThemeColor $xml 'Cider' 'TabItem' '191919' 'Foreground'
        Set-ThemeColor $xml 'Cider' 'TabItemMouseOver' '34363A'
        Set-ThemeColor $xml 'Cider' 'TabItemMouseOver' 'FFFFFF' 'Foreground'
        Set-ThemeColor $xml 'Cider' 'TabItemMouseOverBorder' 'FEE500'
        Set-ThemeColor $xml 'Cider' 'TabItemSelected' 'FEE500'
        Set-ThemeColor $xml 'Cider' 'TabItemSelected' '191919' 'Foreground'
        Set-ThemeColor $xml 'Cider' 'TabItemSelectedBorder' 'B38B00'
        foreach($name in @('FileTabHotGradientTop','FileTabHotGradientBottom')){Set-ThemeColor $xml 'Environment' $name '34363A'}
        Set-ThemeColor $xml 'Environment' 'FileTabHotText' 'FFFFFF'
        Set-ThemeColor $xml 'Environment' 'FileTabHotBorder' 'FEE500'
        foreach($name in @('FileTabSelectedBackground','FileTabSelectedGradientTop','FileTabSelectedGradientBottom','FileTabSelectedGradientMiddle1','FileTabSelectedGradientMiddle2')){Set-ThemeColor $xml 'Environment' $name 'FEE500'}
        Set-ThemeColor $xml 'Environment' 'FileTabSelectedText' '191919'
        Set-ThemeColor $xml 'Environment' 'FileTabText' '191919'
        foreach($name in @('ToolWindowTabMouseOverBackgroundBegin','ToolWindowTabMouseOverBackgroundEnd','AutoHideTabMouseOverBackgroundBegin','AutoHideTabMouseOverBackgroundEnd')){Set-ThemeColor $xml 'Environment' $name '34363A'}
        foreach($name in @('ToolWindowTabMouseOverBorder','AutoHideTabMouseOverBorder')){Set-ThemeColor $xml 'Environment' $name 'FEE500'}
        foreach($name in @('ToolWindowTabMouseOverText','AutoHideTabMouseOverText')){Set-ThemeColor $xml 'Environment' $name 'FFFFFF'}
        Set-ThemeColor $xml 'Environment' 'ToolWindowTabSelectedText' '191919'
        Set-ThemeColor $xml 'Environment' 'ToolWindowTabSelectedTab' 'FEE500'
        Set-ThemeColor $xml 'Environment' 'ToolWindowTabSelectedBorder' 'B38B00'
    }
    $settings=New-Object Xml.XmlWriterSettings;$settings.Encoding=$utf8;$settings.Indent=$true
    $path=Join-Path $generated ($definition.Key+'.xml');$writer=[Xml.XmlWriter]::Create($path,$settings)
    try{$xml.Save($writer)}finally{$writer.Dispose()}
    Copy-Item -LiteralPath $path -Destination (Join-Path $generated ($definition.Key+'.vstheme'))
    $pkgdef=Join-Path $generated ($definition.Key+'.pkgdef')
    & $compiler $path $pkgdef /noLogo *> (Join-Path $generated ($definition.Key+'-compiler.log'))
    if($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $pkgdef)){throw ('Theme compilation failed: '+$definition.Key)}
    $null=$registration.AppendLine('[$RootKey$\Themes\'+$definition.Guid+']').AppendLine('@="'+$definition.Name+'"').AppendLine('"Name"="'+$definition.Name+'"').AppendLine()
    $definitionRows+=[pscustomobject]@{Name=$definition.Name;Guid=$definition.Guid;BaselineSha256=(Get-FileHash -LiteralPath $source).Hash;Definition=$path;DefinitionSha256=(Get-FileHash -LiteralPath $path).Hash;PkgdefSha256=(Get-FileHash -LiteralPath $pkgdef).Hash;Categories=@($theme.Category).Count;Colors=@($theme.Category.Color).Count}
}
[IO.File]::WriteAllText((Join-Path $generated 'ThemeRegistration.pkgdef'),$registration.ToString(),$utf8)
$packages=@()
foreach($year in @('2017','2019','2022')){
    $folder=Join-Path $output ('source/'+$year);$null=New-Item -ItemType Directory -Path (Join-Path $folder 'Themes') -Force
    foreach($name in @('VisualStudioThemes.csproj','ThemePackageMarker.cs','THIRD-PARTY-NOTICES.md')){Copy-Item -LiteralPath (Join-Path $themeRoot $name) -Destination $folder}
    $targetMsbuild=$msbuild
    if($year -eq '2022'){
        $projectFile=Join-Path $folder 'VisualStudioThemes.csproj'
        $projectText=[IO.File]::ReadAllText($projectFile).Replace('15.9.3032','17.0.5241').Replace('v4.6.2','v4.7.2')
        [IO.File]::WriteAllText($projectFile,$projectText,$utf8)
        $targetMsbuild='C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe'
    }
    Copy-Item -LiteralPath (Join-Path $themeRoot 'Licenses') -Destination $folder -Recurse
    Copy-Item -LiteralPath (Join-Path $generated 'ThemeRegistration.pkgdef') -Destination $folder
    foreach($definition in $definitions){Copy-Item -LiteralPath (Join-Path $generated ($definition.Key+'.pkgdef')) -Destination (Join-Path $folder 'Themes')}
    $range=switch($year){'2017'{'[15.0,16.0)'}'2019'{'[16.0,17.0)'}default{'[17.0,18.0)'}}
    $architecture=if($year -eq '2022'){'<ProductArchitecture>amd64</ProductArchitecture>'}else{''}
    $manifest=@"
<?xml version="1.0" encoding="utf-8"?>
<PackageManifest Version="2.0.0" xmlns="http://schemas.microsoft.com/developer/vsx-schema/2011">
 <Metadata><Identity Id="Grokeen.VisualStudioThemes.$year" Version="$Version" Language="en-US" Publisher="Grokeen"/><DisplayName>Grokeen Independent Themes $year</DisplayName><Description xml:space="preserve">Optional Kakao-inspired and Dracula-inspired Visual Studio IDE themes. No AI extension or CLI dependency.</Description><MoreInfo>https://github.com/Grokeen/VisualStudio2017_Codex_Claude_Extension</MoreInfo></Metadata>
 <Installation><InstallationTarget Id="Microsoft.VisualStudio.Community" Version="$range">$architecture</InstallationTarget></Installation>
 <Dependencies />
 <Assets><Asset Type="Microsoft.VisualStudio.VsPackage" Path="ThemeRegistration.pkgdef"/><Asset Type="Microsoft.VisualStudio.VsPackage" Path="Themes\Kakao.pkgdef"/><Asset Type="Microsoft.VisualStudio.VsPackage" Path="Themes\Dracula.pkgdef"/></Assets>
 <Prerequisites><Prerequisite Id="Microsoft.VisualStudio.Component.CoreEditor" Version="$range" DisplayName="Visual Studio core editor"/></Prerequisites>
</PackageManifest>
"@
    [IO.File]::WriteAllText((Join-Path $folder 'source.extension.vsixmanifest'),$manifest,$utf8)
    $log=Join-Path $output ('build-'+$year+'.log')
    & $targetMsbuild (Join-Path $folder 'VisualStudioThemes.csproj') /restore /t:Rebuild /m /nologo /v:minimal /p:Configuration=Release /p:DeployExtension=false /p:ZipPackageCompressionLevel=normal *> $log
    $code=$LASTEXITCODE;$raw=[IO.File]::ReadAllText($log)
    if($code -ne 0 -or [regex]::IsMatch($raw,'(?im)^.*:\s*(warning|error)\s+\w+')){throw ('Theme VSIX build failed or warned: '+$year+'; '+$log)}
    $vsix=Get-ChildItem -LiteralPath (Join-Path $folder 'bin/Release') -Filter '*.vsix' -File
    if(@($vsix).Count -ne 1){throw 'Expected one independent theme VSIX.'}
    $target=Join-Path $output ('Grokeen-VisualStudio-Themes-'+$Version+'-VS'+$year+'.vsix');Copy-Item -LiteralPath $vsix.FullName -Destination $target
    $packages+=[pscustomobject]@{Year=$year;Path=$target;Bytes=(Get-Item -LiteralPath $target).Length;Sha256=(Get-FileHash -LiteralPath $target).Hash;BuildExitCode=$code;Warnings=0;Errors=0}
    Write-Output ('Built independent theme VSIX: '+$year)
}
[pscustomobject]@{Version=$Version;Definitions=$definitionRows;Packages=$packages;ActualIdeInstalled=$false;CurrentThemeChanged=$false} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding UTF8
