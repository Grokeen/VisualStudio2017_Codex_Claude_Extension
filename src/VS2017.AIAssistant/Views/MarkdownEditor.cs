// 사용자 제공 RemoteControlDemo/Features/Markdown/MarkdownnPane.cs의 작성 이벤트·도구 배치를 이식했다.
// 원본 프로젝트는 변경하지 않고 작성·문서 관리·native 미리보기 기능을 공통화했다.
using System;
using System.IO;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using Microsoft.Win32;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : MarkdownEditor
    /// desc         : 공통 Markdown 작성·미리보기 탭과 선택 영역 서식 도구를 제공한다.
    /// author       : CodexCode
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexCode : TASK-024 저장·AI 호출 없이 원문 양방향 바인딩만 제공.
    ///                2026-10-19 CodexCode : Role 추가에 따라 AvalonEdit 작성·선택적 파일 내보내기 기능 확대.
    ///                2026-10-19 CodexOrig : R3-03 문서 목록·현재 저장·이름 변경·삭제·위치 열기와 연결.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed partial class MarkdownEditor : UserControl, IDisposable
    {
        private const int CharacterLimit=128*1024;
        public static readonly DependencyProperty TextProperty=DependencyProperty.Register("Text",typeof(string),typeof(MarkdownEditor),new FrameworkPropertyMetadata("",FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,TextUpdated),ValidText);
        public static readonly DependencyProperty MaxLengthProperty=DependencyProperty.Register("MaxLength",typeof(int),typeof(MarkdownEditor),new PropertyMetadata(8000,MaxLengthUpdated),ValidLength);
        public static readonly DependencyProperty AssetBaseDirectoryProperty=DependencyProperty.Register("AssetBaseDirectory",typeof(string),typeof(MarkdownEditor),new PropertyMetadata(null,AssetsUpdated));
        public string Text { get{return (string)GetValue(TextProperty);} set{SetValue(TextProperty,value);} }
        public int MaxLength { get{return (int)GetValue(MaxLengthProperty);} set{SetValue(MaxLengthProperty,value);} }
        public string AssetBaseDirectory { get{return (string)GetValue(AssetBaseDirectoryProperty);} set{SetValue(AssetBaseDirectoryProperty,value);} }
        public TextEditor Editor { get; private set; }
        public MarkdownPreview Preview { get; private set; }
        public TabControl Tabs { get; private set; }
        public event EventHandler TextChanged;
        private readonly DispatcherTimer previewTimer;
        private readonly Grid body;
        private readonly GridSplitter splitter;
        private readonly TextBlock status,cursorLabel;
        private readonly TextBox searchText;
        private readonly CheckBox searchCase,searchWords,searchRegex;
        private readonly FrameworkElement searchRow,discardRow;
        private MarkdownnFenceColorizer fenceColorizer;
        private MarkdownnSyntaxColorizer syntaxColorizer;
        private DependencyPropertyDescriptor paletteDescriptor;
        private string savedText="";
        private double zoomPercent=100;
        private Action pendingTransition;
        public bool IsDirty { get{return !String.Equals(Text??"",savedText,StringComparison.Ordinal);} }
        public string CurrentPath { get; private set; }
        public string SearchPattern { get{return searchText.Text;} set{if(value!=null&&value.Length>512)throw new ArgumentOutOfRangeException("value");searchText.Text=value??"";} }
        public bool SearchMatchCase { get{return searchCase.IsChecked==true;} set{searchCase.IsChecked=value;} }
        public bool SearchWholeWords { get{return searchWords.IsChecked==true;} set{searchWords.IsChecked=value;} }
        public bool SearchUseRegex { get{return searchRegex.IsChecked==true;} set{searchRegex.IsChecked=value;} }
        public int ViewMode { get{return Tabs.SelectedIndex;} set{if(value<0||value>2)throw new ArgumentOutOfRangeException("value");Tabs.SelectedIndex=value;} }
        public double ZoomPercent { get{return zoomPercent;} set{zoomPercent=Double.IsNaN(value)||Double.IsInfinity(value)?100:Math.Max(60,Math.Min(200,value));ApplyZoom();} }
        private bool synchronizing,disposed;
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RequestTransition
        /// desc         : dirty 문서는 버리기 버튼으로 승인되기 전까지 전환하지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 사용자 승인·크기·UTF-8·로컬 경로 검증 보강.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RequestTransition(Action action){if(!IsDirty){RunFileAction(action);return;}pendingTransition=action;discardRow.Visibility=Visibility.Visible;}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ConfirmTransition
        /// desc         : 실제로 버리기를 누른 경우에만 대기 중인 문서 전환을 실행한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 사용자 승인·크기·UTF-8·로컬 경로 검증 보강.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ConfirmTransition(){var action=pendingTransition;CancelTransition();if(action!=null)RunFileAction(action);}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CancelTransition
        /// desc         : 전환 취소 시 기존 원문·파일 경로를 보존한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 사용자 승인·크기·UTF-8·로컬 경로 검증 보강.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void CancelTransition(){pendingTransition=null;discardRow.Visibility=Visibility.Collapsed;}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ConfirmClose
        /// desc         : dirty 문서는 호스트 닫기를 보류하고 실제 버리기 승인 후에만 다시 닫는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : Window.Closing은 false면 취소하고 callback에서 재시도 가능.
        /// </summary>
        /// ---------------------------------------------------------------------
        public bool ConfirmClose(Action closeAfterDiscard){if(closeAfterDiscard==null)throw new ArgumentNullException("closeAfterDiscard");if(!IsDirty)return true;RequestTransition(()=>{MarkSaved();closeAfterDiscard();});return false;}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : TryNewDocument
        /// desc         : 호출자의 명시적 승인 뒤에만 새 문서로 전환한다.
        ///                새 문서에는 기존 파일의 신뢰 이미지 폴더를 승계하지 않는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 사용자 승인·크기·UTF-8·로컬 경로 검증 보강.
        /// </summary>
        /// ---------------------------------------------------------------------
        public bool TryNewDocument(Func<bool> confirmDiscard){if(disposed)return false;if(IsDirty&&(confirmDiscard==null||!confirmDiscard()))return false;SetCurrentValue(TextProperty,"");CurrentPath=null;ResetDocumentConfirmation();SetCurrentValue(AssetBaseDirectoryProperty,null);MarkSaved();Editor.Document.UndoStack.ClearAll();RefreshPreview();RefreshDocumentList(CurrentPath==null?documentDirectory:Path.GetDirectoryName(CurrentPath));return true;}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : TryLoadFile
        /// desc         : 승인과 모든 읽기 검증을 마친 UTF-8 원문만 현재 문서에 반영한다.
        ///                성공한 문서의 동일 폴더만 PNG/GIF 미리보기 기준으로 사용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 사용자 승인·크기·UTF-8·로컬 경로 검증 보강.
        /// </summary>
        /// ---------------------------------------------------------------------
        public bool TryLoadFile(string path,Func<bool> confirmDiscard){if(disposed)return false;if(IsDirty&&(confirmDiscard==null||!confirmDiscard()))return false;string full=ValidateFilePath(path);byte[] bytes=MarkdownAssetReader.Read(Path.GetDirectoryName(full),Path.GetFileName(full),512*1024,true,false);int skip=bytes.Length>=3&&bytes[0]==239&&bytes[1]==187&&bytes[2]==191?3:0;string value=new UTF8Encoding(false,true).GetString(bytes,skip,bytes.Length-skip);if(value.Length>CharacterLimit)throw new InvalidDataException("Markdown 원문은 128Ki 문자까지 지원합니다.");SetCurrentValue(TextProperty,value);CurrentPath=full;ResetDocumentConfirmation();SetCurrentValue(AssetBaseDirectoryProperty,Path.GetDirectoryName(full));MarkSaved();Editor.Document.UndoStack.ClearAll();RefreshPreview();RefreshDocumentList(CurrentPath==null?documentDirectory:Path.GetDirectoryName(CurrentPath));return true;}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ExportUtf8
        /// desc         : 같은 폴더의 고유 임시 파일을 원자적으로 교체한 뒤 저장 기준을 갱신한다.
        ///                SaveAs 성공 후에는 실제 저장 폴더가 새 상대 이미지 기준이 된다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 사용자 승인·크기·UTF-8·로컬 경로 검증 보강.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void ExportUtf8(string path){if(disposed)throw new ObjectDisposedException("MarkdownEditor");string full=ValidateFilePath(path);byte[] bytes=new UTF8Encoding(false,true).GetBytes(Text??"");if(bytes.Length>512*1024)throw new InvalidDataException("Markdown 저장 한도를 넘었습니다.");string temporary=Path.Combine(Path.GetDirectoryName(full),".markdown-"+Guid.NewGuid().ToString("N")+".tmp");try{using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(bytes,0,bytes.Length);output.Flush(true);}ValidateFilePath(full);if(File.Exists(full))File.Replace(temporary,full,null);else File.Move(temporary,full);CurrentPath=full;ResetDocumentConfirmation();SetCurrentValue(AssetBaseDirectoryProperty,Path.GetDirectoryName(full));MarkSaved();RefreshPreview();RefreshDocumentList(Path.GetDirectoryName(full));}finally{if(File.Exists(temporary))File.Delete(temporary);}}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ValidateFilePath
        /// desc         : 사용자가 지정한 로컬 Markdown 파일과 기존 조상의 reparse 경계를 검사한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 사용자 승인·크기·UTF-8·로컬 경로 검증 보강.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static string ValidateFilePath(string path){if(String.IsNullOrWhiteSpace(path)||path.Length>2048||path.Any(Char.IsControl)||path.Length<3||!Char.IsLetter(path[0])||path[1]!=':'||path[2]!='\\'&&path[2]!='/')throw new InvalidDataException("로컬 Markdown 파일을 선택해 주세요.");string full=Path.GetFullPath(path);if(full.IndexOf(':',2)>=0)throw new InvalidDataException("대체 스트림은 사용할 수 없습니다.");string extension=Path.GetExtension(full);if(!extension.Equals(".md",StringComparison.OrdinalIgnoreCase)&&!extension.Equals(".markdown",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("확장자가 .md 또는 .markdown인 파일을 선택해 주세요.");for(string current=full;!String.IsNullOrEmpty(current);current=Path.GetDirectoryName(current)){if((File.Exists(current)||Directory.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("연결 파일·폴더는 사용할 수 없습니다.");}return full;}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : MarkSaved
        /// desc         : 호출자가 저장·적용한 현재 원문을 dirty 비교 기준으로 보관한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 사용자 승인·크기·UTF-8·로컬 경로 검증 보강.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void MarkSaved(){savedText=Text??"";if(status!=null)status.Text="Markdown · UTF-8";}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ChooseLoad
        /// desc         : 실제 사용자 파일 선택 이후에도 dirty 확인을 거쳐 문서를 전환한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 사용자 승인·크기·UTF-8·로컬 경로 검증 보강.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ChooseLoad(){var dialog=new OpenFileDialog{Filter="Markdown (*.md;*.markdown)|*.md;*.markdown",CheckFileExists=true,Multiselect=false};if(dialog.ShowDialog(Window.GetWindow(this))==true){string path=dialog.FileName;RequestTransition(()=>TryLoadFile(path,()=>true));}}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ChooseExport
        /// desc         : 사용자가 내보내기를 누를 때만 파일 선택 창을 연다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 사용자 승인·크기·UTF-8·로컬 경로 검증 보강.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ChooseExport(){var dialog=new SaveFileDialog{Filter="Markdown (*.md)|*.md",DefaultExt=".md",AddExtension=true,FileName=CurrentPath==null?"Markdown.md":Path.GetFileName(CurrentPath),OverwritePrompt=true};if(dialog.ShowDialog(Window.GetWindow(this))==true)RunFileAction(()=>ExportUtf8(dialog.FileName));}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RunFileAction
        /// desc         : 파일 오류는 원문·전체 경로가 포함되지 않은 상태 안내로 바꾼다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : TASK-024 사용자 승인·크기·UTF-8·로컬 경로 검증 보강.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RunFileAction(Action action){try{action();}catch(Exception error)when(error is IOException||error is InvalidDataException||error is UnauthorizedAccessException||error is System.Security.SecurityException||error is ArgumentException||error is NotSupportedException){status.Text="파일 작업을 완료하지 못했습니다. UTF-8 형식·크기·경로와 사용 권한을 확인해 주세요.";}}


        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : MarkdownEditor
        /// desc         : 접근 가능한 WPF 기본 탭·버튼·텍스트 상자를 구성한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 도구 모음은 실제 원문에만 서식을 추가하며 HTML은 실행하지 않음.
        ///                2026-10-19 CodexOrig : 문서 목록을 접이식으로 연결하고 현재 저장·다른 이름 저장을 구분.
        /// </summary>
        /// ---------------------------------------------------------------------
        public MarkdownEditor()
        {
            Dispatcher.VerifyAccess();
            var layout=new Grid();layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});layout.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});layout.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            var tools=new WrapPanel{Margin=new Thickness(0,0,0,8)};
            var fileTools=AddToolGroup(tools,"문서","▤");
            AddTool(fileTools,"＋ 새","변경 확인 후 새 문서",(s,e)=>RequestTransition(()=>TryNewDocument(()=>true)));
            AddTool(fileTools,"열기","UTF-8 Markdown 파일 열기",(s,e)=>ChooseLoad());AddTool(fileTools,"저장","현재 문서 저장 · Ctrl+S",(s,e)=>{Dispatcher.VerifyAccess();ChooseSave();});AddTool(fileTools,"내보내기","UTF-8 다른 이름으로 저장 · Ctrl+Shift+S",(s,e)=>ChooseExport());
            var editTools=AddToolGroup(tools,"편집","↶");
            AddTool(editTools,"↶ 실행 취소","실행 취소 · Ctrl+Z",(s,e)=>{if(Editor.CanUndo)Editor.Undo();});AddTool(editTools,"↷ 다시 실행","다시 실행 · Ctrl+Y",(s,e)=>{if(Editor.CanRedo)Editor.Redo();});
            var formatTools=AddToolGroup(tools,"서식","B");
            AddTool(formatTools,"B 굵게","선택한 글을 굵게 표시",BoldClicked);AddTool(formatTools,"I 기울임","기울임 · Ctrl+I",(s,e)=>WrapSelection("*","*","기울임"));
            var insertTools=AddToolGroup(tools,"삽입","＋");
            AddTool(insertTools,"• 목록","선택한 줄을 목록으로 작성",ListClicked);AddTool(insertTools,"↗ 링크","선택한 글에 링크 작성",LinkClicked);AddTool(insertTools,"</> 코드","선택한 코드를 fenced block으로 작성",CodeClicked);
            var taskTools=AddToolGroup(tools,"체크 목록","☑");
            AddTool(taskTools,"☐ 항목 추가","체크박스 만들기 · Ctrl+Shift+C",(s,e)=>EditTaskLines(false));AddTool(taskTools,"☑ 완료 전환","완료 전환 · Ctrl+Enter",(s,e)=>EditTaskLines(true));
            var viewTools=AddToolGroup(tools,"보기","⌕");
            AddTool(viewTools,"⌕ 찾기","문자열 검색 · Ctrl+F",(s,e)=>OpenSearch());AddTool(viewTools,"↵ 줄바꿈","자동 줄바꿈 · Alt+Z",(s,e)=>Editor.WordWrap=!Editor.WordWrap);
            layout.Children.Add(tools);
            var search=new DockPanel{Visibility=Visibility.Collapsed,Margin=new Thickness(0,2,0,6)};var searchTools=new WrapPanel();DockPanel.SetDock(searchTools,Dock.Right);AddTool(searchTools,"↑","이전 찾기",(s,e)=>FindNext(true));AddTool(searchTools,"↓","다음 찾기",(s,e)=>FindNext(false));AddTool(searchTools,"×","찾기 닫기",(s,e)=>{searchRow.Visibility=Visibility.Collapsed;Editor.Focus();});search.Children.Add(searchTools);
            var options=new WrapPanel();DockPanel.SetDock(options,Dock.Bottom);searchCase=new CheckBox{Content="대/소문자",Margin=new Thickness(4,4,10,4)};searchWords=new CheckBox{Content="단어 단위",Margin=new Thickness(4,4,10,4)};searchRegex=new CheckBox{Content="정규식",Margin=new Thickness(4,4,10,4),ToolTip="패턴512자·검색100ms 제한"};searchCase.SetResourceReference(FrameworkElement.StyleProperty,typeof(CheckBox));searchWords.SetResourceReference(FrameworkElement.StyleProperty,typeof(CheckBox));searchRegex.SetResourceReference(FrameworkElement.StyleProperty,typeof(CheckBox));options.Children.Add(searchCase);options.Children.Add(searchWords);options.Children.Add(searchRegex);search.Children.Add(options);
            searchText=new TextBox{MaxLength=512,MinWidth=45,Padding=new Thickness(4),ToolTip="문자열 검색 · 정규식은 명시 선택 시 100ms 제한"};searchText.KeyDown+=SearchKeyDown;search.Children.Add(searchText);searchRow=search;Grid.SetRow(search,1);layout.Children.Add(search);
            Editor=new TextEditor{ShowLineNumbers=true,WordWrap=false,AllowDrop=false,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,MinHeight=140,Padding=new Thickness(9),FontFamily=new FontFamily("Consolas"),FontSize=13};
            Editor.Options.ConvertTabsToSpaces=true;Editor.Options.IndentationSize=4;Editor.Options.HighlightCurrentLine=true;Editor.Document.UndoStack.SizeLimit=100;
            Editor.SetResourceReference(ForegroundProperty,"AssistantForegroundBrush");Editor.SetResourceReference(BackgroundProperty,"AssistantBackgroundBrush");Editor.SetResourceReference(BorderBrushProperty,"AssistantBorderBrush");Editor.SetResourceReference(TextEditor.LineNumbersForegroundProperty,"AssistantMutedForegroundBrush");Editor.TextArea.SetResourceReference(ICSharpCode.AvalonEdit.Editing.TextArea.SelectionBrushProperty,"AssistantSelectionBrush");Editor.TextArea.TextView.SetResourceReference(ICSharpCode.AvalonEdit.Rendering.TextView.CurrentLineBackgroundProperty,"AssistantSurfaceBrush");
            using(var resource=typeof(MarkdownEditor).Assembly.GetManifestResourceStream("VS2017.AIAssistant.MarkdownnHighlighting.xshd")){if(resource!=null){syntaxColorizer=new MarkdownnSyntaxColorizer(Editor,MarkdownnCodeHighlighting.LoadDefinition(resource));Editor.TextArea.TextView.LineTransformers.Add(syntaxColorizer);}}
            fenceColorizer=new MarkdownnFenceColorizer(Editor);Editor.TextArea.TextView.LineTransformers.Add(fenceColorizer);
            paletteDescriptor=DependencyPropertyDescriptor.FromProperty(ForegroundProperty,typeof(TextEditor));paletteDescriptor.AddValueChanged(Editor,EditorPaletteChanged);if(syntaxColorizer!=null)syntaxColorizer.RefreshTheme();
            Preview=new MarkdownPreview{MinHeight=140,AssetBaseDirectory=AssetBaseDirectory,Margin=new Thickness(4)};
            body=new Grid();body.ColumnDefinitions.Add(new ColumnDefinition());body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(5)});body.ColumnDefinitions.Add(new ColumnDefinition());body.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});body.RowDefinitions.Add(new RowDefinition());cursorLabel=new TextBlock{Margin=new Thickness(5)};Grid.SetColumnSpan(cursorLabel,3);body.Children.Add(cursorLabel);Grid.SetRow(Editor,1);body.Children.Add(Editor);splitter=new GridSplitter{Width=5,HorizontalAlignment=HorizontalAlignment.Stretch,VerticalAlignment=VerticalAlignment.Stretch,Background=Brushes.SlateGray};splitter.SetResourceReference(Control.BackgroundProperty,"AssistantBorderBrush");Grid.SetColumn(splitter,1);Grid.SetRow(splitter,1);body.Children.Add(splitter);Grid.SetColumn(Preview,2);Grid.SetRow(Preview,1);body.Children.Add(Preview);
            Tabs=new TabControl();Tabs.Items.Add(new TabItem{Header="편집 + 미리보기"});Tabs.Items.Add(new TabItem{Header="편집만"});Tabs.Items.Add(new TabItem{Header="미리보기만"});Grid.SetRow(Tabs,2);layout.Children.Add(Tabs);
            var discard=new WrapPanel{Visibility=Visibility.Collapsed};discard.Children.Add(new TextBlock{Text="저장하지 않은 변경이 있습니다. 버리고 계속할까요?",VerticalAlignment=VerticalAlignment.Center});AddTool(discard,"버리고 계속","작성 변경을 버리고 전환",(s,e)=>ConfirmTransition());AddTool(discard,"취소","현재 작성 유지",(s,e)=>CancelTransition());discardRow=discard;Grid.SetRow(discard,3);layout.Children.Add(discard);status=new TextBlock{Text="Markdown · UTF-8",Margin=new Thickness(5),TextWrapping=TextWrapping.Wrap};Grid.SetRow(status,4);layout.Children.Add(status);Content=layout;
            InitializeDocumentBrowser(layout);
            previewTimer=new DispatcherTimer(DispatcherPriority.Background,Dispatcher){Interval=TimeSpan.FromMilliseconds(160)};previewTimer.Tick+=PreviewTick;
            Editor.TextChanged+=EditorChanged;Editor.Document.Changing+=DocumentChanging;Editor.TextArea.TextEntering+=TextEntering;Editor.TextArea.Caret.PositionChanged+=CaretChanged;Editor.PreviewKeyDown+=EditorKeyDown;Editor.PreviewMouseWheel+=ZoomWithMouseWheel;Preview.PreviewMouseWheel+=ZoomWithMouseWheel;DataObject.AddPastingHandler(Editor,ValidatePaste);Tabs.SelectionChanged+=TabChanged;Unloaded+=EditorUnloaded;Loaded+=EditorLoaded;Tabs.SelectedIndex=0;ApplyViewMode();SynchronizeText();ApplyZoom();
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AddTool
        /// desc         : 키보드로 접근 가능한 원문 편집 버튼을 만든다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 호스트 테마가 표준 Button 스타일을 그대로 적용 가능.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static WrapPanel AddToolGroup(Panel toolbar,string title,string icon)
        {
            var content=new WrapPanel();var heading=new TextBlock{Text=icon+"  "+title,FontSize=11,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,5)};
            heading.SetResourceReference(TextBlock.ForegroundProperty,"AssistantMutedForegroundBrush");
            var groupBody=new StackPanel();groupBody.Children.Add(heading);groupBody.Children.Add(content);
            var group=new Border{BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(6),Padding=new Thickness(8,6,4,5),Margin=new Thickness(0,0,8,6),Child=groupBody};
            group.SetResourceReference(Border.BackgroundProperty,"AssistantSurfaceBrush");group.SetResourceReference(Border.BorderBrushProperty,"AssistantBorderBrush");
            toolbar.Children.Add(group);return content;
        }
        private static void AddTool(Panel tools,string label,string hint,RoutedEventHandler handler){var button=new Button{Content=label,ToolTip=hint,Margin=new Thickness(0,0,4,3)};button.SetResourceReference(FrameworkElement.StyleProperty,"AssistantActionButton");button.Click+=handler;tools.Children.Add(button);}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ValidText
        /// desc         : 바인딩 원문은 최대128Ki 문자로만 받아 무제한 메모리 사용을 막는다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 초과 원문을 조용히 잘라 저장하지 않고 DP 검증 실패로 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static bool ValidText(object value){return value==null||((string)value).Length<=CharacterLimit;}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ValidLength
        /// desc         : 호출자가 설정하는 사용자 입력 한도를 제한한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 기존 바인딩 원문은 작은 입력 한도로 잘라내지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static bool ValidLength(object value){return (int)value>0&&(int)value<=CharacterLimit;}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : TextUpdated
        /// desc         : 외부 원문 변경을 커서 범위를 보존해 작성기에 전달한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : DP 바인딩은 SetCurrentValue로 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void TextUpdated(DependencyObject sender,DependencyPropertyChangedEventArgs args){var editor=(MarkdownEditor)sender;editor.SynchronizeText();editor.OnTextChanged();}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : MaxLengthUpdated
        /// desc         : 새 입력 한도를 기본 작성기에 전달한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 기존 Text 값은 변경하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void MaxLengthUpdated(DependencyObject sender,DependencyPropertyChangedEventArgs args){var editor=(MarkdownEditor)sender;if(editor.status!=null)editor.status.Text="사용자 입력 한도 · "+editor.MaxLength+"자";}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : AssetsUpdated
        /// desc         : 호스트가 명시한 신뢰 이미지 폴더를 미리보기에만 적용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 일반 대화·공통 지시문은 기본 null 폴더를 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        private static void AssetsUpdated(DependencyObject sender,DependencyPropertyChangedEventArgs args){var editor=(MarkdownEditor)sender;if(editor.Preview!=null)editor.Preview.SetDocument(editor.Text??"",editor.AssetBaseDirectory);}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SynchronizeText
        /// desc         : 실제로 다른 원문만 복사하고 기존 선택 범위를 유효 범위에 유지한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 변경 이벤트 재귀와 불필요한 Undo 초기화를 피함.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void SynchronizeText(){if(Editor==null||disposed||synchronizing)return;synchronizing=true;try{string value=Text??"";if(Editor.Text!=value){int start=Editor.SelectionStart,length=Editor.SelectionLength;Editor.Text=value;Editor.Select(Math.Min(start,value.Length),Math.Min(length,Math.Max(0,value.Length-start)));}if(previewTimer!=null){previewTimer.Stop();if(IsLoaded)previewTimer.Start();}}finally{synchronizing=false;}}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : EditorChanged
        /// desc         : 사용자 편집만 양방향 원문에 반영하고 표시 갱신을 짧게 합친다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 파일·계정·AI 실행을 수행하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void EditorChanged(object sender,EventArgs args){if(synchronizing||disposed)return;synchronizing=true;try{SetCurrentValue(TextProperty,Editor.Text);}finally{synchronizing=false;}status.Text=IsDirty?"변경 내용 있음 · "+Editor.Document.TextLength+"자":"Markdown · UTF-8";previewTimer.Stop();if(IsLoaded)previewTimer.Start();}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OnTextChanged
        /// desc         : 호출자가 기존 저장 UI 상태를 갱신할 수 있도록 변경을 알린다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 저장 여부는 호스트가 결정.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void OnTextChanged(){if(!disposed)TextChanged?.Invoke(this,EventArgs.Empty);}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : TabChanged
        /// desc         : 미리보기 탭을 선택할 때 최신 작성 원문을 표시한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 자식 탭 이벤트의 중복 렌더링을 차단.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void TabChanged(object sender,SelectionChangedEventArgs args){if(ReferenceEquals(args.Source,Tabs))ApplyViewMode();}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : PreviewTick
        /// desc         : 연속 편집의 대기 중 미리보기를 한 번 표시한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 타이머는 실행 후 즉시 중지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void PreviewTick(object sender,EventArgs args){RefreshPreview();}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RefreshPreview
        /// desc         : 사용자 원문을 변경하지 않고 현재 보기만 갱신한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 테스트·호스트는 비공개 상태 조작 없이 갱신 가능.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void RefreshPreview(){if(disposed||Preview==null)return;previewTimer?.Stop();Preview.Markdown=Text??"";ApplyZoom();}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : EditorUnloaded
        /// desc         : 탭이나 대화가 숨겨지면 대기 중 표시 갱신을 중단한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 원문은 닫기·저장 결정까지 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void EditorUnloaded(object sender,RoutedEventArgs args){previewTimer.Stop();}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : EditorLoaded
        /// desc         : 재사용된 작성기의 미리보기를 선택 상태에 맞춰 복구한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 자동 포커스 이동 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void EditorLoaded(object sender,RoutedEventArgs args){RefreshPreview();}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReplaceSelection
        /// desc         : 한 입력 한도 안에서 선택 원문을 원자적인 Undo 단위로 교체한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 초과 편집은 전체 거부하여 기존 텍스트를 보존.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ReplaceSelection(string replacement,int selectionOffset,int selectionLength)
        {ReplaceRange(Editor.SelectionStart,Editor.SelectionLength,replacement,selectionOffset,selectionLength);}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : BoldClicked
        /// desc         : 선택 글을 Markdown 굵게 구문으로 감싼다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 빈 선택은 편집 가능한 예시를 삽입.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void BoldClicked(object sender,RoutedEventArgs args){string text=Editor.SelectionLength>0?Editor.SelectedText:"굵은 글";ReplaceSelection("**"+text+"**",2,text.Length);}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ListClicked
        /// desc         : 선택 줄들을 Markdown 목록으로 변환한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 선택한 CRLF·LF 구분을 그대로 유지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ListClicked(object sender,RoutedEventArgs args){TransformLines(line=>"- "+line);}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : LinkClicked
        /// desc         : URL 자리표시자를 가진 링크를 삽입하고 주소 편집 위치를 선택한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 버튼 자체는 링크를 열지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void LinkClicked(object sender,RoutedEventArgs args){string text=Editor.SelectionLength>0?Editor.SelectedText:"링크 글";ReplaceSelection("["+text+"](https://)",text.Length+3,8);}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CodeClicked
        /// desc         : 선택 코드보다 긴 fence로 원문을 코드 블록에 보존한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 코드 안 backtick으로 미리보기가 벗어나지 않도록 제한.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void CodeClicked(object sender,RoutedEventArgs args){string text=Editor.SelectionLength>0?Editor.SelectedText:"코드";int run=0,longest=2;foreach(char c in text){run=c=='`'?run+1:0;longest=Math.Max(longest,run);}string fence=new string('`',longest+1);ReplaceSelection(fence+"\n"+text+"\n"+fence,fence.Length+1,text.Length);}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : Dispose
        /// desc         : 영구히 닫힌 작성기의 대기 타이머·미디어를 정리한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 호스트 원문과 저장 파일은 수정하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void Dispose(){if(disposed)return;disposed=true;previewTimer.Stop();previewTimer.Tick-=PreviewTick;Editor.TextChanged-=EditorChanged;Editor.Document.Changing-=DocumentChanging;Editor.TextArea.TextEntering-=TextEntering;Editor.TextArea.Caret.PositionChanged-=CaretChanged;Editor.PreviewKeyDown-=EditorKeyDown;Editor.PreviewMouseWheel-=ZoomWithMouseWheel;Preview.PreviewMouseWheel-=ZoomWithMouseWheel;DataObject.RemovePastingHandler(Editor,ValidatePaste);Tabs.SelectionChanged-=TabChanged;searchText.KeyDown-=SearchKeyDown;Loaded-=EditorLoaded;Unloaded-=EditorUnloaded;pendingTransition=null;if(paletteDescriptor!=null)paletteDescriptor.RemoveValueChanged(Editor,EditorPaletteChanged);if(syntaxColorizer!=null)Editor.TextArea.TextView.LineTransformers.Remove(syntaxColorizer);Editor.TextArea.TextView.LineTransformers.Remove(fenceColorizer);fenceColorizer.Dispose();Preview.Dispose();}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : EditorPaletteChanged
        /// desc         : 상속·DynamicResource 글색 변경 시 독립 구문 팔레트와 fence 표시를 갱신한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 다른 편집기·원본 XSHD·사용자 원문은 변경하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void EditorPaletteChanged(object sender,EventArgs args){if(disposed)return;if(syntaxColorizer!=null)syntaxColorizer.RefreshTheme();Editor.TextArea.TextView.Redraw();}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ApplyViewMode
        /// desc         : 원본 작성기의 분할·편집·미리보기 전환과 splitter를 제공한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 제공된 MarkdownnPane 작성 기능 이식·유한 입력 및 원문 보호.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ApplyViewMode(){if(body==null||disposed||Tabs.SelectedIndex<0)return;foreach(TabItem tab in Tabs.Items)if(ReferenceEquals(tab.Content,body))tab.Content=null;((TabItem)Tabs.SelectedItem).Content=body;int mode=Tabs.SelectedIndex;Editor.Visibility=mode==2?Visibility.Collapsed:Visibility.Visible;Preview.Visibility=mode==1?Visibility.Collapsed:Visibility.Visible;splitter.Visibility=mode==0?Visibility.Visible:Visibility.Collapsed;body.ColumnDefinitions[0].Width=mode==2?new GridLength(0):new GridLength(1,GridUnitType.Star);body.ColumnDefinitions[1].Width=new GridLength(mode==0?5:0);body.ColumnDefinitions[2].Width=mode==1?new GridLength(0):new GridLength(1,GridUnitType.Star);if(mode!=1)RefreshPreview();}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DocumentChanging
        /// desc         : 직접 호출되는 문서 API도 절대 원문 한도를 넘기기 전에 거부한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 제공된 MarkdownnPane 작성 기능 이식·유한 입력 및 원문 보호.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void DocumentChanging(object sender,DocumentChangeEventArgs args){if((long)Editor.Document.TextLength-args.RemovalLength+args.InsertionLength>CharacterLimit)throw new InvalidOperationException("Markdown 원문은 128Ki 문자까지 지원합니다.");}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : TextEntering
        /// desc         : IME를 포함한 사용자 입력을 삽입 전에 한도 검사한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 제공된 MarkdownnPane 작성 기능 이식·유한 입력 및 원문 보호.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void TextEntering(object sender,TextCompositionEventArgs args){if((long)Editor.Document.TextLength-Editor.SelectionLength+args.Text.Length>MaxLength){args.Handled=true;status.Text="입력 한도를 넘는 변경은 적용하지 않았습니다.";}}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ValidatePaste
        /// desc         : 붙여넣기 전체가 한도 안에 들어올 때만 삽입을 허용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 제공된 MarkdownnPane 작성 기능 이식·유한 입력 및 원문 보호.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ValidatePaste(object sender,DataObjectPastingEventArgs args){string format=args.DataObject.GetDataPresent(DataFormats.UnicodeText)?DataFormats.UnicodeText:DataFormats.Text;string value=args.DataObject.GetData(format) as string;if(value==null||(long)Editor.Document.TextLength-Editor.SelectionLength+value.Length>MaxLength){args.CancelCommand();status.Text="붙여넣기 형식 또는 입력 한도를 확인해 주세요.";}}
        // Ctrl+휠 확대율을 편집기와 두 종류의 미리보기에 같은 비율로 적용합니다.
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ZoomWithMouseWheel
        /// desc         : 원본 Ctrl+휠 동작을 60~200% 표시 배율로 제한한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 제공된 MarkdownnPane 작성 기능 이식·유한 입력 및 원문 보호.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ZoomWithMouseWheel(object sender,MouseWheelEventArgs args){if((Keyboard.Modifiers&ModifierKeys.Control)==0)return;ZoomPercent*=Math.Pow(1.1,args.Delta/120.0);args.Handled=true;}
        // 미리보기 문서가 다시 만들어져도 사용자의 확대율을 유지합니다.
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ApplyZoom
        /// desc         : 배율을 편집기·native 미리보기에 함께 적용하고 원문은 보존한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 제공된 MarkdownnPane 작성 기능 이식·유한 입력 및 원문 보호.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ApplyZoom(){if(Editor==null||disposed)return;Editor.FontSize=13*zoomPercent/100.0;Preview.FontSize=13*zoomPercent/100.0;UpdateCursor();}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : CaretChanged
        /// desc         : 현재 행과 열을 상태 줄에 반영한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 제공된 MarkdownnPane 작성 기능 이식·유한 입력 및 원문 보호.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void CaretChanged(object sender,EventArgs args){UpdateCursor();}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : UpdateCursor
        /// desc         : 행·열·인코딩·확대율을 읽기 전용 상태로 표시한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 제공된 MarkdownnPane 작성 기능 이식·유한 입력 및 원문 보호.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void UpdateCursor(){if(cursorLabel!=null)cursorLabel.Text="Ln "+Editor.TextArea.Caret.Line+", Col "+Editor.TextArea.Caret.Column+" · UTF-8 · "+Math.Round(zoomPercent)+"%";}
        // VS Code의 주요 Markdown 편집 키와 줄 번호·찾기 동작을 한 편집기에 모읍니다.
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : EditorKeyDown
        /// desc         : 원본 서식·목록·체크·줄바꿈 단축키와 검색·내보내기를 연결한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 제공된 MarkdownnPane 작성 기능 이식·유한 입력 및 원문 보호.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void EditorKeyDown(object sender,KeyEventArgs args)
        {
            Dispatcher.VerifyAccess();
            var modifiers=Keyboard.Modifiers;bool handled=true;
            if(args.Key==Key.S&&modifiers==ModifierKeys.Control)ChooseSave();
            else if(args.Key==Key.S&&modifiers==(ModifierKeys.Control|ModifierKeys.Shift))ChooseExport();
            else if(args.Key==Key.F&&modifiers==ModifierKeys.Control)OpenSearch();
            else if(args.Key==Key.F3)FindNext((modifiers&ModifierKeys.Shift)!=0);
            else if(args.Key==Key.Z&&modifiers==ModifierKeys.Alt)Editor.WordWrap=!Editor.WordWrap;
            else if(args.Key==Key.B&&modifiers==ModifierKeys.Control)BoldClicked(sender,args);
            else if(args.Key==Key.I&&modifiers==ModifierKeys.Control)WrapSelection("*","*","기울임");
            else if(args.Key==Key.K&&modifiers==ModifierKeys.Control)LinkClicked(sender,args);
            else if(args.Key==Key.C&&modifiers==(ModifierKeys.Control|ModifierKeys.Shift))EditTaskLines(false);
            else if(args.Key==Key.Enter&&modifiers==ModifierKeys.Control)EditTaskLines(true);
            else if(args.Key==Key.V&&modifiers==(ModifierKeys.Control|ModifierKeys.Shift))ViewMode=ViewMode==2?0:2;
            else if(args.Key==Key.Enter&&modifiers==ModifierKeys.None&&Editor.SelectionLength==0)handled=ContinueMarkdownLine();
            else if(args.Key==Key.Down&&modifiers==ModifierKeys.None)MoveCaretToNextLineEnd();
            else if(args.Key==Key.Tab&&Editor.SelectionLength==0&&(modifiers==ModifierKeys.None||modifiers==ModifierKeys.Shift))handled=IndentMarkdownListLine(modifiers==ModifierKeys.Shift);
            else if(args.Key==Key.Back&&modifiers==ModifierKeys.None&&Editor.SelectionLength==0)handled=OutdentMarkdownListOnBackspace();
            else handled=false;
            if(handled)args.Handled=true;
        }
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : WrapSelection
        /// desc         : 공개 원문 편집 API로 선택 글을 Markdown 구문으로 감싼다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 제공된 MarkdownnPane 작성 기능 이식·유한 입력 및 원문 보호.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void WrapSelection(string before,string after,string placeholder){string value=Editor.SelectionLength==0?placeholder:Editor.SelectedText;ReplaceSelection(before+value+after,before.Length,value.Length);}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ReplaceRange
        /// desc         : 한도 초과를 전체 거부하고 정상 편집을 단일 Undo 단위로 적용한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 제공된 MarkdownnPane 작성 기능 이식·유한 입력 및 원문 보호.
        /// </summary>
        /// ---------------------------------------------------------------------
        private bool ReplaceRange(int start,int length,string value,int selectStart,int selectLength){if(disposed||(long)Editor.Document.TextLength-length+value.Length>MaxLength){if(status!=null)status.Text="입력 한도를 넘는 변경은 적용하지 않았습니다.";return false;}Editor.Document.BeginUpdate();try{Editor.Document.Replace(start,length,value);}finally{Editor.Document.EndUpdate();}Editor.Select(start+selectStart,selectLength);if(ViewMode==2)ViewMode=0;Editor.Focus();return true;}
        // 선택 끝이 다음 줄의 시작이면 그 줄은 제외합니다. 기존 줄바꿈과 중첩 목록을 보존합니다.
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : EditTaskLines
        /// desc         : 체크박스 생성·완료 전환을 원본 helper로 계산한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 제공된 MarkdownnPane 작성 기능 이식·유한 입력 및 원문 보호.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void EditTaskLines(bool toggle){TransformLines(line=>MarkdownnEditing.TaskLine(line,toggle));}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : TransformLines
        /// desc         : 선택 행의 기존 줄바꿈을 보존해 한 번에 교체한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 제공된 MarkdownnPane 작성 기능 이식·유한 입력 및 원문 보호.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void TransformLines(Func<string,string> transform){var first=Editor.Document.GetLineByOffset(Editor.SelectionStart);int end=Editor.SelectionStart+Editor.SelectionLength;var last=Editor.Document.GetLineByOffset(end);if(Editor.SelectionLength>0&&last.Offset==end&&last!=first)last=last.PreviousLine;var output=new StringBuilder();for(var line=first;line!=null;line=line.NextLine){output.Append(transform(Editor.Document.GetText(line.Offset,line.Length)));if(line==last)break;output.Append(Editor.Document.GetText(line.EndOffset,line.DelimiterLength));}string value=output.ToString();ReplaceRange(first.Offset,last.EndOffset-first.Offset,value,0,value.Length);}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : IndentMarkdownListLine
        /// desc         : Tab·Shift+Tab 목록 들여쓰기 동작을 원문 한도 안에서 수행한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 제공된 MarkdownnPane 작성 기능 이식·유한 입력 및 원문 보호.
        /// </summary>
        /// ---------------------------------------------------------------------
        public bool IndentMarkdownListLine(bool outdent){int caret=Editor.CaretOffset;var line=Editor.Document.GetLineByOffset(caret);string value=Editor.Document.GetText(line.Offset,line.Length);if(!MarkdownnEditing.IsListLine(value))return false;if(outdent){int count=Math.Min(value.TakeWhile(c=>c==' ').Count(),Editor.Options.IndentationSize);if(count==0&&value.StartsWith("\t",StringComparison.Ordinal))count=1;if(count>0)ReplaceRange(line.Offset,count,"",Math.Max(0,caret-line.Offset-count),0);}else{string indent=new string(' ',Editor.Options.IndentationSize);ReplaceRange(line.Offset,0,indent,caret-line.Offset+indent.Length,0);}return true;}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OutdentMarkdownListOnBackspace
        /// desc         : 들여쓰기 안이나 빈 목록 항목 끝에서 Backspace 내어쓰기를 수행한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 제공된 MarkdownnPane 작성 기능 이식·유한 입력 및 원문 보호.
        /// </summary>
        /// ---------------------------------------------------------------------
        public bool OutdentMarkdownListOnBackspace(){int caret=Editor.CaretOffset;var line=Editor.Document.GetLineByOffset(caret);string value=Editor.Document.GetText(line.Offset,line.Length);int indentation=value.TakeWhile(c=>c==' '||c=='\t').Count();return indentation>0&&MarkdownnEditing.IsListLine(value)&&(caret<=line.Offset+indentation||caret==line.EndOffset&&MarkdownnEditing.IsEmptyListLine(value))&&IndentMarkdownListLine(true);}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : MoveCaretToNextLineEnd
        /// desc         : 원본 작성기의 다음 줄 끝 이동을 제공한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 제공된 MarkdownnPane 작성 기능 이식·유한 입력 및 원문 보호.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void MoveCaretToNextLineEnd(){var current=Editor.Document.GetLineByOffset(Editor.CaretOffset);var next=current.NextLine??current;Editor.Select(next.EndOffset,0);Editor.TextArea.Caret.BringCaretToView();}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ContinueMarkdownLine
        /// desc         : 목록·인용 이어쓰기와 빈 항목 종료를 한 번의 변경으로 처리한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 제공된 MarkdownnPane 작성 기능 이식·유한 입력 및 원문 보호.
        /// </summary>
        /// ---------------------------------------------------------------------
        public bool ContinueMarkdownLine(){if(Editor.SelectionLength>0)return false;int caret=Editor.CaretOffset;var line=Editor.Document.GetLineByOffset(caret);if(caret!=line.EndOffset)return false;bool remove;string prefix=MarkdownnEditing.Continuation(Editor.Document.GetText(line.Offset,line.Length),out remove);if(prefix==null)return false;string newline=line.DelimiterLength>0?Editor.Document.GetText(line.EndOffset,line.DelimiterLength):(Text??"").Contains("\r\n")?"\r\n":"\n";string replacement=remove?prefix+newline:newline+prefix;ReplaceRange(remove?line.Offset:caret,remove?line.Length:0,replacement,replacement.Length,0);return true;}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OpenSearch
        /// desc         : Ctrl+F에서 선택 원문을 사용해 유한한 문자열 검색을 연다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 제공된 MarkdownnPane 작성 기능 이식·유한 입력 및 원문 보호.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void OpenSearch(){searchRow.Visibility=Visibility.Visible;if(Editor.SelectionLength>0&&Editor.SelectionLength<=512)searchText.Text=Editor.SelectedText;searchText.Focus();searchText.SelectAll();}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SearchKeyDown
        /// desc         : 찾기 입력에서 Enter·Shift+Enter·Escape를 처리한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 제공된 MarkdownnPane 작성 기능 이식·유한 입력 및 원문 보호.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void SearchKeyDown(object sender,KeyEventArgs args){if(args.Key==Key.Escape){searchRow.Visibility=Visibility.Collapsed;Editor.Focus();args.Handled=true;}else if(args.Key==Key.Enter){FindNext((Keyboard.Modifiers&ModifierKeys.Shift)!=0);args.Handled=true;}}
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : FindNext
        /// desc         : 다음·이전 문자열과 명시 선택한 유한 정규식 검색으로 원문 선택을 이동한다.
        /// author       : CodexCode
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexCode : 제공된 MarkdownnPane 작성 기능 이식·유한 입력 및 원문 보호.
        ///                2026-10-19 CodexCode : 대소문자·단어·정규식 옵션에512자·100ms한도 적용.
        /// </summary>
        /// ---------------------------------------------------------------------
        public bool FindNext(bool previous)
        {
            string pattern=searchText.Text;if(String.IsNullOrEmpty(pattern)||pattern.Length>512)return false;string text=Text??"";
            try
            {
                if(!SearchUseRegex)pattern=Regex.Escape(pattern);if(SearchWholeWords)pattern=@"(?<!\w)(?:"+pattern+@")(?!\w)";
                var options=RegexOptions.CultureInvariant|(SearchMatchCase?RegexOptions.None:RegexOptions.IgnoreCase)|(previous?RegexOptions.RightToLeft:RegexOptions.None);
                var regex=new Regex(pattern,options,TimeSpan.FromMilliseconds(100));int start=previous?Math.Max(0,Editor.SelectionStart-1):Math.Min(text.Length,Editor.SelectionStart+Math.Max(1,Editor.SelectionLength));
                Match found=regex.Match(text,start);if(!found.Success)found=regex.Match(text,previous?text.Length:0);
                if(!found.Success){status.Text="찾는 문자열이 없습니다.";return false;}Editor.Select(found.Index,found.Length);Editor.ScrollToLine(Editor.Document.GetLineByOffset(found.Index).LineNumber);return true;
            }
            catch(RegexMatchTimeoutException){status.Text="검색 시간 한도에 도달했습니다. 패턴을 단순하게 바꿔 주세요.";return false;}
            catch(ArgumentException){status.Text="정규식 형식을 확인해 주세요.";return false;}
        }

    }
}
