#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Inspect-EditorSdk
desc         : 설치 SDK 공개 API identity·서명·상수만 ReflectionOnly로 조사한다.
author       : CodexOrig 보조 개발 055
create date  : 2026-10-23
update date  : 2026-10-23 CodexOrig 보조 개발 055 : 검사 대상 메소드 호출·IDE 실행 없이 버전별 계약을 보존한다.
-------------------------------------------------------------------------
#>
param([string]$Installation,[string]$OutputPath)
$ErrorActionPreference='Stop'
$ide=Join-Path $Installation 'Common7/IDE'
$dirs=@((Join-Path $ide 'CommonExtensions/Microsoft/Editor'),(Join-Path $ide 'PublicAssemblies'),(Join-Path $ide 'PrivateAssemblies'),(Join-Path $ide 'CommonExtensions/Platform/MefHosting'),(Join-Path $Installation 'VSSDK/VisualStudioIntegration/Common/Assemblies/v4.0'))
$handler=[ResolveEventHandler]{param($sender,$eventArgs)
 $file=([Reflection.AssemblyName]$eventArgs.Name).Name+'.dll'
 foreach($dir in $dirs){$path=Join-Path $dir $file;if(Test-Path -LiteralPath $path){return [Reflection.Assembly]::ReflectionOnlyLoadFrom($path)}}
 try{return [Reflection.Assembly]::ReflectionOnlyLoad($eventArgs.Name)}catch{return $null}
}
[AppDomain]::CurrentDomain.add_ReflectionOnlyAssemblyResolve($handler)
$names=@('IDifferenceBufferFactoryService','IDifferenceBuffer','ISnapshotDifference','IWpfDifferenceViewerFactoryService','IWpfDifferenceViewer','IClassifierAggregatorService','IViewClassifierAggregatorService','IClassifier','IClassificationFormatMapService','IViewTagAggregatorFactoryService','ITagAggregator`1','IErrorTag','IOverviewMarkTag','ITextMarkerTag','ITextBufferFactoryService','IContentTypeRegistryService','DifferenceViewerOptions','DifferenceBufferOptions','MarkerFormatDefinition','IHierarchicalDifferenceCollection','IDifferenceTrackingSpanCollection')
$rows=@()
foreach($file in @('Microsoft.VisualStudio.Text.Data.dll','Microsoft.VisualStudio.Text.Logic.dll','Microsoft.VisualStudio.Text.UI.dll','Microsoft.VisualStudio.Text.UI.Wpf.dll','Microsoft.VisualStudio.CoreUtility.dll')){
 $path=$dirs|ForEach-Object{Join-Path $_ $file}|Where-Object{Test-Path -LiteralPath $_}|Select-Object -First 1
 $assembly=[Reflection.Assembly]::ReflectionOnlyLoadFrom($path)
 foreach($name in $names){
  $type=$null
  foreach($namespace in @('Microsoft.VisualStudio.Text.Differencing','Microsoft.VisualStudio.Text.Classification','Microsoft.VisualStudio.Text.Tagging','Microsoft.VisualStudio.Text','Microsoft.VisualStudio.Utilities')){
   $type=$assembly.GetType(($namespace+'.'+$name),$false);if($type){break}
  }
  if(!$type){continue}
  $rows+=[pscustomobject]@{Type=$type.FullName;Assembly=$assembly.GetName().FullName;SHA256=(Get-FileHash $path).Hash;Methods=@($type.GetMethods()|ForEach-Object{$_.ToString()});Properties=@($type.GetProperties()|ForEach-Object{$_.ToString()});Constants=@($type.GetFields([Reflection.BindingFlags]'Public,Static')|Where-Object IsLiteral|ForEach-Object{[pscustomobject]@{Name=$_.Name;Value=$_.GetRawConstantValue()}})}
 }
}
[pscustomobject]@{ReflectionOnly=$true;InspectedMethodsInvoked=0;GuiUsed=$false;Rows=$rows}|ConvertTo-Json -Depth 7|Set-Content -LiteralPath $OutputPath -Encoding UTF8
