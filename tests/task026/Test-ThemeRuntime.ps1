#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-ThemeRuntime
desc         : 테마 변경·자원 전달·선택 복원·Thinking 대비·수명 해제를 메모리 WPF에서 검사한다.
author       : CodexOrig4
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig4 : source fixture/고정 제품 DLL 결과를 구분하고 GUI 없이 PNG를 렌더링한다.
               2026-10-19 CodexOrig : SDK17 standalone 검사의 설치 IDE interop 의존성만 명시적으로 해석.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$AssemblyPath,[string]$OutputDirectory,[switch]$AnalyzeSdk)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA'){throw 'Windows PowerShell 5.1 -STA required.'}
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(!$OutputDirectory){$OutputDirectory=Join-Path $repository ('artifacts/task026/runtime-'+[Guid]::NewGuid().ToString('N'))}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if((Test-Path -LiteralPath $OutputDirectory) -and @(Get-ChildItem -LiteralPath $OutputDirectory -Force).Count){throw 'Use a new output directory.'}
$null=New-Item -ItemType Directory -Path $OutputDirectory -Force
foreach($framework in @('WindowsBase','PresentationCore','PresentationFramework','System.Xaml','System.Drawing','System.Web.Extensions')){Add-Type -AssemblyName $framework}
$assetsRelative='src/VS2017.AIAssistant/obj/project.assets.json'
if(![string]::IsNullOrWhiteSpace($AssemblyPath)){
    $metadata=[Reflection.Assembly]::ReflectionOnlyLoadFrom((Resolve-Path -LiteralPath $AssemblyPath).Path)
    $shellReference=$metadata.GetReferencedAssemblies()|Where-Object Name -eq 'Microsoft.VisualStudio.Shell.15.0'|Select-Object -First 1
    if($shellReference.Version.Major -ge 17){$assetsRelative='src/VS2022.AIAssistant/obj/project.assets.json'}
    elseif($shellReference.Version.Major -ge 16){$assetsRelative='src/VS2019.AIAssistant/obj/project.assets.json'}
}
$assetsPath=Join-Path $repository $assetsRelative
$assets=Get-Content -LiteralPath $assetsPath -Raw -Encoding UTF8|ConvertFrom-Json
$packageRoot=$assets.packageFolders.PSObject.Properties.Name|Select-Object -First 1
$references=@{}
$target=$assets.targets.PSObject.Properties|Where-Object Name -like '.NETFramework,Version=*'|Select-Object -First 1
foreach($entry in $target.Value.PSObject.Properties){
    if($entry.Value.PSObject.Properties.Name -contains 'compile'){
        foreach($item in $entry.Value.compile.PSObject.Properties.Name){
            if($item -like '*.dll'){$references[[IO.Path]::GetFileNameWithoutExtension($item)]=Join-Path $packageRoot ($assets.libraries.($entry.Name).path+'/'+$item)}
        }
    }
}
$sourceOnly=[string]::IsNullOrWhiteSpace($AssemblyPath)
if(!$sourceOnly -and $shellReference.Version.Major -ge 17){
    # SDK17의 COM interop은 IDE 제공 파일이다. 제품 패키지에 복사하지 않고 독립 검사 resolver에서만 사용한다.
    $vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    $ideInstances=& $vswhere -all -products '*' -format json -utf8 | ConvertFrom-Json
    $runtimeIde=$ideInstances|Where-Object {([version]$_.installationVersion).Major -eq 17}|Select-Object -First 1
    if(!$runtimeIde){throw 'An installed VS2022 runtime is required for the standalone theme test.'}
    $runtimePublic=Join-Path $runtimeIde.installationPath 'Common7/IDE/PublicAssemblies'
    $runtimePrivate=Join-Path $runtimeIde.installationPath 'Common7/IDE/PrivateAssemblies'
    foreach($name in @($references.Keys)){
        foreach($directory in @($runtimePublic,$runtimePrivate)){
            $runtimePath=Join-Path $directory ($name+'.dll')
            if(Test-Path -LiteralPath $runtimePath -PathType Leaf){$references[$name]=$runtimePath;break}
        }
    }
    # Shell과 private interop은 같은 설치본으로 맞춘다. 서로 다른 VS 서비스 버전의 혼용을 피한다.
    $installedInterop=Join-Path $runtimePrivate 'Microsoft.Internal.VisualStudio.Interop.dll'
    if(!(Test-Path -LiteralPath $installedInterop -PathType Leaf)){throw 'VS2022 IDE interop dependency is required for the standalone theme test.'}
    $references['Microsoft.Internal.VisualStudio.Interop']=$installedInterop
}
$sourceInputs=@();$productHash=$null;$productDirectory=$null
$resolver=[ResolveEventHandler]{param($sender,$eventArgs)
    $name=(New-Object Reflection.AssemblyName($eventArgs.Name)).Name
    if($productDirectory){$local=Join-Path $productDirectory ($name+'.dll');if(Test-Path -LiteralPath $local -PathType Leaf){return [Reflection.Assembly]::LoadFrom($local)}}
    if($references.ContainsKey($name)){return [Reflection.Assembly]::LoadFrom($references[$name])}
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
$oldSettings=$env:VS2017_AI_SETTINGS_PATH;$oldWorkspace=$env:VS2017_AI_WORKSPACE_PATH;$oldDiscovery=$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY
$view=$null;$app=$null
try{
    $env:VS2017_AI_SETTINGS_PATH=Join-Path $OutputDirectory 'settings.json';$env:VS2017_AI_WORKSPACE_PATH=Join-Path $OutputDirectory 'workspace';$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY='1'
    [IO.File]::WriteAllText($env:VS2017_AI_SETTINGS_PATH,'{"ThemeMode":"dark","HideWelcome":true,"ShowHistory":true,"FontSize":14}')
    $sourceNames=@('AssistantThemePolicy.cs','AssistantPalette.cs','AssistantControl.Theme.cs','AssistantControl.Thinking.cs','AssistantTheme.xaml')
    if($sourceOnly){
        $compiler='C:/Program Files (x86)/Microsoft Visual Studio/2017/Community/MSBuild/15.0/Bin/Roslyn/csc.exe'
        $frameworkPath='C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.6.2'
        $loadedPath=Join-Path $OutputDirectory 'Theme.SourceOnly.dll'
        $argsList=@('/nologo','/target:library','/langversion:7','/codepage:65001','/warnaserror+','/nostdlib+',('/out:'+$loadedPath),('/lib:'+$frameworkPath))
        foreach($ref in @('mscorlib','System','System.Core','System.Xaml','System.Drawing','WindowsBase','PresentationCore','PresentationFramework')){$argsList+=('/reference:'+ $ref+'.dll')}
        foreach($ref in $references.Values){$argsList+=('/reference:'+$ref)}
        if($AnalyzeSdk){
            $analyzerEntry=$assets.libraries.PSObject.Properties|Where-Object Name -like 'Microsoft.VisualStudio.Threading.Analyzers/*'|Select-Object -First 1
            $analyzerPath=Join-Path $packageRoot ($analyzerEntry.Value.path+'/analyzers/cs/Microsoft.VisualStudio.Threading.Analyzers.dll')
            $argsList+=('/analyzer:'+$analyzerPath)
        }
        foreach($name in $sourceNames){
            $path=Join-Path $repository ('src/VS2017.AIAssistant/Views/'+$name);$hash=(Get-FileHash -LiteralPath $path).Hash;$copy=Join-Path $OutputDirectory $name;Copy-Item -LiteralPath $path -Destination $copy
            if((Get-FileHash -LiteralPath $copy).Hash -ne $hash){throw 'Frozen input mismatch.'}
            $sourceInputs+=[pscustomobject]@{Path=$path;Sha256=$hash}
            if($name -like '*.cs'){$argsList+=$copy}
        }
        $argsList+=Join-Path $PSScriptRoot 'ThemeHostFixture.cs'
        & $compiler @argsList 2>&1|Tee-Object -FilePath (Join-Path $OutputDirectory 'compile.log')
        if($LASTEXITCODE -ne 0){throw 'Theme source runtime compilation failed.'}
    }else{
        $AssemblyPath=(Resolve-Path -LiteralPath $AssemblyPath).Path;$productDirectory=Split-Path -Parent $AssemblyPath
        $productHash=(Get-FileHash -LiteralPath $AssemblyPath).Hash
        $loadedPath=Join-Path $OutputDirectory 'VS2017.AIAssistant.dll';Copy-Item -LiteralPath $AssemblyPath -Destination $loadedPath
        if((Get-FileHash -LiteralPath $loadedPath).Hash -ne $productHash){throw 'Frozen product DLL mismatch.'}
    }
    $assembly=[Reflection.Assembly]::LoadFrom($loadedPath)
    [Reflection.Assembly]::LoadFrom($references['Microsoft.VisualStudio.Shell.15.0'])|Out-Null
    if(!$sourceOnly -and $assembly.GetName().Name -ne 'VS2017.AIAssistant'){throw 'Expected actual product DLL.'}
    $app=New-Object Windows.Application
    $app.ShutdownMode=[Windows.ShutdownMode]::OnExplicitShutdown
    if($sourceOnly){
        $xaml=[IO.File]::ReadAllText((Join-Path $OutputDirectory 'AssistantTheme.xaml'))
        $xaml=$xaml.Replace('xmlns:local="clr-namespace:VS2017.AIAssistant.Views"','xmlns:local="clr-namespace:VS2017.AIAssistant.Views;assembly=Theme.SourceOnly"')
        $resources=[Windows.Markup.XamlReader]::Parse($xaml)
        $assembly.GetType('VS2017.AIAssistant.Views.ThemeFixtureResources').GetField('Current').SetValue($null,$resources)
    }
    $type=$assembly.GetType('VS2017.AIAssistant.Views.AssistantControl',$true)
    $view=[Activator]::CreateInstance($type)
    $flags=[Reflection.BindingFlags]'Instance,NonPublic,Public'
    $scm=$type.GetField('scmLookupStarted',$flags);if($scm){$scm.SetValue($view,$true)}
    $checks=New-Object 'Collections.Generic.List[object]'
    function Check-Theme([string]$name,[bool]$passed,[string]$detail=''){$checks.Add([pscustomobject]@{Name=$name;Passed=$passed;Detail=$detail})}
    function Invoke-Theme([string]$name,[object[]]$values){
        $unwrapped=New-Object 'object[]' $values.Count
        for($i=0;$i -lt $values.Count;$i++){$unwrapped[$i]=$values[$i].PSObject.BaseObject}
        return $type.GetMethod($name,$flags).Invoke($view,$unwrapped)
    }
    function Mode-Theme{return $type.GetProperty('AppliedThemeMode',$flags).GetValue($view,$null)}
    function Render-Theme([string]$mode){
        $null=Invoke-Theme 'RestoreThemeMode' @($mode)
        $view.Measure((New-Object Windows.Size(640,620)));$view.Arrange((New-Object Windows.Rect(0,0,640,620)));$view.UpdateLayout()
        $bitmap=New-Object Windows.Media.Imaging.RenderTargetBitmap(640,620,96,96,[Windows.Media.PixelFormats]::Pbgra32);$bitmap.Render($view)
        $encoder=New-Object Windows.Media.Imaging.PngBitmapEncoder;$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
        $stream=[IO.File]::Create((Join-Path $OutputDirectory ($mode+'.png')));try{$encoder.Save($stream)}finally{$stream.Dispose()}
    }
    $menu=New-Object Windows.Controls.ContextMenu;$menu.Resources.MergedDictionaries.Add($view.Resources);$menu.Style=$view.FindResource('AssistantContextMenu')
    $tooltip=New-Object Windows.Controls.ToolTip;$tooltip.Resources.MergedDictionaries.Add($view.Resources);$tooltip.Style=$view.FindResource([Windows.Controls.ToolTip])
    $popupChild=New-Object Windows.Controls.Border;$popupChild.Resources.MergedDictionaries.Add($view.Resources);$popupChild.SetResourceReference([Windows.Controls.Border]::BackgroundProperty,'AssistantPopupBackgroundBrush')
    Check-Theme 'Saved dark mode initializes selection' ((Mode-Theme) -eq 'dark')
    $null=Invoke-Theme 'RestoreThemeMode' @('light');Check-Theme 'Manual Light is applied' ((Mode-Theme) -eq 'light')
    Check-Theme 'Detached menu follows Light resources' ($menu.Background.Color.R -gt 210 -and $menu.Foreground.Color.R -lt 100)
    Check-Theme 'Detached tooltip follows Light resources' ($tooltip.Background.Color.R -gt 210 -and $tooltip.Foreground.Color.R -lt 100)
    Check-Theme 'Detached popup child follows Light resources' ($popupChild.Background.Color -eq $view.Resources['AssistantPopupBackgroundBrush'].Color)
    $choice=$view.FindName('ThemeModeChoice');Check-Theme 'Light selection matches displayed palette' ($choice.SelectedValue -ceq 'light')
    $before=$view.Resources['AssistantBackgroundBrush'].Color
    $null=Invoke-Theme 'RestoreThemeMode' @('dark');Check-Theme 'Dark changes the displayed background' ((Mode-Theme) -eq 'dark' -and $view.Resources['AssistantBackgroundBrush'].Color -ne $before)
    Check-Theme 'Existing menu switches to Dark with main UI' ($menu.Background.Color.R -lt 100 -and $menu.Foreground.Color.R -gt 170)
    Check-Theme 'Existing tooltip switches to Dark with main UI' ($tooltip.Background.Color.R -lt 100 -and $tooltip.Foreground.Color.R -gt 170)
    Check-Theme 'Existing popup child switches to Dark with main UI' ($popupChild.Background.Color.R -lt 100)
    $choice.SelectedValue='light';Check-Theme 'Setting selection previews immediately' ((Mode-Theme) -eq 'light')
    $app.Resources[[Microsoft.VisualStudio.Shell.VsBrushes]::ToolWindowBackgroundKey]=[Windows.Media.Brushes]::WhiteSmoke
    $app.Resources[[Microsoft.VisualStudio.Shell.VsBrushes]::ToolWindowTextKey]=[Windows.Media.Brushes]::Black
    $null=Invoke-Theme 'RestoreThemeMode' @('auto');Check-Theme 'Auto uses a light host color' ((Mode-Theme) -eq 'light' -and $view.Resources['AssistantBackgroundBrush'].Color -eq [Windows.Media.Colors]::WhiteSmoke)
    $app.Resources[[Microsoft.VisualStudio.Shell.VsBrushes]::ToolWindowBackgroundKey]=[Windows.Media.Brushes]::Black
    $app.Resources[[Microsoft.VisualStudio.Shell.VsBrushes]::ToolWindowTextKey]=[Windows.Media.Brushes]::WhiteSmoke
    $null=Invoke-Theme 'RefreshAssistantTheme' @();Check-Theme 'Auto refresh follows a dark host color' ((Mode-Theme) -eq 'dark' -and $view.Resources['AssistantForegroundBrush'].Color -eq [Windows.Media.Colors]::WhiteSmoke)
    $null=Invoke-Theme 'RestoreThemeMode' @('light');$null=Invoke-Theme 'RefreshAssistantTheme' @();Check-Theme 'Manual Light persists despite dark host' ((Mode-Theme) -eq 'light')
    $null=Invoke-Theme 'UpdateThinkingAnimation' @($true)
    $sweep=$type.GetField('thinkingSweepBrush',$flags).GetValue($view)
    Check-Theme 'Light thinking shimmer uses a dark readable highlight' ($sweep.GradientStops[2].Color.R -lt 90)
    $null=Invoke-Theme 'RestoreThemeMode' @('dark');$sweep=$type.GetField('thinkingSweepBrush',$flags).GetValue($view)
    Check-Theme 'Active shimmer changes with theme' ($sweep.GradientStops[2].Color -eq [Windows.Media.Colors]::White)
    $null=Invoke-Theme 'UpdateThinkingAnimation' @($false)
    Check-Theme 'Stopped shimmer releases its animation brush' ($null -eq $type.GetField('thinkingSweepBrush',$flags).GetValue($view))
    $null=Invoke-Theme 'AssistantThemeLoaded' @($view,(New-Object Windows.RoutedEventArgs))
    Check-Theme 'Load attaches one system theme subscription' ([bool]$type.GetField('themeEventsAttached',$flags).GetValue($view))
    $null=Invoke-Theme 'AssistantThemeLoaded' @($view,(New-Object Windows.RoutedEventArgs))
    $null=Invoke-Theme 'AssistantThemeUnloaded' @($view,(New-Object Windows.RoutedEventArgs))
    Check-Theme 'Unload releases system and SDK subscriptions' (!$type.GetField('themeEventsAttached',$flags).GetValue($view) -and !$type.GetField('ideThemeEventsAttached',$flags).GetValue($view))
    if(!$sourceOnly){
        $preview=$null
        try{
            $null=Invoke-Theme 'RestoreThemeMode' @('dark')
            $preview=[Activator]::CreateInstance($assembly.GetType('VS2017.AIAssistant.Views.MarkdownPreview',$true))
            $preview.Resources.MergedDictionaries.Add($view.Resources)
            $preview.Markdown="[link](https://example.com)`n`n~~~cs`nvar n = 1;`n~~~"
            foreach($mode in @('light','dark')){
                $null=Invoke-Theme 'RestoreThemeMode' @($mode)
                $document=$preview.TextView.Document
                Check-Theme ("Existing Markdown code block follows "+$mode+" palette") ($document.Blocks.LastBlock.Background.Color -eq $view.Resources['AssistantCodeBackgroundBrush'].Color)
                Check-Theme ("Existing Markdown link follows "+$mode+" palette") ($document.Blocks.FirstBlock.Inlines.FirstInline.Foreground.Color -eq $view.Resources['AssistantLinkBrush'].Color)
            }
        }finally{if($preview){$preview.Dispose()}}
        $dialogs=New-Object 'Collections.Generic.List[Windows.Window]'
        try{
            $dialogType=$assembly.GetType('VS2017.AIAssistant.Views.AssistantDialog',$true)
            $dialog=[Activator]::CreateInstance($dialogType,[object[]]@($view,'테마 검토','설정·안내 공통 UI'))
            $dialogs.Add($dialog)
            $policy=[Activator]::CreateInstance($assembly.GetType('VS2017.AIAssistant.Services.WorkspacePermissionPolicy',$true))
            $workspaceType=$assembly.GetType('VS2017.AIAssistant.Views.WorkspaceActionsDialog',$true)
            $workspaceDialog=[Activator]::CreateInstance($workspaceType,[object[]]@($view,$OutputDirectory,$policy,$null))
            $dialogs.Add($workspaceDialog)
            foreach($mode in @('light','dark')){
                $null=Invoke-Theme 'RestoreThemeMode' @($mode)
                foreach($window in $dialogs){
                    Check-Theme ("Existing actual "+$window.GetType().Name+" follows "+$mode) ($window.Background.Color -eq $view.Resources['AssistantBackgroundBrush'].Color -and $window.Foreground.Color -eq $view.Resources['AssistantForegroundBrush'].Color)
                }
            }
        }finally{foreach($window in $dialogs){$window.Close()}}
        $null=Invoke-Theme 'RestoreThemeMode' @('light');$snapshot=Invoke-Theme 'CaptureSettings' @()
        Check-Theme 'Actual settings snapshot includes Light mode' ($snapshot.ThemeMode -ceq 'light')
        $null=Invoke-Theme 'SaveSettingsClicked' @($view,(New-Object Windows.RoutedEventArgs))
        $saved=Get-Content -LiteralPath $env:VS2017_AI_SETTINGS_PATH -Raw|ConvertFrom-Json
        Check-Theme 'Actual explicit save persists Light' ($saved.ThemeMode -ceq 'light')
        $reloaded=$null
        try{
            $reloaded=[Activator]::CreateInstance($type)
            Check-Theme 'A new product control restores the saved Light selection' ($type.GetProperty('AppliedThemeMode',$flags).GetValue($reloaded,$null) -ceq 'light' -and $reloaded.FindName('ThemeModeChoice').SelectedValue -ceq 'light')
        }finally{if($reloaded){$reloaded.Dispose()}}
        $null=Invoke-Theme 'RestoreThemeMode' @('dark');$null=Invoke-Theme 'RestoreThemeMode' @($snapshot.ThemeMode)
        Check-Theme 'Snapshot restore returns the displayed selection' ((Mode-Theme) -eq 'light' -and $choice.SelectedValue -ceq 'light')
    }
    Render-Theme 'light';Render-Theme 'dark'
    $failed=@($checks|Where-Object{!$_.Passed}).Count
    $result=[pscustomobject]@{Succeeded=$failed -eq 0;Passed=$checks.Count-$failed;Failed=$failed;Checks=$checks.ToArray();SourceOnly=$sourceOnly;SdkAnalyzerEnabled=$sourceOnly -and [bool]$AnalyzeSdk;SdkReferenceAssetsPath=$assetsPath;SdkShellAssemblyPath=$references['Microsoft.VisualStudio.Shell.15.0'];ProductAssemblyTested=!$sourceOnly;AssemblySha256=(Get-FileHash -LiteralPath $loadedPath).Hash;ProductAssemblySha256=$productHash;Inputs=$sourceInputs;WindowShown=$false;SyntheticHostColors=$true;ActualIdeTested=$false;WindowsThemeChanged=$false;IdeThemeChanged=$false;CheckedAt=[DateTimeOffset]::Now.ToString('o')}
    $result|ConvertTo-Json -Depth 7|Set-Content -LiteralPath (Join-Path $OutputDirectory 'result.json') -Encoding UTF8
    [pscustomobject]@{Succeeded=$result.Succeeded;Passed=$result.Passed;Failed=$failed;Errors=@($checks|Where-Object{!$_.Passed});SourceOnly=$sourceOnly;OutputDirectory=$OutputDirectory}|ConvertTo-Json -Depth 5
    if($failed){throw 'Theme runtime assertions failed; original results preserved.'}
}finally{
    if($view){$view.Dispose()};if($app){$app.Shutdown()}
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver)
    $env:VS2017_AI_SETTINGS_PATH=$oldSettings;$env:VS2017_AI_WORKSPACE_PATH=$oldWorkspace;$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY=$oldDiscovery
}
