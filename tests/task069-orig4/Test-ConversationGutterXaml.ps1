[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$xamlPath = Join-Path $repo 'src\VS2017.AIAssistant\Views\AssistantControl.xaml'
[xml]$document = Get-Content -LiteralPath $xamlPath -Raw -Encoding UTF8
$namespaces = New-Object Xml.XmlNamespaceManager($document.NameTable)
$namespaces.AddNamespace('x', 'http://schemas.microsoft.com/winfx/2006/xaml')

function Assert-Condition([string]$name, [bool]$condition, $details) {
    if (!$condition) { throw "FAIL: $name; $details" }
    Write-Output "PASS: $name; $details"
}

$scroll = $document.SelectSingleNode("//*[@x:Name='ConversationScroll']", $namespaces)
Assert-Condition 'conversation scroller remains vertical-only' ($scroll.GetAttribute('HorizontalScrollBarVisibility') -eq 'Disabled' -and $scroll.GetAttribute('VerticalScrollBarVisibility') -eq 'Auto') 'existing scroll contract retained'
$surface = $document.SelectSingleNode("//*[@x:Name='ConversationSurface']", $namespaces)
$header = $document.SelectSingleNode("//*[@x:Name='ConversationHeader']", $namespaces)
Assert-Condition 'fixed header remains outside the scrolling body' ($header.ParentNode -eq $surface -and $header.GetAttribute('Grid.Row') -eq '0' -and $scroll.GetAttribute('Grid.Row') -eq '1') 'header row 0; scroller row 1'

$gutterGrid = $scroll.SelectSingleNode("*[local-name()='Grid']")
$columns = $gutterGrid.SelectNodes("*[local-name()='Grid.ColumnDefinitions']/*[local-name()='ColumnDefinition']")
$body = $gutterGrid.SelectSingleNode("*[local-name()='StackPanel' and @Grid.Column='1']")
Assert-Condition 'body is centered between two responsive gutters' ($columns.Count -eq 3 -and $null -ne $body) 'three columns; message body in center'
foreach ($index in @(0, 2)) {
    $column = $columns[$index]
    Assert-Condition "gutter $index has responsive 12–20 px bounds" ($column.GetAttribute('Width') -eq '0.04*' -and $column.GetAttribute('MinWidth') -eq '12' -and $column.GetAttribute('MaxWidth') -eq '20') $column.OuterXml
}
Assert-Condition 'center column remains flexible' ($columns[1].GetAttribute('Width') -eq '*') $columns[1].OuterXml

$composer = $document.SelectSingleNode("//*[@x:Name='ComposerSurface']", $namespaces)
Assert-Condition 'composer alignment remains unchanged' ($composer.GetAttribute('Grid.Row') -eq '5' -and $composer.GetAttribute('Grid.Column') -eq '2') 'existing input row and column'
$maxWidths = @($document.SelectNodes("//*[@TargetName='MessageBubble' and @Property='MaxWidth']") | ForEach-Object { $_.GetAttribute('Value') })
Assert-Condition 'message bubble maximum widths remain unchanged' (($maxWidths -join ',') -eq '680,760') ($maxWidths -join ',')
$horizontalCodeScroll = $document.SelectNodes("//*[local-name()='TextBox' and @HorizontalScrollBarVisibility='Auto']")
Assert-Condition 'existing code/diff horizontal scrolling remains available' ($horizontalCodeScroll.Count -ge 3) "horizontal-scroll text views: $($horizontalCodeScroll.Count)"

$converter = New-Object Windows.GridLengthConverter
$layoutGrid = New-Object Windows.Controls.Grid
foreach ($column in $columns) {
    $definition = New-Object Windows.Controls.ColumnDefinition
    $definition.Width = $converter.ConvertFromString($column.GetAttribute('Width'))
    if ($column.HasAttribute('MinWidth')) { $definition.MinWidth = [double]$column.GetAttribute('MinWidth') }
    if ($column.HasAttribute('MaxWidth')) { $definition.MaxWidth = [double]$column.GetAttribute('MaxWidth') }
    $layoutGrid.ColumnDefinitions.Add($definition)
}
foreach ($width in @(180, 343, 360, 520, 640)) {
    $layoutGrid.Measure([Windows.Size]::new($width, 120))
    $layoutGrid.Arrange([Windows.Rect]::new(0, 0, $width, 120))
    $actual = @($layoutGrid.ColumnDefinitions | ForEach-Object { [Math]::Round($_.ActualWidth, 2) })
    $pass = $actual[0] -ge 12 -and $actual[0] -le 20 -and $actual[2] -ge 12 -and $actual[2] -le 20 -and $actual[1] -gt 0 -and [Math]::Abs(($actual | Measure-Object -Sum).Sum - $width) -lt 0.1
    Assert-Condition "WPF layout gutter bounds at $width px" $pass "left=$($actual[0]); body=$($actual[1]); right=$($actual[2])"
}

Write-Output 'Conversation gutter XAML checks: PASS'
