using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using VS2017.AIAssistant.Chat;
using VS2017.AIAssistant.Editing;
using VS2017.AIAssistant.Providers;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : ProviderCliIntegrationTests
/// desc         : 실제 제품 Provider가 네이티브 자식 프로세스에 전달한 모델·속도·역할 문맥을 검증한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : TASK-015 고정 DLL 및 독립 합성 CLI로 전달 경계 검증.
///                2026-10-18 CodexOrig : 공통 규칙의 원문 보존과 필수 규칙의 단일 stdin 조합 검증.
/// </summary>
/// ---------------------------------------------------------------------
internal static class ProviderCliIntegrationTests
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 2000000 };
    private static int passed;
    private static string directory, probe, fixture;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 검사 진입과 네이티브 CLI fixture 진입을 구분하고 결과를 남긴다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 사용자 파일·계정·네트워크를 사용하지 않는 제품 통합 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        Console.InputEncoding = new UTF8Encoding(false);
        if (args.Length != 1 || !Directory.Exists(args[0])) return RunFixture(args);
        directory = args[0]; fixture = Assembly.GetExecutingAssembly().Location;
        probe = Path.Combine(directory, "native-probe");
        string previousProbe = Environment.GetEnvironmentVariable("VS2017_AI_PROVIDER_PROBE");
        string previousCache = Environment.GetEnvironmentVariable("VS2017_AI_MODELS_CACHE_PATH");
        string previousFailure = Environment.GetEnvironmentVariable("VS2017_AI_PROVIDER_FAIL");
        try
        {
            Environment.SetEnvironmentVariable("VS2017_AI_PROVIDER_PROBE", probe);
            Environment.SetEnvironmentVariable("VS2017_AI_PROVIDER_FAIL", null);
            string cache = Path.Combine(directory, "models-cache.json");
            File.WriteAllText(cache, Json.Serialize(new { models = new[] { new {
                slug = "fixture-codex", display_name = "Fixture Codex", visibility = "list", priority = 1,
                default_reasoning_level = "high", supported_reasoning_levels = new[] {
                    new { effort = "low", description = "Low" }, new { effort = "high", description = "High" },
                    new { effort = "ultra", description = "Ultra" } }, additional_speed_tiers = new[] { "fast" }
            } } }), new UTF8Encoding(false));
            Environment.SetEnvironmentVariable("VS2017_AI_MODELS_CACHE_PATH", cache);
            RunChecks();
            File.WriteAllText(Path.Combine(directory, "result.json"), Json.Serialize(new { Passed = passed, Failed = 0 }), new UTF8Encoding(false));
            Console.WriteLine("Provider CLI integration: " + passed + "/" + passed + " PASS"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally
        {
            Environment.SetEnvironmentVariable("VS2017_AI_PROVIDER_PROBE", previousProbe);
            Environment.SetEnvironmentVariable("VS2017_AI_MODELS_CACHE_PATH", previousCache);
            Environment.SetEnvironmentVariable("VS2017_AI_PROVIDER_FAIL", previousFailure);
        }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RunFixture
    /// desc         : 받은 인수와 stdin만 기록하고 Codex/Claude 규약의 합성 수정안을 반환한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 AI나 소스 쓰기 없이 CLI 옵션과 비밀 분리를 확인.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int RunFixture(string[] args)
    {
        if (args.Length == 1 && args[0] == "--version")
        { Console.WriteLine(Environment.GetEnvironmentVariable("VS2017_AI_PROVIDER_OLD_VERSION") == "1" ? "2.1.10 (Claude Code)" : "2.1.999 (Claude Code)"); return 0; }
        string target = Environment.GetEnvironmentVariable("VS2017_AI_PROVIDER_PROBE");
        if (string.IsNullOrEmpty(target)) return 9;
        File.WriteAllText(target + ".args", Json.Serialize(args), new UTF8Encoding(false));
        File.WriteAllText(target + ".stdin", Console.In.ReadToEnd(), new UTF8Encoding(false));
        if (Environment.GetEnvironmentVariable("VS2017_AI_PROVIDER_FAIL") == "1")
        { Console.Error.WriteLine("SECRET_FIXTURE_ACCOUNT_DETAIL"); return 7; }
        var proposal = new { summary = "합성 인수 검증 수정안", edits = new[] { new { old_text = "return 1;", new_text = "return 2;" } } };
        Console.WriteLine(args.Contains("--print") ? Json.Serialize(new { structured_output = proposal }) : Json.Serialize(proposal));
        return 0;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RunChecks
    /// desc         : 실제 subprocess 인수·JSON 원문·기본 안전 제한·오류 전파를 검사한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : Fast와 깊은 추론을 기존 수정안 승인 경계와 함께 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void RunChecks()
    {
        string sourcePath = Path.Combine(directory, "Source.cs");
        File.WriteAllText(sourcePath, "return 1;", new UTF8Encoding(false));
        var source = new DocumentSnapshot(sourcePath, "return 1;", "fixture-hash", "C#7 .NET Framework 4.6.2", false, false);
        var provider = new CliEditProvider(TimeSpan.FromSeconds(15));
        var context = new ChatRequestContext("공통 규칙", "fixture-codex", null, null, "ultra", "fast", "에이전트 이름: 검토담당\n역할: 원본 확인\n수신 요청: 따옴표 \"검사\"");
        var proposal = provider.GenerateWithContextAsync("Codex", fixture, source, "숫자를 변경해 주세요.", context, CancellationToken.None).GetAwaiter().GetResult();
        string[] argv = ReadArguments();
        Check(proposal.ProposedText == "return 2;" && File.ReadAllText(sourcePath) == "return 1;", "Codex options produce a proposal while preserving source disk");
        Check(ValueAfter(argv, "--model") == "fixture-codex" && argv.Contains("model_reasoning_effort=\"ultra\""), "Advertised ultra effort and model reach native argv exactly");
        Check(argv.Contains("service_tier=\"fast\"") && argv.Contains("features.fast_mode=true"), "Codex fast mode reaches its actual config switches");
        Check(ValueAfter(argv, "--sandbox") == "read-only" && argv.Contains("--ignore-user-config") && argv.Contains("--ephemeral"), "Fast mode preserves read-only ephemeral proposal execution");
        Check(argv.Length >= 2 && argv[argv.Length - 2] == "--" && argv[argv.Length - 1] == "-", "Native stdin delimiter remains separate from variable options");
        string prompt = File.ReadAllText(probe + ".stdin");
        var payload = (Dictionary<string, object>)Json.DeserializeObject(prompt.Substring(prompt.IndexOf('\n') + 1));
        Check((string)payload["agent_context"] == context.AgentContext && context.Instructions == "공통 규칙" && (string)payload["user_instructions"] == MandatoryInstructionStore.DefaultInstructions + "\n\n공통 규칙", "Named collaboration context remains separate while mandatory and unchanged shared rules are delivered once");
        Check((string)payload["source_text"] == "return 1;" && !argv.Any(a => a.Contains("검토담당") || a.Contains("공통 규칙")), "Source and named-agent instructions travel through stdin only");

        context = new ChatRequestContext("", "fixture-codex", null, null, "high", "standard");
        provider.GenerateWithContextAsync("Codex", fixture, source, "변경해 주세요.", context, CancellationToken.None).GetAwaiter().GetResult();
        Check(!ReadArguments().Contains("service_tier=\"fast\""), "Standard mode does not request the Fast tier");

        context = new ChatRequestContext("", "opus", null, null, "high", "fast", "이름: Claude 검토자");
        proposal = provider.GenerateWithContextAsync("Claude", fixture, source, "변경해 주세요.", context, CancellationToken.None).GetAwaiter().GetResult();
        argv = ReadArguments();
        Check(proposal.ProposedText == "return 2;" && ValueAfter(argv, "--model") == "opus" && ValueAfter(argv, "--effort") == "high", "Claude model and effort survive its structured print protocol");
        var settings = Json.DeserializeObject(ValueAfter(argv, "--settings")) as Dictionary<string, object>;
        Check(settings != null && settings.ContainsKey("fastMode") && Equals(settings["fastMode"], true), "Claude print receives fastMode in its settings JSON");
        Check(ValueAfter(argv, "--tools") == "" && ValueAfter(argv, "--disallowedTools") == "mcp__*" && argv.Contains("--no-session-persistence"), "Claude Fast retains disabled tools and no persistent CLI session");

        string priorOldVersion = Environment.GetEnvironmentVariable("VS2017_AI_PROVIDER_OLD_VERSION");
        try
        {
            Environment.SetEnvironmentVariable("VS2017_AI_PROVIDER_OLD_VERSION", "1");
            Reject(delegate { provider.GenerateWithContextAsync("Claude", fixture, source, "변경", new ChatRequestContext("", "opus", null, null, "high", "fast"), CancellationToken.None).GetAwaiter().GetResult(); }, "Old Claude print CLI is rejected before a paid Fast request");
        }
        finally { Environment.SetEnvironmentVariable("VS2017_AI_PROVIDER_OLD_VERSION", priorOldVersion); }

        Reject(delegate { provider.GenerateWithContextAsync("Claude", fixture, source, "변경", new ChatRequestContext("", "sonnet", null, null, "high", "fast"), CancellationToken.None).GetAwaiter().GetResult(); }, "Unsupported Sonnet Fast is rejected before execution");
        Reject(delegate { provider.GenerateWithContextAsync("Codex", fixture, source, "변경", new ChatRequestContext("", "unknown-model", null, null, "ultra"), CancellationToken.None).GetAwaiter().GetResult(); }, "Deep effort is rejected for an unknown manual model");
        Environment.SetEnvironmentVariable("VS2017_AI_PROVIDER_FAIL", "1");
        try
        {
            provider.GenerateWithContextAsync("Codex", fixture, source, "변경", new ChatRequestContext(), CancellationToken.None).GetAwaiter().GetResult();
            throw new Exception("Expected native failure");
        }
        catch (InvalidOperationException error)
        {
            Check(error.Message.Contains("종료 코드 7") && error.Message.Contains("모델") && !error.Message.Contains("SECRET_FIXTURE_ACCOUNT_DETAIL"), "Native failures identify configuration checks without revealing CLI account stderr");
        }
        finally { Environment.SetEnvironmentVariable("VS2017_AI_PROVIDER_FAIL", null); }
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ReadArguments
    /// desc         : 네이티브 fixture가 받은 실제 argv를 원문 그대로 읽는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 셸 토큰화 추측 없이 경계 확인.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string[] ReadArguments() { return Json.Deserialize<string[]>(File.ReadAllText(probe + ".args")); }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ValueAfter
    /// desc         : 중복되지 않은 옵션의 실제 다음 인수를 읽는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 빈 문자열 값도 그대로 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string ValueAfter(string[] values, string key) { int index = Array.IndexOf(values, key); return index >= 0 && index + 1 < values.Length ? values[index + 1] : null; }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Reject
    /// desc         : 잘못된 옵션이 실제 native fixture 실행 전에 거부되는지 검사한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 기존 probe 파일 변경 여부도 비교.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Reject(Action action, string name)
    {
        string before = File.ReadAllText(probe + ".stdin"); bool rejected = false;
        try { action(); } catch (ArgumentException) { rejected = true; } catch (InvalidOperationException) { rejected = true; }
        Check(rejected && File.ReadAllText(probe + ".stdin") == before, name);
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 의미 있는 전달 경계 검사만 통과 개수와 함께 기록한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실패 시 즉시 비정상 종료.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL " + name); passed++; Console.WriteLine("PASS " + name); }
}
