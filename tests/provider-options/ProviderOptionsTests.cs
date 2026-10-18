using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using VS2017.AIAssistant.Providers;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : ProviderOptionsTests
/// desc         : 실제 캐시 형식·지원 기능·명령 인수의 경계를 합성 파일과 자식 프로세스로 검증한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : 공유 제품 빌드와 인증·네트워크 없이 독립 검사.
/// </summary>
/// ---------------------------------------------------------------------
internal static class ProviderOptionsTests
{
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 5000000 };
    private static readonly List<string> Passed = new List<string>(), Failed = new List<string>();
    private static string root;

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 테스트와 인수 회수 자식 프로세스를 구분하여 결과 JSON을 기록한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 네이티브 인수 전달과 제품 카탈로그 검증.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        if (args.Length > 0 && args[0] == "--echo") { Console.Write(Json.Serialize(args.Skip(1).ToArray())); return 0; }
        root = Path.GetFullPath(args[0]); Directory.CreateDirectory(root);
        string previous = Environment.GetEnvironmentVariable("VS2017_AI_MODELS_CACHE_PATH");
        try
        {
            string fixturePath = Path.Combine(root, "모델 캐시 fixture.json");
            string valid = Catalog(Model("fixture-deep", new[] { "low", "medium", "high", "xhigh", "max", "ultra" }, true, 1),
                Model("fixture-quick", new[] { "none", "minimal", "low" }, false, 2));
            File.WriteAllText(fixturePath, valid, new UTF8Encoding(false));
            Environment.SetEnvironmentVariable("VS2017_AI_MODELS_CACHE_PATH", fixturePath);
            Check("Catalogue reads actual Codex model schema with priority ordering", () => {
                var value = ProviderModelCatalog.LoadCodex(); Assert(value.Models.Count == 2 && value.Models[0].Id == "fixture-deep" && value.SourcePath == fixturePath); });
            Check("Model capabilities preserve advertised max and ultra levels", () => Assert(ProviderModelCatalog.LoadCodex().Find("fixture-deep").Efforts.SequenceEqual(new[] { "low", "medium", "high", "xhigh", "max", "ultra" })));
            Check("none and minimal are available only when the actual model advertises them", () => {
                Assert(new ProviderExecutionOptions("Codex", "fixture-quick", "minimal").ReasoningEffort == "minimal");
                Expect<ArgumentException>(() => new ProviderExecutionOptions("Codex", "fixture-deep", "minimal")); });
            Check("Fast support comes from model speed metadata", () => Assert(ProviderModelCatalog.LoadCodex().Find("fixture-deep").SupportsFast && !ProviderModelCatalog.LoadCodex().Find("fixture-quick").SupportsFast));
            Check("CLI default remains usable without hardcoded model names", () => Assert(new ProviderExecutionOptions("Codex").BuildProposalArguments().Length == 0));
            Check("Unknown custom model permits the bounded basic fallback", () => Assert(new ProviderExecutionOptions("Codex", "private-deployment", "high").ModelCapability == null));
            Check("Unknown model cannot silently request unverified advanced effort", () => Expect<ArgumentException>(() => new ProviderExecutionOptions("Codex", "private-deployment", "ultra")));
            Check("Unknown model cannot silently enable Fast", () => Expect<ArgumentException>(() => new ProviderExecutionOptions("Codex", "private-deployment", "auto", "fast")));
            Check("Known model rejects unsupported effort", () => Expect<ArgumentException>(() => new ProviderExecutionOptions("Codex", "fixture-quick", "high")));
            Check("Known model rejects unsupported Fast", () => Expect<ArgumentException>(() => new ProviderExecutionOptions("Codex", "fixture-quick", "auto", "fast")));
            Check("Auto effort omits a forced effort config", () => Assert(!new ProviderExecutionOptions("Codex", "fixture-deep").BuildProposalArguments().Any(x => x.Contains("reasoning_effort"))));
            Check("Codex Fast and ultra produce the real config argument vector", () => Assert(new ProviderExecutionOptions("Codex", "fixture-deep", "ultra", "fast").BuildProposalArguments().SequenceEqual(new[] { "--model", "fixture-deep", "--config", "model_reasoning_effort=\"ultra\"", "--config", "service_tier=\"fast\"", "--config", "features.fast_mode=true" })));
            Check("Builder never adds native tool, approval or sandbox permissions", () => Assert(!new ProviderExecutionOptions("Codex", "fixture-deep", "high", "fast").BuildProposalArguments().Any(x => x.Contains("sandbox") || x.Contains("approval") || x.Contains("tools") || x.Contains("bypass"))));
            Check("Static integration helper uses the isolated catalogue", () => Assert(ProviderExecutionOptions.BuildProposalArguments("Codex", "fixture-deep", "ultra", "fast").Contains("service_tier=\"fast\"")));
            Check("Provider values are case sensitive", () => Expect<ArgumentException>(() => new ProviderExecutionOptions("codex")));
            Check("Effort values are case sensitive", () => Expect<ArgumentException>(() => new ProviderExecutionOptions("Codex", "fixture-deep", "High")));
            Check("Speed values are case sensitive", () => Expect<ArgumentException>(() => new ProviderExecutionOptions("Codex", "fixture-deep", "auto", "FAST")));
            Check("Unsupported speed has a clear validation failure", () => Expect<ArgumentException>(() => new ProviderExecutionOptions("Codex", "fixture-deep", "auto", "ultrafast")));
            Check("Effort cannot inject config text", () => Expect<ArgumentException>(() => new ProviderExecutionOptions("Codex", "fixture-deep", "high\"\nservice_tier=\"fast")));
            Check("Model name cannot contain control characters", () => Expect<ArgumentException>(() => new ProviderExecutionOptions("Codex", "model\r\n--help")));
            Check("Model name cannot masquerade as a CLI option", () => Expect<ArgumentException>(() => new ProviderExecutionOptions("Codex", "--help")));
            Check("Model input length has a hard boundary", () => { Assert(new ProviderExecutionOptions("Codex", new string('x', 120)).Model.Length == 120); Expect<ArgumentException>(() => new ProviderExecutionOptions("Codex", new string('x', 121))); });
            Check("Descriptor validates case and default support", () => { Expect<ArgumentException>(() => new ProviderModelDescriptor("Codex", "model", "", new[] { "High" })); Expect<ArgumentException>(() => new ProviderModelDescriptor("Codex", "model", "", new[] { "low" }, "high")); });
            Check("Model catalogue and level collections are immutable copies", () => {
                var levels = new[] { "low", "new_level" }; var descriptor = new ProviderModelDescriptor("Codex", "next-model", "Next", levels, "low"); levels[0] = "changed";
                var models = new[] { descriptor }; var catalogue = new ProviderModelCatalog(models); models[0] = null;
                Assert(catalogue.Models[0].Efforts[0] == "low"); Expect<NotSupportedException>(() => ((IList<string>)descriptor.Efforts).Add("high")); });
            Check("Future advertised level is supported without an enum rebuild", () => {
                var catalogue = new ProviderModelCatalog(new[] { new ProviderModelDescriptor("Codex", "next-model", "Next", new[] { "new_level" }) });
                Assert(new ProviderExecutionOptions("Codex", "next-model", "new_level", "standard", catalogue).BuildProposalArguments().Contains("model_reasoning_effort=\"new_level\"")); });
            Check("Returned argv cannot mutate a later request", () => { var options = new ProviderExecutionOptions("Codex", "fixture-deep", "high"); var argv = options.BuildProposalArguments(); argv[1] = "changed"; Assert(options.BuildProposalArguments()[1] == "fixture-deep"); });
            Check("Wrong provider catalogue cannot cross provider boundaries", () => Expect<ArgumentException>(() => new ProviderExecutionOptions("Claude", "fixture-deep", "auto", "standard", ProviderModelCatalog.LoadCodex())));
            Check("Hidden models are excluded without exposing internal identifiers", () => Assert(ProviderModelCatalog.FromCodexJson(Catalog(Model("internal-only", new[] { "low" }, false, 0, "hide"))).Models.Count == 0));
            Check("Explicitly unavailable models are excluded", () => {
                var a = Model("unavailable", new[] { "low" }); a["availability"] = false;
                var b = Model("denied", new[] { "low" }); b["availability"] = "denied";
                var c = Model("object-disabled", new[] { "low" }); c["availability"] = new { is_available = false };
                Assert(ProviderModelCatalog.FromCodexJson(Catalog(a,b,c)).Models.Count == 0); });
            Check("API visibility is not confused with ChatGPT CLI availability", () => { var model = Model("chatgpt-only", new[] { "low" }); model["supported_in_api"] = false; Assert(ProviderModelCatalog.FromCodexJson(Catalog(model)).Models.Count == 1); });
            Check("Service tier priority metadata also advertises Fast", () => { var model = Model("tier-model", new[] { "low" }); model["service_tiers"] = new[] { new { id = "priority", description = "2x speed" } }; Assert(ProviderModelCatalog.FromCodexJson(Catalog(model)).Models[0].SpeedDescription == "2x speed"); });
            Check("Duplicate model IDs do not overwrite the first valid capability", () => { var data = ProviderModelCatalog.FromCodexJson(Catalog(Model("same", new[] { "low" }), Model("same", new[] { "ultra" }))); Assert(data.Models.Count == 1 && data.Models[0].Efforts.Single() == "low" && data.Warning.Length > 0); });
            Check("Broken model metadata is skipped with a bounded generic warning", () => { var model = Model("broken", new[] { "low" }); model["default_reasoning_level"] = "ultra"; var catalogue = ProviderModelCatalog.FromCodexJson(Catalog(model)); Assert(catalogue.Models.Count == 0 && catalogue.Warning.Length > 0); });
            Check("Credential-like and instruction cache fields never enter the product catalogue", () => { var model = Model("clean", new[] { "low" }); model["base_instructions"] = "SHOULD_NOT_ESCAPE"; var raw = Json.Serialize(new { identity = "PRIVATE_IDENTITY", models = new[] { model } }); string serialized = Json.Serialize(ProviderModelCatalog.FromCodexJson(raw)); Assert(!serialized.Contains("SHOULD_NOT_ESCAPE") && !serialized.Contains("PRIVATE_IDENTITY")); });
            Check("UTF8 BOM cache files are supported", () => { string path = Path.Combine(root, "bom.json"); File.WriteAllText(path, valid, new UTF8Encoding(true)); Assert(ProviderModelCatalog.LoadCodex(path).Models.Count == 2); });
            Check("Missing cache returns the CLI default fallback and a generic warning", () => { var value = ProviderModelCatalog.LoadCodex(Path.Combine(root, "missing.json")); Assert(value.Models.Count == 0 && value.Warning.Length > 0); });
            Check("Exclusive file lock does not crash model selection", () => { using (var file = File.Open(fixturePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) Assert(ProviderModelCatalog.LoadCodex(fixturePath).Models.Count == 0); });
            Check("Invalid UTF8 cache never becomes partially decoded model metadata", () => { string path = Path.Combine(root, "invalid-utf8.json"); File.WriteAllBytes(path, new byte[] { 123, 0xFF, 125 }); Assert(ProviderModelCatalog.LoadCodex(path).Models.Count == 0); });
            Check("Cache files larger than 2MB are rejected before deserialization", () => { string path = Path.Combine(root, "oversized.json"); using (var file = File.Create(path)) file.SetLength(ProviderModelCatalog.MaximumCacheBytes + 1); Assert(ProviderModelCatalog.LoadCodex(path).Models.Count == 0); });
            Check("Multibyte cache limit is measured in UTF8 bytes", () => Expect<InvalidDataException>(() => ProviderModelCatalog.FromCodexJson("{\"models\":[],\"text\":\"" + new string('한', 710000) + "\"}")));
            Check("Relative isolation environment path is rejected", () => { Environment.SetEnvironmentVariable("VS2017_AI_MODELS_CACHE_PATH", "relative-cache.json"); Assert(ProviderModelCatalog.LoadCodex().Models.Count == 0); Environment.SetEnvironmentVariable("VS2017_AI_MODELS_CACHE_PATH", fixturePath); });
            Check("Explicit cache argument overrides the isolation environment", () => { Environment.SetEnvironmentVariable("VS2017_AI_MODELS_CACHE_PATH", Path.Combine(root,"missing.json")); Assert(ProviderModelCatalog.LoadCodex(fixturePath).Models.Count == 2); Environment.SetEnvironmentVariable("VS2017_AI_MODELS_CACHE_PATH", fixturePath); });
            Check("JSON model list shape and entry count are bounded", () => { Expect<InvalidDataException>(() => ProviderModelCatalog.FromCodexJson("{\"models\":{}}")); Expect<InvalidDataException>(() => ProviderModelCatalog.FromCodexJson(Catalog(Enumerable.Range(0,257).Select(i=>Model("m"+i,new[] { "low" })).ToArray()))); });
            foreach (string invalid in new[] { "{'models':[]}", "{models:[]}", "{\"models\":[],}", "{\"models\":[],/*x*/\"x\":1}",
                "{\"models\":[],\"models\":[]}", "{\"models\":[],\"\\u006dodels\":[]}", "{\"models\":[],\"x\":NaN}", "{\"models\":[],\"x\":01}",
                "{\"models\":[],\"x\":1.}", "{\"models\":[],\"x\":1e+}", "{\"models\":[],\"x\":\"\\q\"}", "{\"models\":[]} trailing" })
            { string input = invalid; Check("Strict JSON rejects malformed cache: " + invalid.Substring(0, Math.Min(40, invalid.Length)), () => Expect<InvalidDataException>(() => ProviderModelCatalog.FromCodexJson(input))); }
            Check("Excessively nested JSON is rejected", () => Expect<InvalidDataException>(() => ProviderModelCatalog.FromCodexJson("{\"models\":[],\"x\":" + new string('[',35) + "0" + new string(']',35) + "}")));
            Check("Claude standard explicitly disables Fast without native permission changes", () => Assert(new ProviderExecutionOptions("Claude", "sonnet").BuildProposalArguments().SequenceEqual(new[] { "--model", "sonnet", "--settings", "{\"fastMode\":false}" })));
            Check("Claude Opus Fast maps to session settings JSON", () => { var options = new ProviderExecutionOptions("Claude", "opus", "high", "fast"); Assert(options.BuildProposalArguments().Contains("{\"fastMode\":true}") && options.MinimumCliVersion == new Version(2,1,205)); });
            Check("Claude Sonnet and Haiku cannot silently switch to an Opus Fast model", () => { Expect<ArgumentException>(()=>new ProviderExecutionOptions("Claude","sonnet","auto","fast")); Expect<ArgumentException>(()=>new ProviderExecutionOptions("Claude","haiku","auto","fast")); });
            Check("Claude explicit model exposes verified xhigh and max support", () => Assert(new ProviderExecutionOptions("Claude", "claude-opus-5-5", "max", "fast").BuildProposalArguments().Contains("max")));
            Check("Claude alias advanced capabilities remain conservative across endpoints", () => Expect<ArgumentException>(()=>new ProviderExecutionOptions("Claude","sonnet","xhigh")));
            Check("Claude Fast rejects an old or unknown CLI version", () => { var options=new ProviderExecutionOptions("Claude","opus","auto","fast"); Expect<InvalidOperationException>(()=>options.ValidateCliVersion(new Version(2,1,204))); Expect<InvalidOperationException>(()=>options.ValidateCliVersion(null)); options.ValidateCliVersion(new Version(2,1,205)); });
            Check("Claude latest explicit model requires its documented CLI version", () => { var options=new ProviderExecutionOptions("Claude","claude-opus-5-5"); Expect<InvalidOperationException>(()=>options.ValidateCliVersion(new Version(2,1,279))); options.ValidateCliVersion(new Version(2,1,280)); });
            Check("Windows argument quoting round-trips spaces, JSON, quotes, trailing slashes and shell-looking text", RoundTrip);
            Check("Windows argument quoting rejects native NUL and line breaks", () => { Expect<ArgumentException>(()=>ProviderExecutionOptions.QuoteArgument("a\0b")); Expect<ArgumentException>(()=>ProviderExecutionOptions.QuoteArgument("a\nb")); });
        }
        finally { Environment.SetEnvironmentVariable("VS2017_AI_MODELS_CACHE_PATH", previous); }
        var report = new { Succeeded = Failed.Count == 0, Passed = Passed.Count, Failed = Failed.Count, Checks = Passed, Errors = Failed,
            ProductAssemblyPath=typeof(ProviderExecutionOptions).Assembly.Location, NetworkRequested=false, PersonalCredentialsRead=false, CheckedAt=DateTimeOffset.Now.ToString("O") };
        string text = Json.Serialize(report); File.WriteAllText(Path.Combine(root,"result.json"),text,new UTF8Encoding(true)); Console.WriteLine(text);
        return Failed.Count == 0 ? 0 : 1;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Model
    /// desc         : 실제 Codex 모델 캐시와 같은 키를 사용하는 합성 항목을 만든다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 계정 정보 없는 모델 지원 기능 fixture.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static Dictionary<string, object> Model(string id, string[] levels, bool fast=false, int priority=0, string visibility="list")
    { return new Dictionary<string, object> { {"slug",id},{"display_name",id},{"supported_reasoning_levels",levels.Select(x=>new {effort=x,description="fixture"}).ToArray()}, {"default_reasoning_level",levels[0]}, {"additional_speed_tiers",fast?new[]{"fast"}:new string[0]}, {"priority",priority},{"visibility",visibility} }; }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Catalog
    /// desc         : 알려진 루트 목록 형식으로 합성 캐시 JSON을 직렬화한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 실제 캐시의 목록 모양을 fixture에 유지.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static string Catalog(params Dictionary<string,object>[] models) { return Json.Serialize(new {models}); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 각 검사 실패를 보존하면서 독립된 나머지 검사를 계속한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 사용자 결과와 원인 목록을 구조화.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(string name, Action action) { try { action(); Passed.Add(name); } catch(Exception error) { Failed.Add(name+": "+error.Message); } }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Assert
    /// desc         : 조건을 만족하지 않는 검사를 명시적으로 실패시킨다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 검사 결과 누락 방지.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Assert(bool condition) { if(!condition) throw new InvalidOperationException("Expected user-visible result was not observed."); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Expect
    /// desc         : 지원되지 않는 값이 예상한 예외로 거부되는지 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 예외 없는 묵시적 허용을 실패로 판정.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Expect<T>(Action action) where T:Exception { try { action(); } catch(T) { return; } throw new InvalidOperationException("Expected "+typeof(T).Name); }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : RoundTrip
    /// desc         : 실제 자식 프로세스가 받은 인수로 Windows 인코딩을 독립 확인한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 셸을 거치지 않는 실제 ProcessStartInfo 경계 검사.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void RoundTrip()
    {
        string[] expected={"", "공백 포함 인수", "embedded\"quote", "C:\\space folder\\", "two\\\\\"quotes", "{\"fastMode\":true}", "$(echo NO_EXECUTION) & untouched;"};
        var start=new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,String.Join(" ",new[]{"--echo"}.Concat(expected).Select(ProviderExecutionOptions.QuoteArgument)))
            {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=new UTF8Encoding(false)};
        using(var process=Process.Start(start))
        { string output=process.StandardOutput.ReadToEnd(); if(!process.WaitForExit(5000)) { process.Kill(); throw new TimeoutException("Synthetic child did not exit."); } Assert(process.ExitCode==0 && Json.Deserialize<string[]>(output).SequenceEqual(expected)); }
    }
}
