using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VS2017.AIAssistant.Services;
using VS2017.AIAssistant.Views;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : CliGuideChecks
/// desc         : 실제 설치 없는 callback fixture로 승인·중복·원본 hash·경로 경계와 안내 구조를 검사한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : source host와 실제 제품 DLL·동봉 문서 검사를 실행기가 구분.
/// </summary>
/// ---------------------------------------------------------------------
public static class CliGuideChecks
{
    public static readonly List<string> Checks=new List<string>(),Errors=new List<string>();
    /// <summary>
    /// name         : Run
    /// desc         : temp 자산만 변조하고 원본 배치·문서 및 실제 installer는 실행하지 않는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 프로세스 수명 fixture와 실제 동봉자산 조회를 분리.
    /// </summary>
    public static void Run(string fixtureRoot,string originalInstaller,string packageRoot,bool sourceOnly)
    {
        byte[] original=File.ReadAllBytes(originalInstaller);string originalHash=Hash(original);
        string good=Path.Combine(fixtureRoot,"valid");WriteFixture(good,original);
        Check("Original installer bytes match the compile-pinned release hash",delegate{Assert(originalHash==CliGuideService.InstallerSha256);});
        Check("Assets resolve only under the fixed assembly Resources/CliGuide directory",delegate{var s=new CliGuideService(good);Assert(s.AssetsDirectory==Path.Combine(good,"Resources","CliGuide") && s.GetPdfPath()==Path.Combine(s.AssetsDirectory,CliGuideService.PdfFileName));});
        Check("Relative drive-relative UNC and device installation paths are rejected",delegate{foreach(string path in new[]{"relative","C:relative","\\\\server\\share","\\\\?\\C:\\device"})Assert(Throws<ArgumentException>(delegate{new CliGuideService(path);}));});
        Check("Missing PDF reports a friendly local asset error rather than a browser 404",delegate{string root=Path.Combine(fixtureRoot,"missing");Directory.CreateDirectory(root);Assert(Throws<FileNotFoundException>(delegate{new CliGuideService(root).GetPdfPath();}));});
        Check("Missing Markdown retains offline installation login and connection guidance",delegate{var text=new CliGuideService(Path.Combine(fixtureRoot,"missing")).ReadMarkdownFallback();Assert(text.Contains("SUCCESS")&&text.Contains("로그인")&&text.Contains("다시")&&text.Contains("확장 패키지"));});
        Check("Malformed and oversized PDFs are rejected before browser navigation",delegate{foreach(string kind in new[]{"bad","large"}){string root=Path.Combine(fixtureRoot,kind);WriteFixture(root,original);File.WriteAllBytes(Path.Combine(root,"Resources","CliGuide",CliGuideService.PdfFileName),kind=="bad"?Encoding.UTF8.GetBytes("not PDF"):new byte[CliGuideService.MaximumPdfBytes+1]);Assert(Throws<InvalidDataException>(delegate{new CliGuideService(root).GetPdfPath();}));}});
        Check("Markdown is bounded and corrupted UTF8 retains offline fallback",delegate{foreach(string kind in new[]{"md-large","md-invalid"}){string root=Path.Combine(fixtureRoot,kind);WriteFixture(root,original);File.WriteAllBytes(Path.Combine(root,"Resources","CliGuide","HowToInstallCLI.md"),kind=="md-large"?new byte[CliGuideService.MaximumMarkdownBytes+1]:new byte[]{0xC3,0x28});Assert(new CliGuideService(root).ReadMarkdownFallback().Contains("텍스트 설명서를 읽지 못했습니다"));}});
        Check("Markdown fallback is readable text and does not execute embedded markup",delegate{string root=Path.Combine(fixtureRoot,"md-markup");WriteFixture(root,original);File.WriteAllText(Path.Combine(root,"Resources","CliGuide","HowToInstallCLI.md"),"# guide\n<script>do_not_run()</script>",new UTF8Encoding(false));Assert(new CliGuideService(root).ReadMarkdownFallback().Contains("<script>"));});
        Check("Only the exact local PDF and its page fragment are allowed",delegate{var s=new CliGuideService(good);string uri=new Uri(s.GetPdfPath()).AbsoluteUri;Assert(s.IsAllowedNavigation(uri)&&s.IsAllowedNavigation(uri+"#page=3"));});
        Check("Installed Korean PDF filename roundtrips through its encoded file URI",delegate{var s=new CliGuideService(good);var uri=new Uri(s.GetPdfPath());Assert(uri.AbsoluteUri.Contains("%EC")&&uri.LocalPath==s.GetPdfPath()&&s.IsAllowedNavigation(uri.AbsoluteUri));});
        Check("External protocols other files query strings and path-prefix tricks are denied",delegate{var s=new CliGuideService(good);string pdf=s.GetPdfPath();foreach(string uri in new[]{"https://example.invalid/","http://example.invalid/","javascript:alert(1)","data:text/html,x","file://server/share/Guide.pdf",new Uri(pdf).AbsoluteUri+"?x=1",new Uri(pdf+".other").AbsoluteUri,new Uri(Path.Combine(good,"outside.pdf")).AbsoluteUri})Assert(!s.IsAllowedNavigation(uri));});
        Check("Reparse-point asset directory is refused instead of escaping the installation",delegate{var s=new CliGuideService(Path.Combine(fixtureRoot,"junction-install"));Assert(Throws<InvalidOperationException>(delegate{s.GetPdfPath();}));});
        Check("Cancel returns before file validation and never invokes the launcher",delegate{int calls=0;var s=new CliGuideService(Path.Combine(fixtureRoot,"missing"),delegate(ProcessStartInfo info){calls++;return new FixtureProcess();});Assert(s.LaunchInstaller(false)==CliInstallerLaunchStatus.Cancelled&&calls==0&&!s.IsInstallationRunning);});
        Check("Approval starts exactly once and rejects duplicate execution",delegate{var run=new FixtureProcess();int calls=0;var s=new CliGuideService(good,delegate(ProcessStartInfo info){calls++;return run;});try{Assert(s.LaunchInstaller(true)==CliInstallerLaunchStatus.Started&&s.IsInstallationRunning&&s.LaunchInstaller(true)==CliInstallerLaunchStatus.AlreadyRunning&&calls==1);}finally{run.End(0);}});
        Check("Independent service instances share the process-wide installation gate",delegate{var run=new FixtureProcess();int second=0;var a=new CliGuideService(good,delegate(ProcessStartInfo info){return run;});var b=new CliGuideService(good,delegate(ProcessStartInfo info){second++;return new FixtureProcess();});try{a.LaunchInstaller(true);Assert(b.IsInstallationRunning&&b.LaunchInstaller(true)==CliInstallerLaunchStatus.AlreadyRunning&&second==0);}finally{run.End(0);}Assert(!b.IsInstallationRunning);});
        Check("Launcher receives only fixed system cmd path and quoted no-option batch path",delegate{var run=new FixtureProcess();ProcessStartInfo captured=null;var s=new CliGuideService(good,delegate(ProcessStartInfo info){captured=info;return run;});try{s.LaunchInstaller(true);Assert(captured.FileName==Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"cmd.exe")&&captured.Arguments=="/d /v:off /s /c \"\""+Path.Combine(s.AssetsDirectory,"InstallCLI.bat")+"\"\"");Assert(captured.UseShellExecute&&!captured.CreateNoWindow&&captured.WindowStyle==ProcessWindowStyle.Normal&&captured.Verb=="open"&&captured.WorkingDirectory==s.AssetsDirectory);}finally{run.End(0);}});
        Check("Batch verification holds a deny-write lock through the launch callback",delegate{var run=new FixtureProcess();bool denied=false;var s=new CliGuideService(good,delegate(ProcessStartInfo info){try{File.WriteAllBytes(Path.Combine(good,"Resources","CliGuide","InstallCLI.bat"),new byte[]{1});}catch(IOException){denied=true;}return run;});try{s.LaunchInstaller(true);Assert(denied);}finally{run.End(0);}});
        Check("Verified batch remains write and replacement locked after launch until actual exit",delegate{string root=Path.Combine(fixtureRoot,"lifetime-lock");WriteFixture(root,original);string path=Path.Combine(root,"Resources","CliGuide","InstallCLI.bat");var run=new FixtureProcess();var s=new CliGuideService(root,delegate(ProcessStartInfo info){return run;});try{s.LaunchInstaller(true);Assert(Throws<IOException>(delegate{File.WriteAllText(path,"tampered after launch");})&&Throws<IOException>(delegate{File.Move(path,path+".renamed");}));}finally{run.End(0);}File.WriteAllText(path,"fixture write after exit");Assert(File.ReadAllText(path)=="fixture write after exit");});
        Check("Throwing notification subscriber cannot unlock a still-running installer",delegate{var run=new FixtureProcess();int notices=0;var s=new CliGuideService(good,delegate(ProcessStartInfo info){return run;});s.InstallationChanged+=delegate{throw new InvalidOperationException("fixture subscriber error");};s.InstallationChanged+=delegate{notices++;};try{Assert(s.LaunchInstaller(true)==CliInstallerLaunchStatus.Started&&s.IsInstallationRunning&&s.LaunchInstaller(true)==CliInstallerLaunchStatus.AlreadyRunning&&notices==1);}finally{run.End(0);}Assert(!s.IsInstallationRunning&&s.LastExitCode==0&&notices==2);});
        Check("Closing the guide does not stop its installer or release the cross-instance gate",delegate{var run=new FixtureProcess();var s=new CliGuideService(good,delegate(ProcessStartInfo info){return run;});AssistantControl host=null;CliGuideDialog dialog=null;try{s.LaunchInstaller(true);host=new AssistantControl();dialog=new CliGuideDialog(host,s);dialog.Close();var next=new CliGuideService(good,delegate(ProcessStartInfo info){throw new InvalidOperationException("must not launch twice");});Assert(next.LaunchInstaller(true)==CliInstallerLaunchStatus.AlreadyRunning&&run.DisposeCount==0&&Throws<IOException>(delegate{File.AppendAllText(Path.Combine(s.AssetsDirectory,"InstallCLI.bat"),"tamper");}));}finally{run.End(0);if(dialog!=null)dialog.Close();var disposable=host as IDisposable;if(disposable!=null)disposable.Dispose();}Assert(run.DisposeCount==1&&!s.IsInstallationRunning);});
        Check("Changed real batch bytes are rejected before launch and release the gate",delegate{string root=Path.Combine(fixtureRoot,"tampered");WriteFixture(root,original);File.AppendAllText(Path.Combine(root,"Resources","CliGuide","InstallCLI.bat"),"\r\nrem changed fixture");int calls=0;var s=new CliGuideService(root,delegate(ProcessStartInfo info){calls++;return new FixtureProcess();});Assert(Throws<InvalidDataException>(delegate{s.LaunchInstaller(true);})&&calls==0&&!s.IsInstallationRunning);});
        Check("Oversized batch is rejected before invoking a process",delegate{string root=Path.Combine(fixtureRoot,"large-bat");WriteFixture(root,original);File.WriteAllBytes(Path.Combine(root,"Resources","CliGuide","InstallCLI.bat"),new byte[CliGuideService.MaximumInstallerBytes+1]);int calls=0;var s=new CliGuideService(root,delegate(ProcessStartInfo info){calls++;return new FixtureProcess();});Assert(Throws<InvalidDataException>(delegate{s.LaunchInstaller(true);})&&calls==0);});
        Check("Command metacharacters in a fixture installation path cannot inject arguments",delegate{foreach(string name in new[]{"amp&name","percent%name","bang!name","caret^name"}){string root=Path.Combine(fixtureRoot,name);WriteFixture(root,original);int calls=0;var s=new CliGuideService(root,delegate(ProcessStartInfo info){calls++;return new FixtureProcess();});Assert(Throws<InvalidOperationException>(delegate{s.LaunchInstaller(true);})&&calls==0&&!s.IsInstallationRunning);}});
        Check("Launcher failure does not leave duplicate-execution lock stuck",delegate{var s=new CliGuideService(good,delegate(ProcessStartInfo info){throw new IOException("fixture start failure");});Assert(Throws<IOException>(delegate{s.LaunchInstaller(true);})&&!s.IsInstallationRunning);});
        Check("Actual exit code is retained once without implying login success",delegate{var run=new FixtureProcess();var s=new CliGuideService(good,delegate(ProcessStartInfo info){return run;});s.LaunchInstaller(true);run.End(7);run.End(0);Assert(!s.IsInstallationRunning&&s.LastExitCode==7&&run.DisposeCount==1);});
        Check("Queued old exit notification cannot release a new process owner or its verified file",delegate{var old=new FixtureProcess();var next=new FixtureProcess();int launches=0;var s=new CliGuideService(good,delegate(ProcessStartInfo info){return ++launches==1?old:next;});s.LaunchInstaller(true);Action queued=old.CaptureExitNotification();old.End(1);try{s.LaunchInstaller(true);queued();Assert(s.IsInstallationRunning&&s.LastExitCode==null&&next.DisposeCount==0&&Throws<IOException>(delegate{File.AppendAllText(Path.Combine(s.AssetsDirectory,"InstallCLI.bat"),"race tamper");}));}finally{next.End(0);}Assert(!s.IsInstallationRunning&&next.DisposeCount==1);});
        Check("Already-exited callback process releases the gate without a missed event",delegate{var run=new FixtureProcess();run.End(0);var s=new CliGuideService(good,delegate(ProcessStartInfo info){return run;});Assert(s.LaunchInstaller(true)==CliInstallerLaunchStatus.Started&&!s.IsInstallationRunning&&s.LastExitCode==0&&run.DisposeCount==1);});
        Check("No default PDF app opens until the explicit service method is called",delegate{int calls=0;ProcessStartInfo captured=null;var s=new CliGuideService(good,null,delegate(ProcessStartInfo info){calls++;captured=info;});s.ReadMarkdownFallback();s.GetPdfPath();Assert(calls==0);s.OpenPdfExternally();Assert(calls==1&&captured.FileName==s.GetPdfPath()&&captured.Arguments==""&&captured.UseShellExecute&&captured.Verb=="open");});
        Check("WebView profile stays within the isolated workspace and is not an existing browser profile",delegate{var s=new CliGuideService(good);string expected=Path.Combine(Environment.GetEnvironmentVariable("VS2017_AI_WORKSPACE_PATH"),"CliGuideWebView2");Assert(s.GetBrowserDataDirectory()==expected&&!Directory.Exists(expected));});
        Check("Confirmation distinguishes network PATH normal user login and cancellation",delegate{string text=CliGuideService.Confirmation;Assert(text.Contains("인터넷")&&text.Contains("PATH")&&text.Contains("일반 사용자")&&text.Contains("별도")&&text.Contains("관리자 권한을 요청하지"));});
        Check("Only callback fixtures executed and the original batch remained byte-identical",delegate{Assert(Hash(File.ReadAllBytes(originalInstaller))==originalHash);});
        CheckDialog(good);
        if(!sourceOnly)
        {
            Check("Actual packaged PDF exists with valid signature and bounded size",delegate{var s=new CliGuideService(packageRoot);Assert(File.Exists(s.GetPdfPath()));});
            Check("Actual packaged installer byte hash equals the compiled trusted original",delegate{Assert(Hash(File.ReadAllBytes(Path.Combine(packageRoot,"Resources","CliGuide","InstallCLI.bat")))==CliGuideService.InstallerSha256);});
            Check("Actual packaged Markdown includes installation results login and troubleshooting",delegate{string text=new CliGuideService(packageRoot).ReadMarkdownFallback();Assert(text.Contains("## 03.")&&text.Contains("SUCCESS")&&text.Contains("## 05.")&&text.Contains("/check")&&text.Contains("## 14."));});
            Check("Actual WebView2 managed dependencies and architecture loaders are packaged",delegate{Assert(File.Exists(Path.Combine(packageRoot,"Microsoft.Web.WebView2.Core.dll"))&&File.Exists(Path.Combine(packageRoot,"Microsoft.Web.WebView2.Wpf.dll")));Assert(Directory.GetFiles(packageRoot,"WebView2Loader.dll",SearchOption.AllDirectories).Length>=2);});
        }
    }
    /// <summary>
    /// name         : CheckDialog
    /// desc         : 표시하지 않은 실제 Dialog의 공통 UI·fallback·확인 선택 구조를 검증한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : WebView 생성·설치·ShowDialog를 호출하지 않음.
    /// </summary>
    private static void CheckDialog(string root)
    {
        AssistantControl host=null;CliGuideDialog dialog=null;
        Check("Unshown guide inherits the common dialog without starting browser or installer",delegate{host=new AssistantControl();dialog=new CliGuideDialog(host,new CliGuideService(root));Assert(dialog is AssistantDialog && !dialog.IsVisible && Read(dialog,"browser")==null);});
        if(dialog!=null)
        {
            Check("Guide exposes centered PDF text external-app and separate installation actions",delegate{var labels=dialog.Actions.Children.OfType<Button>().ToArray();Assert(labels.Length>=5&&labels.Any(x=>x.Content.ToString().Contains("PDF 보기"))&&labels.Any(x=>x.Content.ToString().Contains("텍스트"))&&labels.Any(x=>x.Content.ToString().Contains("CLI 설치"))&&labels.All(x=>x.HorizontalContentAlignment==HorizontalAlignment.Center||x.Content.ToString()=="닫기"));});
            Check("Fallback is a readonly dark TextBox with literal Markdown content",delegate{var text=(TextBox)Read(dialog,"markdown");Assert(text.IsReadOnly&&text.Text.Contains("fixture guide")&&((SolidColorBrush)text.Background).Color.R<60);});
            Check("Offline fallback transition leaves no browser instance or installer launch",delegate{dialog.GetType().GetMethod("ShowFallback",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(dialog,new object[]{"fixture fallback"});Assert(Read(dialog,"browser")==null&&dialog.Message.Text=="fixture fallback");});
            Check("Installation confirmation uses the same dialog and defaults to cancellation",delegate{var type=typeof(CliGuideDialog).GetNestedType("InstallationConfirmationDialog",BindingFlags.NonPublic);var confirm=(AssistantDialog)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{host},null);try{var buttons=confirm.Actions.Children.OfType<Button>().ToArray();Assert(buttons.Length==2&&buttons.Single(x=>x.IsDefault).IsCancel&&buttons.Any(x=>x.Content.ToString().Contains("설치")));}finally{confirm.Close();}});
            dialog.Close();
        }
        var disposable=host as IDisposable;if(disposable!=null)disposable.Dispose();
    }
    /// <summary>
    /// name         : WriteFixture
    /// desc         : 실행되지 않는 테스트 복사본과 최소 PDF 서명·Markdown만 만든다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 원본 경로를 수정하지 않음.
    /// </summary>
    private static void WriteFixture(string root,byte[] installer){string path=Path.Combine(root,"Resources","CliGuide");Directory.CreateDirectory(path);File.WriteAllBytes(Path.Combine(path,"InstallCLI.bat"),installer);File.WriteAllText(Path.Combine(path,CliGuideService.PdfFileName),"%PDF-1.7\nfixture only\n",Encoding.ASCII);File.WriteAllText(Path.Combine(path,"HowToInstallCLI.md"),"# fixture guide\nOffline content\n",new UTF8Encoding(false));}
    /// <summary>
    /// name         : FixtureProcess
    /// desc         : 실제 명령 없이 종료 이벤트만 제공하는 명시적 설치 프로세스 fixture.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 설치 성공 증거로 집계하지 않음.
    /// </summary>
    private sealed class FixtureProcess : ICliGuideProcess
    {
        public event EventHandler Exited;public bool HasExited{get;private set;}public int? ExitCode{get;private set;}public int DisposeCount{get;private set;}
        /// <summary>
        /// name         : End
        /// desc         : fixture 종료와 중복 이벤트를 전달한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : OS 프로세스는 만들지 않음.
        /// </summary>
        public void End(int code){HasExited=true;ExitCode=code;var handler=Exited;if(handler!=null)handler(this,EventArgs.Empty);}
        /// <summary>
        /// name         : CaptureExitNotification
        /// desc         : 해제 전에 이미 큐에 들어간 과거 종료 콜백을 fixture에서 재현한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 실제 OS 프로세스나 원본 파일을 사용하지 않음.
        /// </summary>
        public Action CaptureExitNotification(){var handler=Exited;return delegate{if(handler!=null)handler(this,EventArgs.Empty);};}
        /// <summary>
        /// name         : Dispose
        /// desc         : 실제 핸들 없는 fixture 정리 횟수를 기록한다.
        /// author       : CodexCode
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexCode : 중복 정리 검증.
        /// </summary>
        public void Dispose(){DisposeCount++;}
    }
    /// <summary>
    /// name         : Read
    /// desc         : 실제 Dialog 구조만 읽고 private field는 수정하지 않는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 표시 없는 수명 검사.
    /// </summary>
    private static object Read(object value,string name){return value.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(value);}
    /// <summary>
    /// name         : Hash
    /// desc         : 원본·fixture 설치 파일의 실제 바이트를 SHA256으로 비교한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 줄바꿈 정규화 없이 비교.
    /// </summary>
    private static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}
    /// <summary>
    /// name         : Throws
    /// desc         : 정확한 거부 예외를 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 단순 실패를 임의 성공으로 집계하지 않음.
    /// </summary>
    private static bool Throws<T>(Action action) where T:Exception{try{action();return false;}catch(T){return true;}}
    /// <summary>
    /// name         : Check
    /// desc         : 독립 검사의 실제 성공과 실패를 기록한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 후속 경계 검사 계속 수행.
    /// </summary>
    private static void Check(string name,Action action){try{action();Checks.Add(name);}catch(Exception error){Errors.Add(name+": "+error.GetType().Name+": "+error.Message);}}
    /// <summary>
    /// name         : Assert
    /// desc         : 관찰값이 계약과 다를 때 검사를 실패시킨다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 installer 실행을 검증하지 않음.
    /// </summary>
    private static void Assert(bool value){if(!value)throw new InvalidOperationException("Observed CLI guide contract did not match.");}
}
