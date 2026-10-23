#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Inspect-DifferenceImplementation
desc         : 설치 SDK의 비교 overview 소비 경로를 ReflectionOnly 메타데이터와 IL로 읽는다.
author       : CodexOrig 보조 개발 055
create date  : 2026-10-23
update date  : 2026-10-23 CodexOrig 보조 개발 055 : 검사 대상 메소드를 호출하지 않고 문자열과 필드·호출 참조만 보존한다.
-------------------------------------------------------------------------
#>
param([Parameter(Mandatory=$true)][string]$Installation,[Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference='Stop'
$ide=Join-Path $Installation 'Common7/IDE'
$dirs=@((Join-Path $ide 'CommonExtensions/Microsoft/Editor'),(Join-Path $ide 'PublicAssemblies'),(Join-Path $ide 'PrivateAssemblies'),(Join-Path $ide 'CommonExtensions/Platform/MefHosting'),(Join-Path $Installation 'VSSDK/VisualStudioIntegration/Common/Assemblies/v4.0'))
$handler=[ResolveEventHandler]{param($sender,$eventArgs)
    $file=([Reflection.AssemblyName]$eventArgs.Name).Name+'.dll'
    foreach($dir in $dirs){$path=Join-Path $dir $file;if(Test-Path -LiteralPath $path){return [Reflection.Assembly]::ReflectionOnlyLoadFrom($path)}}
    try{return [Reflection.Assembly]::ReflectionOnlyLoad($eventArgs.Name)}catch{return $null}
}
[AppDomain]::CurrentDomain.add_ReflectionOnlyAssemblyResolve($handler)
$opcodes=@{}
foreach($field in [Reflection.Emit.OpCodes].GetFields([Reflection.BindingFlags]'Public,Static')){$opcode=$field.GetValue($null);$opcodes[([int]$opcode.Value -band 65535)]=$opcode}
$results=@();$assemblies=@();$loadErrors=@()
foreach($file in @('Microsoft.VisualStudio.Platform.VSEditor.dll','Microsoft.VisualStudio.Editor.Implementation.dll')){
    $path=Join-Path $dirs[0] $file
    $assembly=[Reflection.Assembly]::ReflectionOnlyLoadFrom($path)
    $assemblies+=[pscustomobject]@{Path=$path;Identity=$assembly.FullName;SHA256=(Get-FileHash $path).Hash}
    try{$types=$assembly.GetTypes()}catch [Reflection.ReflectionTypeLoadException]{$types=$_.Exception.Types|Where-Object{$_};$loadErrors+=@($_.Exception.LoaderExceptions|ForEach-Object Message|Select-Object -Unique)}
    foreach($type in $types|Where-Object{$_.FullName -match 'Differenc|DiffOverview'}){
        $methods=@($type.GetMethods([Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly'))+@($type.GetConstructors([Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly'))
        foreach($method in $methods){
            try{$body=$method.GetMethodBody();if(!$body){continue};$bytes=$body.GetILAsByteArray()}catch{continue}
            $rows=@();$position=0
            while($position -lt $bytes.Length){
                $offset=$position;$code=[int]$bytes[$position];$position++
                if($code -eq 254){$code=65024+[int]$bytes[$position];$position++}
                $op=$opcodes[$code];if(!$op){throw ("Unknown IL opcode code="+$code+" type="+$type.FullName+" method="+$method.Name+" offset="+$offset)}
                $length=0;$value=$null
                switch($op.OperandType.ToString()){
                    'InlineNone'{}
                    {$_ -in 'ShortInlineBrTarget','ShortInlineI','ShortInlineVar'}{$length=1}
                    'InlineVar'{$length=2}
                    {$_ -in 'InlineBrTarget','InlineI','ShortInlineR'}{$length=4}
                    {$_ -in 'InlineI8','InlineR'}{$length=8}
                    'InlineSwitch'{$length=4+4*[BitConverter]::ToInt32($bytes,$position)}
                    {$_ -in 'InlineString','InlineField','InlineMethod','InlineType','InlineTok','InlineSig'}{
                        $length=4;$token=[BitConverter]::ToInt32($bytes,$position)
                        try{if($_ -eq 'InlineString'){$value=$method.Module.ResolveString($token)}elseif($_ -ne 'InlineSig'){$value=$method.Module.ResolveMember($token).ToString()}}catch{$value='unresolved:'+$_.Exception.Message}
                    }
                    default{throw ('Unhandled IL operand '+$op.OperandType)}
                }
                if($null -ne $value){$rows+=[pscustomobject]@{Offset=$offset;Opcode=$op.Name;Value=$value}}
                $position+=$length
            }
            if($rows.Count -gt 0){$results+=[pscustomobject]@{Type=$type.FullName;Method=$method.Name;Attributes=@($type.GetCustomAttributesData()|ForEach-Object ToString);IL=$rows}}
        }
    }
}
[pscustomobject]@{ReflectionOnly=$true;InspectedMethodsInvoked=0;GuiUsed=$false;Assemblies=$assemblies;TypeLoadErrors=$loadErrors;Methods=$results}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $OutputPath -Encoding UTF8
