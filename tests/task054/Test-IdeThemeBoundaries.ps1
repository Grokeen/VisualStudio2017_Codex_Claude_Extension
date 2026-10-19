#requires -Version 5.1
<#
name         : Test-IdeThemeBoundaries
desc         : 고정 제품의 최소 테마 XML 계층과 제한된 UTF8 입력을 화면·IDE 변경 없이 검사한다.
author       : CodexOrig
create date  : 2026-10-20
update date  : 2026-10-20 CodexOrig : TASK054 실제 IDE에서 찾은 그룹 누락과 입력 크기 경계 회귀.
#>
param([Parameter(Mandatory=$true)][string]$AssemblyPath,[Parameter(Mandatory=$true)][string]$AssetsPath,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$out=[IO.Path]::GetFullPath($OutputDirectory)
if((Test-Path -LiteralPath $out) -and @(Get-ChildItem -LiteralPath $out -Force).Count){throw 'Preserve previous evidence.'}
$null=New-Item -ItemType Directory -Path $out -Force
foreach($name in @('WindowsBase','PresentationCore','PresentationFramework','System.Xaml')){Add-Type -AssemblyName $name}
$assets=Get-Content -LiteralPath $AssetsPath -Raw -Encoding UTF8|ConvertFrom-Json
$packageRoot=$assets.packageFolders.PSObject.Properties.Name|Select-Object -First 1
$target=$assets.targets.PSObject.Properties|Where-Object {$_.Name -like '.NETFramework,Version=v*' -and $_.Name -notlike '*/win*'}|Select-Object -First 1
$dependencies=@{}
foreach($entry in $target.Value.PSObject.Properties){if($entry.Value.PSObject.Properties.Name -contains 'compile'){foreach($part in $entry.Value.compile.PSObject.Properties.Name){if($part -like '*.dll'){$dependencies[[IO.Path]::GetFileNameWithoutExtension($part)]=Join-Path $packageRoot ($assets.libraries.($entry.Name).path+'/'+$part)}}}}
$resolver=[ResolveEventHandler]{param($sender,$eventArgs) $name=([Reflection.AssemblyName]::new($eventArgs.Name)).Name; if($dependencies.ContainsKey($name)){return [Reflection.Assembly]::LoadFrom($dependencies[$name])}; return $null}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
$passed=[Collections.Generic.List[string]]::new();$failed=[Collections.Generic.List[string]]::new()
function Check([string]$name,[bool]$condition){if($condition){$passed.Add($name)}else{$failed.Add($name)}}
try{
    $assembly=[Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $AssemblyPath).Path)
    $type=$assembly.GetType('VS2017.AIAssistant.Views.AssistantControl',$true)
    $flags=[Reflection.BindingFlags]'Static,NonPublic'
    $xmlMethod=$type.GetMethod('ThemeSettingsXml',$flags)
    $readMethod=$type.GetMethod('ReadBoundedThemeFile',$flags)
    foreach($version in @('15.0','16.0','17.0')){foreach($theme in @('{1D41C4A3-CF75-4F0A-B2FF-9A8C2D431B92}','{8EC84919-8A34-422A-ABE1-CC238AD4D4B1}')){
        [xml]$xml=$xmlMethod.Invoke($null,[object[]]@([string]$version,[string]$theme))
        $selected=$xml.SelectSingleNode('/UserSettings/Category[@name="Environment_Group"][@RegisteredName="Environment_Group"]/Category[@name="Environment_FontsAndColors"]/FontsAndColors/Theme/@Id')
        Check ('IDE group and exact theme '+$version+' '+$theme) ($selected -and $selected.Value -eq $theme -and $xml.UserSettings.ApplicationIdentity.version -eq $version)
        Check 'No unrelated option categories imported' ($xml.SelectNodes('//Category').Count -eq 2 -and $xml.SelectNodes('//PropertyValue').Count -eq 1 -and $xml.SelectNodes('//ToolsOptions').Count -eq 0)
    }}
    foreach($arguments in @(@('18.0','{1D41C4A3-CF75-4F0A-B2FF-9A8C2D431B92}'),@('15.0','{00000000-0000-0000-0000-000000000000}'))){
        $rejected=$false;try{$null=$xmlMethod.Invoke($null,[object[]]@([string]$arguments[0],[string]$arguments[1]))}catch{$rejected=$_.Exception.InnerException -is [ArgumentException]}
        Check 'Unsupported version or theme rejected' $rejected
    }
    $path=Join-Path $out 'bounded-utf8.txt'
    foreach($count in @(0,1,63,64,4095,4096,8193)){
        $sample=('한'*$count);[IO.File]::WriteAllText($path,$sample,[Text.UTF8Encoding]::new($true))
        $read=$readMethod.Invoke($null,[object[]]@([string]$path,[int]$count))
        Check ('UTF8 exact bound round trip '+$count) ($read -ceq $sample)
        [IO.File]::WriteAllText($path,($sample+'끝'),[Text.UTF8Encoding]::new($false))
        $rejected=$false;try{$null=$readMethod.Invoke($null,[object[]]@([string]$path,[int]$count))}catch{$rejected=$_.Exception.InnerException -is [IO.InvalidDataException]}
        Check ('One character past bound rejected '+$count) $rejected
    }
}
catch{$failed.Add($_.Exception.ToString())}
finally{[AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver)}
$result=[ordered]@{Succeeded=$failed.Count -eq 0;Passed=$passed.Count;Failed=$failed.Count;Checks=$passed;Errors=$failed;AssemblySha256=(Get-FileHash -LiteralPath $AssemblyPath).Hash;WindowShown=$false;IdeSettingsChanged=$false;CheckedAt=[DateTimeOffset]::Now.ToString('o')}
$result|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $out 'result.json') -Encoding UTF8
$result|ConvertTo-Json -Depth 5
if($failed.Count){exit 1}
