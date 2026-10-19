#requires -Version 5.1
[CmdletBinding()]
param([string]$OutputDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(!$OutputDirectory){$OutputDirectory=Join-Path $repository ('artifacts/task050/theme-presets-'+[Guid]::NewGuid().ToString('N'))}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if((Test-Path -LiteralPath $OutputDirectory) -and @(Get-ChildItem -LiteralPath $OutputDirectory -Force).Count){throw 'Use a fresh output directory; existing evidence is preserved.'}
$null=New-Item -ItemType Directory -Path $OutputDirectory -Force
Add-Type -AssemblyName WindowsBase,PresentationCore,PresentationFramework
$inputs=@()
foreach($name in @('AssistantThemePolicy.cs','AssistantPalette.cs')){
    $source=Join-Path $repository ('src/VS2017.AIAssistant/Views/'+$name);$target=Join-Path $OutputDirectory $name
    $hash=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash;Copy-Item -LiteralPath $source -Destination $target
    if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $hash){throw 'Palette source copy hash mismatch.'}
    $inputs+=[pscustomobject]@{Path=$source;Sha256=$hash}
}
$compiler='C:/Program Files (x86)/Microsoft Visual Studio/2017/Community/MSBuild/15.0/Bin/Roslyn/csc.exe'
$references='C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.6.2'
if(!(Test-Path -LiteralPath $compiler)){throw 'VS2017 C# compiler is unavailable.'}
$assembly=Join-Path $OutputDirectory 'ThemePresets.SourceOnly.dll'
& $compiler '/nologo' '/target:library' '/langversion:7' '/codepage:65001' '/warnaserror+' '/nostdlib+' ('/out:'+$assembly) ('/lib:'+$references) '/reference:mscorlib.dll' '/reference:System.dll' '/reference:WindowsBase.dll' '/reference:PresentationCore.dll' '/reference:PresentationFramework.dll' (Join-Path $OutputDirectory 'AssistantThemePolicy.cs') (Join-Path $OutputDirectory 'AssistantPalette.cs') 2>&1 | Tee-Object -FilePath (Join-Path $OutputDirectory 'compile.log')
if($LASTEXITCODE -ne 0){throw 'Theme preset source-only compile failed.'}
[Reflection.Assembly]::LoadFrom($assembly)|Out-Null
$script:checks=New-Object 'Collections.Generic.List[object]'
function Add-Check([string]$name,[bool]$passed,[string]$detail){$script:checks.Add([pscustomobject]@{Name=$name;Passed=$passed;Detail=$detail})}
function Get-Luminance([Windows.Media.Color]$color){
    $channels=@($color.R,$color.G,$color.B)|ForEach-Object{$value=$_/255.0;if($value -le 0.04045){$value/12.92}else{[Math]::Pow(($value+0.055)/1.055,2.4)}}
    return 0.2126*$channels[0]+0.7152*$channels[1]+0.0722*$channels[2]
}
function Get-Contrast([Windows.Media.Color]$a,[Windows.Media.Color]$b){$la=Get-Luminance $a;$lb=Get-Luminance $b;return ([Math]::Max($la,$lb)+0.05)/([Math]::Min($la,$lb)+0.05)}
foreach($preset in @('vs','kakao','dracula')){
    foreach($mode in @('dark','light')){
        $resources=New-Object Windows.ResourceDictionary
        [VS2017.AIAssistant.Views.AssistantPalette]::Apply($resources,$mode -eq 'light')
        $before=$resources['AssistantBackgroundBrush'].Color.ToString()
        [VS2017.AIAssistant.Views.AssistantPalette]::ApplyPreset($resources,$preset,$mode -eq 'light')
        if($preset -eq 'vs'){
            Add-Check "$mode VS preset preserves the host palette" ($resources['AssistantBackgroundBrush'].Color.ToString() -eq $before) $before
            continue
        }
        foreach($surface in @('AssistantBackgroundBrush','AssistantSurfaceBrush','AssistantPopupBackgroundBrush')){
            foreach($text in @('AssistantForegroundBrush','AssistantMutedForegroundBrush','AssistantLinkBrush')){
                $ratio=Get-Contrast $resources[$surface].Color $resources[$text].Color
                Add-Check "$preset/$mode $text on $surface contrast" ($ratio -ge 4.5) ([Math]::Round($ratio,3).ToString())
            }
        }
        foreach($surface in @('AssistantPrimaryBrush','AssistantPrimaryHoverBrush')){
            $ratio=Get-Contrast $resources[$surface].Color $resources['AssistantOnPrimaryBrush'].Color
            Add-Check "$preset/$mode primary foreground on $surface contrast" ($ratio -ge 4.5) ([Math]::Round($ratio,3).ToString())
        }
    }
}
$xaml=[IO.File]::ReadAllText((Join-Path $repository 'src/VS2017.AIAssistant/Views/AssistantControl.xaml'))
$dialog=[IO.File]::ReadAllText((Join-Path $repository 'src/VS2017.AIAssistant/Views/AssistantDialogs.cs'))
$editor=[IO.File]::ReadAllText((Join-Path $repository 'src/VS2017.AIAssistant/Views/MarkdownEditor.cs'))
$markdownDialog=[IO.File]::ReadAllText((Join-Path $repository 'src/VS2017.AIAssistant/Views/AssistantControl.Markdown.cs'))
$themeRuntime=[IO.File]::ReadAllText((Join-Path $repository 'src/VS2017.AIAssistant/Views/AssistantControl.Theme.cs'))
Add-Check 'Settings has Visual Studio, Kakao and Dracula choices' ($xaml.Contains('Tag="vs"') -and $xaml.Contains('Tag="kakao"') -and $xaml.Contains('Tag="dracula"')) ''
Add-Check 'Settings includes Windows completion notification control' ($xaml.Contains('x:Name="CompletionNotificationsChoice"')) ''
Add-Check 'Settings navigation selects one left-side page at a time' ($dialog.Contains('SetSectionPages') -and $dialog.Contains('ShowSectionPage')) ''
Add-Check 'Guide return button stays at the top-left outside the reading scroll' ($dialog.Contains('Content = page') -and $dialog.Contains('HorizontalAlignment = HorizontalAlignment.Left')) ''
Add-Check 'Markdown popup uses a larger work-area-bounded size' ($markdownDialog.Contains('Math.Min(1040') -and $markdownDialog.Contains('Math.Min(780')) ''
Add-Check 'Markdown toolbar uses functional groups' ([regex]::Matches($editor,'AddToolGroup\(tools,').Count -ge 6) 'Expected six functional groups.'
Add-Check 'Markdown search checkboxes use the shared CheckBox style' ($editor.Contains('SetResourceReference(FrameworkElement.StyleProperty,typeof(CheckBox))')) ''
Add-Check 'Claude and Codex tabs expose arrow-key navigation and spacing' ($xaml.Contains('KeyboardNavigation.DirectionalNavigation="Contained"') -and $xaml.Contains('Margin="0,0,22,0"')) ''
Add-Check 'High Contrast keeps the host palette instead of a named preset' ($themeRuntime.Contains('SystemParameters.HighContrast') -and $themeRuntime.Contains('AssistantPalette.ApplyPreset')) ''
$failed=@($checks|Where-Object{-not $_.Passed})
$result=[pscustomobject]@{Succeeded=$failed.Count -eq 0;Passed=$checks.Count-$failed.Count;Failed=$failed.Count;Checks=$checks.ToArray();Inputs=$inputs;SourceOnly=$true;ProductAssemblyTested=$false;WindowShown=$false;VisualStudioThemeChanged=$false;CheckedAt=[DateTimeOffset]::Now.ToString('o')}
$result|ConvertTo-Json -Depth 7|Set-Content -LiteralPath (Join-Path $OutputDirectory 'result.json') -Encoding UTF8
[pscustomobject]@{Succeeded=$result.Succeeded;Passed=$result.Passed;Failed=$result.Failed;Errors=$failed;OutputDirectory=$OutputDirectory}|ConvertTo-Json -Depth 6
if($failed.Count){throw 'Theme preset checks failed; original results are preserved.'}
