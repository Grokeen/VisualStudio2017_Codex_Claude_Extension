using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using VS2017.AIAssistant.Providers;
using VS2017.AIAssistant.Services;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : ScmIdentityTests
/// desc         : 형상관리 이름 조회의 읽기 명령·로그인 구분·비밀 배제·제한 시간을 실제 합성 exe로 검증한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : 한글 작성자·GitHub 이름·원격 호스트·프로세스 취소 검증.
/// </summary>
/// ---------------------------------------------------------------------
internal static class ScmIdentityTests
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    private static readonly List<string> passed = new List<string>(), errors = new List<string>();
    private static string root, project, git, gh, gcm, configPath, logPath;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : fixture Git/GH 실행과 독립 검사 진입을 분리한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 계정 인증·외부 네트워크 없이 실제 subprocess 사용.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        if (args.Length > 0 && args[0] == "--identity-child") { Thread.Sleep(30000); return 0; }
        string name = Path.GetFileName(Assembly.GetExecutingAssembly().Location);
        if (name.StartsWith("Fixture ", StringComparison.Ordinal) || name == "git.exe" || name == "git-credential-manager.exe") return Fixture(args, name.Contains("GCM") || name == "git-credential-manager.exe" ? "gcm" : name.Contains("GH") ? "gh" : "git");
        root = Path.GetFullPath(args[0]); Directory.CreateDirectory(root);
        project = Path.Combine(root,"프로젝트 & $(실행하지않음)"); Directory.CreateDirectory(project);
        git = Path.Combine(root,"Fixture Git 한글.exe"); gh = Path.Combine(root,"Fixture GH 한글.exe");
        gcm = Path.Combine(root,"Fixture GCM 한글.exe");
        File.Copy(Assembly.GetExecutingAssembly().Location,git,true); File.Copy(Assembly.GetExecutingAssembly().Location,gh,true);
        File.Copy(Assembly.GetExecutingAssembly().Location,gcm,true);
        configPath = Path.Combine(root,"fixture-config.json"); logPath = Path.Combine(root,"fixture-commands.jsonl");
        string old = Environment.GetEnvironmentVariable("VS2017_AI_SCM_FIXTURE_PATH");
        Environment.SetEnvironmentVariable("VS2017_AI_SCM_FIXTURE_PATH",configPath);
        try
        {
            Check("A verified GitHub API login is distinguished from the Git author", () => { Prepare(); var value=Read(); Assert(value.AccountName=="fixture-login" && value.Source=="GitHub CLI" && value.AuthenticationVerified); });
            Check("Git uses an argument vector with the exact selected project directory", () => {
                var commands=Commands(); Assert(commands[0].Tool=="git" && commands[0].Args.SequenceEqual(new[]{"-C",project,"rev-parse","--is-inside-work-tree"}) && commands[1].Args.SequenceEqual(new[]{"-C",project,"config","--get","user.name"})); });
            Check("GitHub identity uses only the fixed read-only user endpoint and hostname", () => Assert(Commands().Single(x=>x.Tool=="gh").Args.SequenceEqual(new[]{"api","user","--jq",".login","--hostname","github.com","--method","GET"})));
            Check("A Korean Git author is retained as an explicitly unverified fallback", () => { Prepare(); var value=new ScmIdentityService(git,"",gcmExecutable:"").ReadAsync(project,CancellationToken.None).GetAwaiter().GetResult(); Assert(value.AccountName=="한글 작성자" && !value.AuthenticationVerified && value.Description.Contains("로그인 미확인")); });
            Check("Disabling optional gh performs no origin lookup or GitHub process", () => Assert(Commands().Count==2 && !Commands().Any(x=>x.Tool=="gh")));
            Check("No GitHub login is claimed when gh authentication fails", () => { Prepare(new Settings { GhExit=1 }); var value=Read(); Assert(value.AccountName=="한글 작성자" && !value.AuthenticationVerified); });
            Check("Secret-looking CLI stderr and remote userinfo never appear in the result", () => { Prepare(new Settings { Remote="https://user:SYNTHETIC_SECRET@github.com/owner/repo.git", GhExit=1, GhError="TOKEN_MUST_NOT_APPEAR" }); var value=Read(); string text=Json.Serialize(value); Assert(!text.Contains("SYNTHETIC_SECRET") && !text.Contains("TOKEN_MUST_NOT_APPEAR") && !text.Contains("https://")); });
            Check("Successful GitHub identity does not expose remote URL credentials", () => { Prepare(new Settings {Remote="https://user:SYNTHETIC_SECRET@github.com/owner/repo.git"}); var value=Read(); Assert(value.AuthenticationVerified && !Json.Serialize(value).Contains("SYNTHETIC_SECRET")); });
            Check("SSH GitHub remote matches the same authenticated host", () => { Prepare(new Settings{Remote="git@github.com:owner/repository.git"}); Assert(Read().AuthenticationVerified); });
            Check("Explicit SSH URL matches GitHub without forwarding its user info", () => { Prepare(new Settings{Remote="ssh://git@github.com/owner/repository.git"}); Assert(Read().AuthenticationVerified); });
            foreach(string remote in new[]{"https://gitlab.example/owner/repo.git","https://github.com.evil.example/owner/repo.git","git@github.com.evil.example:owner/repo.git","file://github.com/owner/repo.git","https://enterprise.example/owner/repo.git"})
            { string candidate=remote; Check("Unrecognized remote never invokes authenticated gh: "+remote,()=>{Prepare(new Settings{Remote=candidate});var value=Read();Assert(!value.AuthenticationVerified && value.AccountName=="한글 작성자" && !Commands().Any(x=>x.Tool=="gh"));}); }
            Check("Malformed or multiline remote does not invoke gh",()=>{Prepare(new Settings{Remote="https://github.com/owner/repo.git\nhttps://evil.example"});Assert(!Read().AuthenticationVerified && !Commands().Any(x=>x.Tool=="gh"));});
            Check("A project without Git work-tree validation never reads identity settings",()=>{Prepare(new Settings{Repository=false});var value=Read();Assert(value.AccountName.Length==0 && !value.AuthenticationVerified && Commands().Count==1);});
            Check("Missing origin preserves author fallback",()=>{Prepare(new Settings{RemoteExit=2});var value=Read();Assert(value.AccountName=="한글 작성자" && !value.AuthenticationVerified && !Commands().Any(x=>x.Tool=="gh"));});
            Check("Missing author setting can still use a verified GitHub login",()=>{Prepare(new Settings{AuthorExit=1});Assert(Read().AuthenticationVerified);});
            Check("Multiline author setting is not treated as a single account name",()=>{Prepare(new Settings{Author="FIRST\nSECOND"});Assert(new ScmIdentityService(git,"",gcmExecutable:"").ReadAsync(project,CancellationToken.None).GetAwaiter().GetResult().AccountName.Length==0);});
            Check("An overlong author setting is not propagated into AI names",()=>{Prepare(new Settings{Author=new string('x',97)});Assert(new ScmIdentityService(git,"",gcmExecutable:"").ReadAsync(project,CancellationToken.None).GetAwaiter().GetResult().AccountName.Length==0);});
            Check("A malformed GH response is not marked as authenticated",()=>{Prepare(new Settings{Login="{\"access_token\":\"SYNTHETIC\"}"});var value=Read();Assert(!value.AuthenticationVerified && !Json.Serialize(value).Contains("SYNTHETIC"));});
            Check("Project-relative paths are rejected before launching a process",()=>{Prepare();Expect<ArgumentException>(()=>new ScmIdentityService(git,gh,gcmExecutable:"").ReadAsync("relative",CancellationToken.None).GetAwaiter().GetResult());Assert(Commands().Count==0);});
            Check("Project paths containing control characters are rejected",()=>{Prepare();Expect<ArgumentException>(()=>new ScmIdentityService(git,gh,gcmExecutable:"").ReadAsync(project+"\n",CancellationToken.None).GetAwaiter().GetResult());Assert(Commands().Count==0);});
            Check("Missing project directory is rejected",()=>Expect<ArgumentException>(()=>new ScmIdentityService(git,gh,gcmExecutable:"").ReadAsync(Path.Combine(root,"missing"),CancellationToken.None).GetAwaiter().GetResult()));
            Check("UNC identity lookup is rejected to retain a bounded local query",()=>Expect<ArgumentException>(()=>new ScmIdentityService(git,gh,gcmExecutable:"").ReadAsync(@"\\server\share",CancellationToken.None).GetAwaiter().GetResult()));
            Check("Batch and command wrappers cannot be used as native Git or gh",()=>{Expect<ArgumentException>(()=>new ScmIdentityService("git.cmd",gh));Expect<ArgumentException>(()=>new ScmIdentityService(git,"gh.bat"));});
            Check("Configured CLI relative paths are rejected",()=>Expect<ArgumentException>(()=>new ScmIdentityService("relative.exe",gh).ReadAsync(project,CancellationToken.None).GetAwaiter().GetResult()));
            Check("Timeout cannot exceed the five-second service boundary",()=>{Expect<ArgumentOutOfRangeException>(()=>new ScmIdentityService(git,gh,TimeSpan.FromSeconds(6)));Expect<ArgumentOutOfRangeException>(()=>new ScmIdentityService(git,gh,TimeSpan.Zero));});
            Check("A missing Git executable yields an unverified empty fallback",()=>{var value=new ScmIdentityService(Path.Combine(root,"missing.exe"),gh).ReadAsync(project,CancellationToken.None).GetAwaiter().GetResult();Assert(value.AccountName.Length==0&&!value.AuthenticationVerified);});
            Check("Pre-cancelled identity query launches no child",()=>{Prepare();using(var cancel=new CancellationTokenSource()){cancel.Cancel();Expect<OperationCanceledException>(()=>new ScmIdentityService(git,gh,gcmExecutable:"").ReadAsync(project,cancel.Token).GetAwaiter().GetResult());}Assert(Commands().Count==0);});
            Check("Excessive stdout is bounded and remains a generic fallback",()=>{Prepare(new Settings{HugeOutput=true});var value=Read();Assert(!value.AuthenticationVerified&&value.AccountName.Length==0);});
            Check("Excessive stderr is bounded without exposing it or losing the author fallback",()=>{Prepare(new Settings{GhError=new string('X',5000)});var value=Read();Assert(!value.AuthenticationVerified&&value.AccountName=="한글 작성자"&&!Json.Serialize(value).Contains(new string('X',100)));});
            Check("A hanging Git command stops within its global deadline",()=>{Prepare(new Settings{HangGit=true});var watch=Stopwatch.StartNew();var value=new ScmIdentityService(git,gh,TimeSpan.FromMilliseconds(250),"").ReadAsync(project,CancellationToken.None).GetAwaiter().GetResult();Assert(watch.Elapsed.TotalSeconds<2&&!value.AuthenticationVerified);AssertFixtureProcessesEnded();});
            Check("A hanging GitHub check preserves the already read author",()=>{Prepare(new Settings{HangGh=true});var value=new ScmIdentityService(git,gh,TimeSpan.FromMilliseconds(450),"").ReadAsync(project,CancellationToken.None).GetAwaiter().GetResult();Assert(value.AccountName=="한글 작성자"&&!value.AuthenticationVerified);AssertFixtureProcessesEnded();});
            Check("Cancellation terminates both the fixture parent and its nested child",()=>{Prepare(new Settings{HangGit=true,SpawnChild=true});using(var cancel=new CancellationTokenSource(400))Expect<OperationCanceledException>(()=>new ScmIdentityService(git,gh,gcmExecutable:"").ReadAsync(project,cancel.Token).GetAwaiter().GetResult());AssertFixtureProcessesEnded();});
            Check("Verified GitHub CLI takes priority over GCM stored account metadata",()=>{Prepare();var value=ReadWithGcm(gh);Assert(value.AuthenticationVerified&&!value.HasStoredAccount&&!Commands().Any(x=>x.Tool=="gcm"));});
            Check("A single stored GCM account works without gh but never claims server verification",()=>{Prepare();var value=ReadWithGcm();Assert(value.AccountName=="stored-one"&&value.Source=="Git Credential Manager"&&value.HasStoredAccount&&!value.AuthenticationVerified&&value.Description=="Git Credential Manager 저장 계정 · 서버 인증 미확인");});
            Check("GCM only receives the fixed public account list command with no UI",()=>Assert(Commands().Single(x=>x.Tool=="gcm").Args.SequenceEqual(new[]{"github","list","--no-ui"})));
            Check("Failed GitHub authentication can fall back to a stored GCM name",()=>{Prepare(new Settings{GhExit=1});var value=ReadWithGcm(gh);Assert(value.HasStoredAccount&&!value.AuthenticationVerified&&Commands().Any(x=>x.Tool=="gh"));});
            Check("Several stored accounts without a project selection retain author and ambiguity guidance",()=>{Prepare(new Settings{GcmAccounts="stored-one\nstored-two"});var value=ReadWithGcm();Assert(!value.HasStoredAccount&&value.AccountName=="한글 작성자"&&value.Description.Contains("여러 개"));});
            Check("An explicit remote username selects only its matching stored account without exposing a password",()=>{Prepare(new Settings{GcmAccounts="stored-one\nstored-two",Remote="https://stored-two:SYNTHETIC_SECRET@github.com/owner/repo.git"});var value=ReadWithGcm();Assert(value.HasStoredAccount&&value.AccountName=="stored-two"&&!Json.Serialize(value).Contains("SYNTHETIC_SECRET")&&!File.ReadAllText(logPath).Contains("SYNTHETIC_SECRET"));});
            Check("A GitHub credential username selects the matching account through fixed git config argv",()=>{Prepare(new Settings{GcmAccounts="stored-one\nstored-two",CredentialUsername="STORED-TWO"});var value=ReadWithGcm();Assert(value.HasStoredAccount&&value.AccountName=="stored-two"&&Commands().Any(x=>x.Args.SequenceEqual(new[]{"-C",project,"config","--get","credential.https://github.com.username"})));});
            Check("An unmatched explicit remote username never selects an arbitrary stored account",()=>{Prepare(new Settings{GcmAccounts="stored-one\nstored-two",Remote="https://other-user@github.com/owner/repo.git",CredentialUsername="stored-two"});Assert(!ReadWithGcm().HasStoredAccount);});
            Check("An unmatched credential username cannot choose among stored accounts",()=>{Prepare(new Settings{GcmAccounts="stored-one\nstored-two",CredentialUsername="other-user"});Assert(!ReadWithGcm().HasStoredAccount);});
            Check("A failed credential username lookup cannot choose among stored accounts",()=>{Prepare(new Settings{GcmAccounts="stored-one\nstored-two",CredentialUsername="stored-two",CredentialExit=1});Assert(!ReadWithGcm().HasStoredAccount);});
            Check("A single stored account conflicting with the explicit remote identity is not selected",()=>{Prepare(new Settings{Remote="https://other-user@github.com/owner/repo.git"});Assert(!ReadWithGcm().HasStoredAccount);});
            Check("SSH transport user git is not treated as the GitHub account selector",()=>{Prepare(new Settings{GcmAccounts="git\nstored-two",Remote="git@github.com:owner/repo.git"});Assert(!ReadWithGcm().HasStoredAccount);});
            Check("Case-insensitive duplicate GCM entries represent one stored account",()=>{Prepare(new Settings{GcmAccounts="Stored-One\nstored-one\n"});var value=ReadWithGcm();Assert(value.HasStoredAccount&&value.AccountName=="Stored-One");});
            foreach(string output in new[]{"stored-one\npassword=SYNTHETIC_SECRET","stored-one\ntoken: SYNTHETIC_SECRET","{\"access_token\":\"SYNTHETIC_SECRET\"}","https://user:SYNTHETIC_SECRET@github.com", "stored-one\tSYNTHETIC_SECRET"})
            {string candidate=output;Check("Secret-shaped GCM output is rejected as a whole: "+Array.IndexOf(new[]{"stored-one\npassword=SYNTHETIC_SECRET","stored-one\ntoken: SYNTHETIC_SECRET","{\"access_token\":\"SYNTHETIC_SECRET\"}","https://user:SYNTHETIC_SECRET@github.com", "stored-one\tSYNTHETIC_SECRET"},output),()=>{Prepare(new Settings{GcmAccounts=candidate});var value=ReadWithGcm();Assert(!value.HasStoredAccount&&value.AccountName=="한글 작성자"&&!Json.Serialize(value).Contains("SYNTHETIC_SECRET"));});}
            Check("Non-GitHub origin cannot invoke GCM even when a stored account is available",()=>{Prepare(new Settings{Remote="https://gitlab.example/owner/repo.git"});Assert(!ReadWithGcm().HasStoredAccount&&!Commands().Any(x=>x.Tool=="gcm"));});
            Check("A failed GCM list command cannot report stored account metadata",()=>{Prepare(new Settings{GcmExit=1});Assert(!ReadWithGcm().HasStoredAccount);});
            Check("Empty GCM account metadata retains the unverified author",()=>{Prepare(new Settings{GcmAccounts=""});var value=ReadWithGcm();Assert(value.AccountName=="한글 작성자"&&!value.HasStoredAccount);});
            Check("Excessive GCM output is bounded without returning its contents",()=>{Prepare(new Settings{GcmAccounts=new string('X',5000)});var value=ReadWithGcm();Assert(value.AccountName=="한글 작성자"&&!value.HasStoredAccount&&!Json.Serialize(value).Contains(new string('X',100)));});
            Check("A hanging GCM metadata query is terminated within the same global deadline",()=>{Prepare(new Settings{HangGcm=true});var watch=Stopwatch.StartNew();var value=new ScmIdentityService(git,"",TimeSpan.FromMilliseconds(450),gcm).ReadAsync(project,CancellationToken.None).GetAwaiter().GetResult();Assert(value.AccountName=="한글 작성자"&&!value.HasStoredAccount&&watch.Elapsed.TotalSeconds<2);AssertFixtureProcessesEnded();});
            Check("Caller cancellation during GCM metadata remains cancellation and ends its process",()=>{Prepare(new Settings{HangGcm=true});using(var cancel=new CancellationTokenSource(450))Expect<OperationCanceledException>(()=>new ScmIdentityService(git,"",gcmExecutable:gcm).ReadAsync(project,cancel.Token).GetAwaiter().GetResult());AssertFixtureProcessesEnded();});
            Check("GCM batch wrappers are rejected before starting any process",()=>Expect<ArgumentException>(()=>new ScmIdentityService(git,"",null,"gcm.cmd")));
            Check("Default GCM discovery uses the fixed native file under the selected Git installation",()=>{Prepare();string install=Path.Combine(root,"fixed-git"),nativeGit=Path.Combine(install,"cmd","git.exe"),nativeGcm=Path.Combine(install,"mingw64","bin","git-credential-manager.exe");Directory.CreateDirectory(Path.GetDirectoryName(nativeGit));Directory.CreateDirectory(Path.GetDirectoryName(nativeGcm));File.Copy(git,nativeGit,true);File.Copy(gcm,nativeGcm,true);CopyProductDependency(nativeGit);CopyProductDependency(nativeGcm);var value=new ScmIdentityService(nativeGit,"").ReadAsync(project,CancellationToken.None).GetAwaiter().GetResult();Assert(value.HasStoredAccount&&Commands().Single(x=>x.Tool=="gcm").Args.SequenceEqual(new[]{"github","list","--no-ui"}));});
            if(args.Length>1&&!String.IsNullOrEmpty(args[1]))Check("Actual local Git configuration returns the Korean author without claiming login",()=>RealGit(args[1]));
        }
        finally{Environment.SetEnvironmentVariable("VS2017_AI_SCM_FIXTURE_PATH",old);}
        var report=new{Succeeded=errors.Count==0,Passed=passed.Count,Failed=errors.Count,Checks=passed,Errors=errors,NetworkRequested=false,RealGitChecked=args.Length>1&&!String.IsNullOrEmpty(args[1]),CredentialFilesRead=false,ProductAssemblyPath=typeof(ScmIdentityService).Assembly.Location,CheckedAt=DateTimeOffset.Now.ToString("O")};
        string reportJson=Json.Serialize(report);File.WriteAllText(Path.Combine(root,"result.json"),reportJson,new UTF8Encoding(true));Console.WriteLine(reportJson);return errors.Count==0?0:1;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Prepare
    /// desc         : 각 검사에서 독립된 합성 응답과 명령 기록을 초기화한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 이전 검사의 네이티브 응답 상태를 재사용하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Prepare(Settings settings=null){settings=settings??new Settings();settings.LogPath=logPath;File.WriteAllText(configPath,Json.Serialize(settings),new UTF8Encoding(false));File.WriteAllText(logPath,"");}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Read
    /// desc         : 합성 네이티브 Git/GH를 주입하여 실제 비동기 서비스를 실행한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 구현을 모사하지 않고 프로세스 경계 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static ScmIdentity Read(){return new ScmIdentityService(git,gh,gcmExecutable:"").ReadAsync(project,CancellationToken.None).GetAwaiter().GetResult();}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ReadWithGcm
    /// desc         : 합성 GCM만 명시적으로 허용하여 실제 저장 계정을 읽지 않는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 기본 GitHub CLI도 비활성화하여 계정 목록 경계 격리.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static ScmIdentity ReadWithGcm(string github=""){return new ScmIdentityService(git,github,gcmExecutable:gcm).ReadAsync(project,CancellationToken.None).GetAwaiter().GetResult();}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CopyProductDependency
    /// desc         : 제품 DLL 참조 검사에서만 하위 폴더 fixture 옆에 같은 의존 DLL을 복사한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 독립 소스 컴파일은 복사하지 않고 고정 제품 DLL은 그대로 재사용.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void CopyProductDependency(string fixture)
    {
        if(typeof(ScmIdentityService).Assembly==Assembly.GetExecutingAssembly())return;
        string dependency=typeof(ScmIdentityService).Assembly.Location;
        File.Copy(dependency,Path.Combine(Path.GetDirectoryName(fixture),Path.GetFileName(dependency)),true);
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Commands
    /// desc         : fixture가 실제 받은 인수 벡터만 읽는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 원격 출력과 자격 정보는 명령 로그에 넣지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static List<Command> Commands(){return File.ReadAllLines(logPath).Where(x=>x.Length>0).Select(x=>Json.Deserialize<Command>(x)).ToList();}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Fixture
    /// desc         : 고정 Git/GH 읽기 명령을 합성 결과로 응답한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 명령·계정·대기·하위 프로세스를 네이티브 실행기로 재현.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Fixture(string[] args,string tool)
    {
        var settings=Json.Deserialize<Settings>(File.ReadAllText(Environment.GetEnvironmentVariable("VS2017_AI_SCM_FIXTURE_PATH")));
        File.AppendAllText(settings.LogPath,Json.Serialize(new Command{Tool=tool,Args=args,Pid=Process.GetCurrentProcess().Id})+"\n",new UTF8Encoding(false));
        if(tool=="gh"){if(settings.HangGh)Thread.Sleep(30000);Console.Error.Write(settings.GhError);Console.WriteLine(settings.Login);return settings.GhExit;}
        if(tool=="gcm"){if(!args.SequenceEqual(new[]{"github","list","--no-ui"}))return 9;if(settings.HangGcm)Thread.Sleep(30000);Console.WriteLine(settings.GcmAccounts);return settings.GcmExit;}
        if(args.Length<4||args[0]!="-C")return 9;
        if(args[2]=="rev-parse")
        {
            if(settings.SpawnChild){var child=Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,"--identity-child"){UseShellExecute=false,CreateNoWindow=true});File.AppendAllText(settings.LogPath,Json.Serialize(new Command{Tool="child",Args=new string[0],Pid=child.Id})+"\n",new UTF8Encoding(false));}
            if(settings.HangGit)Thread.Sleep(30000);Console.WriteLine(settings.HugeOutput?new string('x',5000):(settings.Repository?"true":"false"));return 0;
        }
        if(args[2]=="config"){if(args.Last()=="credential.https://github.com.username"){Console.WriteLine(settings.CredentialUsername);return settings.CredentialExit;}Console.WriteLine(settings.Author);return settings.AuthorExit;}
        if(args[2]=="remote"){Console.WriteLine(settings.Remote);return settings.RemoteExit;}return 9;
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AssertFixtureProcessesEnded
    /// desc         : Job 취소 이후 fixture 부모와 자식 PID가 모두 종료되었는지 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 사용자 프로세스와 구분된 기록 PID만 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void AssertFixtureProcessesEnded(){var watch=Stopwatch.StartNew();while(Commands().Any(x=>Alive(x.Pid))&&watch.ElapsedMilliseconds<1500)Thread.Sleep(20);Assert(!Commands().Any(x=>Alive(x.Pid)));}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Alive
    /// desc         : 테스트가 기록한 PID의 종료 여부를 경합에 안전하게 읽는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 이미 종료한 프로세스의 조회 실패 허용.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static bool Alive(int pid){try{using(var process=Process.GetProcessById(pid))return !process.HasExited;}catch(ArgumentException){return false;}}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RealGit
    /// desc         : 사용자 저장소 대신 임시 저장소의 로컬 설정으로 한글 작성자 반환을 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : gh 비활성화로 실제 네트워크 인증 요청 방지.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void RealGit(string nativeGit)
    {
        string directory=Path.Combine(root,"실제 Git 임시 저장소");Directory.CreateDirectory(directory);
        GitCommand(nativeGit,new[]{"init","--quiet",directory});GitCommand(nativeGit,new[]{"-C",directory,"config","--local","user.name","실제 합성 작성자"});
        var value=new ScmIdentityService(nativeGit,"",gcmExecutable:"").ReadAsync(directory,CancellationToken.None).GetAwaiter().GetResult();Assert(value.AccountName=="실제 합성 작성자"&&!value.AuthenticationVerified&&value.Description.Contains("로그인 미확인"));
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : GitCommand
    /// desc         : 테스트 준비에 필요한 임시 저장소 생성·로컬 설정만 실행한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 전역 설정·커밋·원격 작업을 수행하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void GitCommand(string executable,string[] args){using(var process=Process.Start(new ProcessStartInfo(executable,String.Join(" ",args.Select(ProviderExecutionOptions.QuoteArgument))){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true})){string output=process.StandardOutput.ReadToEnd();string error=process.StandardError.ReadToEnd();Assert(process.WaitForExit(5000)&&process.ExitCode==0);}}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 실패 원인을 기록하고 나머지 독립 검사를 계속한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 검사 결과 구조화.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(string name,Action action){try{action();passed.Add(name);}catch(Exception error){errors.Add(name+": "+error.Message);}}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Assert
    /// desc         : 기대한 사용자 결과가 아니면 검사를 실패시킨다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 묵시적 성공 방지.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Assert(bool value){if(!value)throw new InvalidOperationException("Expected identity boundary was not observed.");}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Expect
    /// desc         : 잘못된 입력과 취소가 명시적으로 전달되는지 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 예상 예외 종류 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Expect<T>(Action action)where T:Exception{try{action();}catch(T){return;}throw new InvalidOperationException("Expected "+typeof(T).Name);}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Settings
    /// desc         : 네트워크와 자격 정보 없는 합성 응답 옵션이다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실패·제한 시간 시나리오 지정.
    /// </summary>
    /// ---------------------------------------------------------------------
    private sealed class Settings{public string LogPath;public string Author="한글 작성자",Remote="https://github.com/owner/repo.git",Login="fixture-login",GhError="",GcmAccounts="stored-one",CredentialUsername="";public bool Repository=true,HangGit,HangGh,HangGcm,HugeOutput,SpawnChild;public int AuthorExit,RemoteExit,GhExit,GcmExit,CredentialExit;}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Command
    /// desc         : fixture가 실제 수신한 명령과 테스트 PID만 보관한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 원격 출력은 보관하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private sealed class Command{public string Tool;public string[] Args;public int Pid;}
}
