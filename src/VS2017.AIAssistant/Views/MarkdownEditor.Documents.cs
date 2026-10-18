using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace VS2017.AIAssistant.Views
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : MarkdownEditor
    /// desc         : 명시적으로 선택한 로컬 문서 폴더의 목록·이름 변경·삭제·현재 파일 저장을 제공한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : R3-03 참고 작성기의 누락된 파일 관리 기능 이식.
    /// </summary>
    /// ---------------------------------------------------------------------
    public sealed partial class MarkdownEditor
    {
        private ListBox documentList;
        private TextBox documentName;
        private TextBlock documentFolderLabel;
        private Expander documentBrowser;
        private WrapPanel deleteConfirmation;
        private string documentDirectory;
        private string pendingDeletePath;
        public string DocumentDirectory { get { return documentDirectory; } }
        public string[] ListedDocuments { get { return documentList == null ? new string[0] : documentList.Items.Cast<DocumentItem>().Select(x => x.Path).ToArray(); } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DocumentItem
        /// desc         : 표시 이름과 검증할 실제 단일 문서 경로를 함께 보관한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : 전체 경로를 편집 문자열로 추정하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private sealed class DocumentItem { public string Path; public override string ToString() { return System.IO.Path.GetFileName(Path); } }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : InitializeDocumentBrowser
        /// desc         : 접을 수 있는 목록을 탭 옆에 배치하고 좁은 창에서는 탭 위로 이동한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : 파일 선택·명시적 클릭 전에는 폴더를 읽거나 파일을 변경하지 않음.
        ///                2026-10-19 CodexOrig4 : 좁은 문서 목록의 내부 스크롤로 파일 동작·확인 영역 겹침과 잘림 방지.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void InitializeDocumentBrowser(Grid layout)
        {
            Dispatcher.VerifyAccess();
            var panel = new DockPanel();
            var actions = new WrapPanel(); DockPanel.SetDock(actions, Dock.Top); panel.Children.Add(actions);
            AddTool(actions, "폴더 선택", "로컬 Markdown 문서 폴더 선택", (s,e) => {Dispatcher.VerifyAccess();ChooseDocumentDirectory();});
            AddTool(actions, "새로 고침", "선택한 폴더의 문서 목록 갱신", (s,e) => RunFileAction(() => RefreshDocumentList(documentDirectory)));
            documentFolderLabel = new TextBlock { Text = "폴더를 선택해 주세요.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4), MaxHeight = 45 };
            DockPanel.SetDock(documentFolderLabel,Dock.Top); panel.Children.Add(documentFolderLabel);
            var fileActions = new StackPanel { Margin = new Thickness(4) }; DockPanel.SetDock(fileActions,Dock.Bottom); panel.Children.Add(fileActions);
            documentName = new TextBox { ToolTip = "현재 파일의 새 이름 (.md 또는 .markdown)", MaxLength = 160 };
            fileActions.Children.Add(documentName);
            var buttons = new WrapPanel(); fileActions.Children.Add(buttons);
            AddTool(buttons,"이름 변경","현재 문서 이름 변경 · 기존 파일 덮어쓰기 없음",(s,e)=>RunFileAction(()=>{Dispatcher.VerifyAccess();RenameCurrentDocument(documentName.Text);}));
            AddTool(buttons,"파일 위치","현재 문서가 있는 로컬 폴더 열기",(s,e)=>RunFileAction(()=>OpenCurrentDocumentLocation()));
            AddTool(buttons,"삭제","현재 파일 삭제 확인",(s,e)=>{if(CurrentPath!=null){pendingDeletePath=CurrentPath;deleteConfirmation.Visibility=Visibility.Visible;}});
            deleteConfirmation = new WrapPanel { Visibility = Visibility.Collapsed };
            deleteConfirmation.Children.Add(new TextBlock { Text = "현재 파일과 저장하지 않은 내용을 삭제할까요?", TextWrapping = TextWrapping.Wrap, MaxWidth = 170 });
            AddTool(deleteConfirmation,"삭제 확인","이 문서 파일만 삭제",(s,e)=>RunFileAction(()=>{Dispatcher.VerifyAccess();if(pendingDeletePath==null||!String.Equals(pendingDeletePath,CurrentPath,StringComparison.OrdinalIgnoreCase)){ResetDocumentConfirmation();return;}DeleteCurrentDocument(()=>true);ResetDocumentConfirmation();}));
            AddTool(deleteConfirmation,"취소","현재 파일과 작성 내용 유지",(s,e)=>ResetDocumentConfirmation());
            fileActions.Children.Add(deleteConfirmation);
            documentList = new ListBox { MinHeight = 50, DisplayMemberPath = "", ToolTip = "문서를 선택하고 Enter 또는 두 번 클릭하여 열기" };
            documentList.MouseDoubleClick += (s,e) => OpenSelectedDocument();
            documentList.PreviewKeyDown += (s,e) => { if(e.Key==Key.Enter){e.Handled=true;OpenSelectedDocument();} };
            panel.Children.Add(documentList);
            // 모든 파일 동작을 하나의 가시 영역으로 스크롤한다. DockPanel을 유한한 높이로 직접
            // 제한하면 하단 동작이 MinHeight를 가진 문서 목록과 겹치고 삭제 확인이 잘린다.
            var documentScroll = new ScrollViewer {
                Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                CanContentScroll = false, Focusable = false
            };
            documentBrowser = new Expander { Header = "문서 목록", Content = documentScroll, IsExpanded = false, Margin = new Thickness(0,0,7,0), HorizontalContentAlignment = HorizontalAlignment.Stretch };
            var host = new Grid();host.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });host.ColumnDefinitions.Add(new ColumnDefinition());host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });host.RowDefinitions.Add(new RowDefinition());
            layout.Children.Remove(Tabs);Grid.SetRow(host,2);layout.Children.Add(host);host.Children.Add(documentBrowser);host.Children.Add(Tabs);
            SizeChanged += (s,e) => { if(!disposed)LayoutDocumentBrowser(host); };
            documentBrowser.Expanded += (s,e) => LayoutDocumentBrowser(host);
            documentBrowser.Collapsed += (s,e) => LayoutDocumentBrowser(host);
            LayoutDocumentBrowser(host);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : LayoutDocumentBrowser
        /// desc         : 문서 목록이 작성 탭의 남은 폭을 가리지 않게 반응형 배치를 적용한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : 목록은 처음 접힌 상태이며 작은 창의 높이를 170으로 제한.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void LayoutDocumentBrowser(Grid host)
        {
            bool narrow=ActualWidth<620;
            documentBrowser.Width=narrow?double.NaN:documentBrowser.IsExpanded?210:double.NaN;
            documentBrowser.MaxHeight=narrow?170:double.PositiveInfinity;
            Grid.SetRow(documentBrowser,0);Grid.SetColumn(documentBrowser,0);Grid.SetColumnSpan(documentBrowser,narrow?2:1);Grid.SetRowSpan(documentBrowser,narrow?1:2);
            Grid.SetRow(Tabs,narrow?1:0);Grid.SetColumn(Tabs,narrow?0:1);Grid.SetColumnSpan(Tabs,narrow?2:1);Grid.SetRowSpan(Tabs,narrow?1:2);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ChooseDocumentDirectory
        /// desc         : 사용자가 직접 선택한 로컬 폴더에 대해서만 목록을 조회한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : 기본 폴더나 프로젝트 폴더의 자동 검색 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ChooseDocumentDirectory()
        {
            Dispatcher.VerifyAccess();
            using(var dialog=new System.Windows.Forms.FolderBrowserDialog { Description="Markdown 문서 폴더를 선택하세요.",ShowNewFolderButton=true })
                if(dialog.ShowDialog()==System.Windows.Forms.DialogResult.OK)RunFileAction(()=>{Dispatcher.VerifyAccess();SetDocumentDirectory(dialog.SelectedPath);documentBrowser.IsExpanded=true;});
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SetDocumentDirectory
        /// desc         : 정션·네트워크·장치 경로를 거부하고 최상위 로컬 문서만 유한하게 나열한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : 1024개 항목 검사·256개 Markdown 표시 상한, 재귀 탐색 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public void SetDocumentDirectory(string directory)
        {
            Dispatcher.VerifyAccess();if(disposed)throw new ObjectDisposedException("MarkdownEditor");
            string full=Path.GetDirectoryName(ValidateFilePath(Path.Combine(directory??"",".markdown-directory-check.md")));
            if(!Directory.Exists(full))throw new DirectoryNotFoundException();
            RefreshDocumentList(full);
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RefreshDocumentList
        /// desc         : 조회가 끝난 목록만 교체하여 실패 시 기존 목록과 폴더를 보존한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : 파일·초안은 목록 새로 고침으로 변경되지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void RefreshDocumentList(string directory)
        {
            if(documentList==null||String.IsNullOrEmpty(directory))return;
            ValidateFilePath(Path.Combine(directory,".markdown-directory-check.md"));
            var items=new List<string>();int inspected=0;
            foreach(string file in Directory.EnumerateFiles(directory))
            {
                if(++inspected>1024||items.Count>=256)break;
                string extension=Path.GetExtension(file);
                if((extension.Equals(".md",StringComparison.OrdinalIgnoreCase)||extension.Equals(".markdown",StringComparison.OrdinalIgnoreCase))&&(File.GetAttributes(file)&FileAttributes.ReparsePoint)==0)items.Add(file);
            }
            documentList.Items.Clear();foreach(string file in items.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase))documentList.Items.Add(new DocumentItem{Path=file});
            documentDirectory=directory;documentFolderLabel.Text=directory+(inspected>1024||items.Count>=256?" · 목록 한도 적용":"");documentName.Text=CurrentPath==null?"":Path.GetFileName(CurrentPath);documentName.ToolTip=CurrentPath==null?"현재 문서 이름":"현재 문서: "+CurrentPath;ResetDocumentConfirmation();
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ResetDocumentConfirmation
        /// desc         : 성공한 문서 전환 뒤에는 이전 파일의 삭제 확인을 승계하지 않는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : SC024-02 삭제 승인은 표시 당시 경로에도 결합하고 취소·전환 시 초기화.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ResetDocumentConfirmation(){pendingDeletePath=null;if(deleteConfirmation!=null)deleteConfirmation.Visibility=Visibility.Collapsed;}

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OpenSelectedDocument
        /// desc         : 목록의 실제 로컬 경로를 재검증하고 미저장 변경 확인 뒤에만 문서를 연다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : 이름 변경·삭제 확인은 새 문서에 승계하지 않음.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void OpenSelectedDocument()
        {
            var selected=documentList.SelectedItem as DocumentItem;if(selected==null)return;
            string path=selected.Path;deleteConfirmation.Visibility=Visibility.Collapsed;
            RequestTransition(()=>TryLoadFile(path,()=>true));
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : SaveCurrentFile
        /// desc         : 연 파일을 같은 경로에 UTF-8 원자 저장하며 새 문서는 파일 선택을 요구한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : Ctrl+S와 다른 이름 저장 동작을 구분.
        /// </summary>
        /// ---------------------------------------------------------------------
        public bool SaveCurrentFile(){Dispatcher.VerifyAccess();if(disposed)throw new ObjectDisposedException("MarkdownEditor");if(CurrentPath==null)return false;ExportUtf8(CurrentPath);return true;}

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ChooseSave
        /// desc         : 현재 문서는 그대로 저장하고 새 문서에만 저장 위치를 묻는다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : 명시적 사용자 저장 명령만 파일에 반영.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void ChooseSave(){Dispatcher.VerifyAccess();if(CurrentPath==null)ChooseExport();else RunFileAction(()=>{Dispatcher.VerifyAccess();SaveCurrentFile();});}

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : OpenCurrentDocumentLocation
        /// desc         : 사용자가 누른 경우에만 검증한 현재 Markdown 파일을 탐색기에서 선택한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : 명령 셸·파일 실행 없이 Windows 탐색기 고정 경로만 사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        private void OpenCurrentDocumentLocation()
        {
            if(CurrentPath==null)return;string path=ValidateFilePath(CurrentPath);if(!File.Exists(path))throw new FileNotFoundException();
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"),Arguments="/select,\""+path+"\"",UseShellExecute=false });
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : RenameCurrentDocument
        /// desc         : 현재 Markdown 파일의 이름만 변경하고 충돌·경로 이탈·덮어쓰기를 거부한다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : 미저장 원문·undo는 보존하고 파일 확장자·정션 검증 재사용.
        /// </summary>
        /// ---------------------------------------------------------------------
        public bool RenameCurrentDocument(string name)
        {
            Dispatcher.VerifyAccess();if(disposed)throw new ObjectDisposedException("MarkdownEditor");if(CurrentPath==null)return false;
            if(String.IsNullOrWhiteSpace(name)||name.Length>160||name.Any(Char.IsControl)||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0||name.EndsWith(".",StringComparison.Ordinal)||name.EndsWith(" ",StringComparison.Ordinal))throw new InvalidDataException("문서 파일 이름을 확인하세요.");
            string old=ValidateFilePath(CurrentPath);if(Path.GetExtension(name).Length==0)name+=".md";
            string target=ValidateFilePath(Path.Combine(Path.GetDirectoryName(old),name));
            if(String.Equals(old,target,StringComparison.OrdinalIgnoreCase))return true;
            if(File.Exists(target))throw new IOException("같은 이름의 문서가 있습니다.");
            File.Move(old,target);CurrentPath=target;RefreshDocumentList(Path.GetDirectoryName(target));return true;
        }

        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : DeleteCurrentDocument
        /// desc         : 명시적인 확인을 받은 현재 단일 Markdown 파일만 삭제하고 현재 문서를 비운다.
        /// author       : CodexOrig
        /// create date  : 2026-10-19
        /// update date  : 2026-10-19 CodexOrig : 확인 거부·실패는 원문과 파일을 유지하며 재귀 삭제 없음.
        /// </summary>
        /// ---------------------------------------------------------------------
        public bool DeleteCurrentDocument(Func<bool> confirm)
        {
            Dispatcher.VerifyAccess();if(disposed)throw new ObjectDisposedException("MarkdownEditor");if(CurrentPath==null||confirm==null||!confirm())return false;
            string path=ValidateFilePath(CurrentPath);if(!File.Exists(path))throw new FileNotFoundException();
            File.Delete(path);TryNewDocument(()=>true);RefreshDocumentList(Path.GetDirectoryName(path));return true;
        }
    }
}
