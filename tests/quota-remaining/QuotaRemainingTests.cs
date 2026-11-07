using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;
using VS2017.AIAssistant.Providers;

// CodexOrig / 2026-11-07: Compile actual product usage/catalog sources; no CLI, network or credentials.
internal static class QuotaRemainingTests
{
    private static readonly List<object> checks = new List<object>();
    private static int failed;
    private static void Check(bool pass, string name)
    { checks.Add(new { Name = name, Passed = pass }); if (!pass) failed++; }
    private static int Main(string[] arguments)
    {
        foreach (string culture in new[] { "ko-KR", "en-US", "de-DE" })
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            foreach (double used in new[] { 0.0, 100.0, 23.5 })
            {
                var usage = new ProviderUsageSnapshot { QuotaUsedPercent = used, ContextWindow = 1000, TotalTokens = 5000, Source = "synthetic quota" };
                string expected = (100 - used).ToString("0.#", CultureInfo.InvariantCulture) + "%";
                Check(usage.QuotaRemainingPercent == 100 - used && usage.Describe().Contains("계정 할당량 남음(사용률 기준): " + expected), culture + " quota " + used);
                Check(usage.Describe().Contains("남은 토큰 수·컨텍스트 잔여량이 아닙니다.") && usage.TotalTokens == 5000 && usage.ContextWindow == 1000, culture + " no context/token inference " + used);
            }
        }
        foreach (double? invalid in new double?[] { null, -0.01, 100.01, Double.NaN, Double.PositiveInfinity, Double.NegativeInfinity })
        {
            var usage = new ProviderUsageSnapshot { QuotaUsedPercent = invalid, InputTokens = 123, Source = "no valid quota" };
            Check(!usage.QuotaRemainingPercent.HasValue && usage.Describe().Contains("계정 할당량 남음(사용률 기준): 확인 불가") && usage.Describe().Contains("계정 할당량 사용률: 확인 불가"), "unknown/invalid " + invalid);
        }
        var beforeReset = DateTimeOffset.Parse("2026-11-08T00:00:00+09:00", CultureInfo.InvariantCulture);
        var update = new ProviderUsageSnapshot { QuotaUsedPercent = 75, QuotaResetsAt = beforeReset, Source = "actual source marker" };
        Check(update.Describe().Contains("25%") && update.Describe().Contains("한도 초기화:") && update.Describe().Contains("actual source marker"), "reset and source retained");
        update.QuotaUsedPercent = 0;
        Check(update.QuotaRemainingPercent == 100 && update.Describe().Contains("남음(사용률 기준): 100%"), "new report replaces old quota");
        update.QuotaUsedPercent = null;
        Check(!update.QuotaRemainingPercent.HasValue && update.Describe().Contains("남음(사용률 기준): 확인 불가"), "missing next report cannot retain computed remaining");
        AccountContextCases();
        bool dll = arguments.Length > 1 && arguments[1] == "dll";
        File.WriteAllText(arguments[0], new JavaScriptSerializer().Serialize(new { Passed = checks.Count - failed, Failed = failed, Checks = checks, ActualProductSources = !dll, ActualProductDll = dll, GuiExecuted = false, RealAIRequests = 0, Network = false }));
        return failed == 0 ? 0 : 1;
    }

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : AccountContextCases
    /// desc         : 과거 방 quota가 현재 계정 표시를 대체하지 못하고 양쪽 snapshot이 불변인지 확인한다.
    /// author       : CodexOrig 보조 개발 055
    /// create date  : 2026-11-07
    /// update date  :
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void AccountContextCases()
    {
        var json = new JavaScriptSerializer();
        foreach (string culture in new[] { "ko-KR", "en-US", "de-DE" })
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var tokens = new ProviderUsageSnapshot { InputTokens = 11, OutputTokens = 7, CachedInputTokens = 3, TotalTokens = 18,
                ContextWindow = 1000, QuotaUsedPercent = 75, QuotaResetsAt = DateTimeOffset.Parse("2026-11-08T00:00:00+09:00", CultureInfo.InvariantCulture),
                Source = "OLD_TOKEN_SOURCE", UpdatedAt = DateTimeOffset.Parse("2026-11-07T10:00:00+09:00", CultureInfo.InvariantCulture) };
            var account = new ProviderUsageSnapshot { InputTokens = 999, TotalTokens = 999, QuotaUsedPercent = 10,
                QuotaResetsAt = DateTimeOffset.Parse("2026-11-09T00:00:00+09:00", CultureInfo.InvariantCulture),
                Source = "CURRENT_ACCOUNT_SOURCE", UpdatedAt = DateTimeOffset.Parse("2026-11-07T11:00:00+09:00", CultureInfo.InvariantCulture) };
            string tokenBefore = json.Serialize(tokens), accountBefore = json.Serialize(account), legacy = tokens.Describe();
            string text = tokens.DescribeForAccount(account);
            Check(text.Contains("할당량 남음(사용률 기준): 90%") && text.Contains("할당량 사용률: 10%") && !text.Contains("할당량 사용률: 75%"), culture + " current account overrides cached room quota");
            Check(text.Contains("합계: 18 tokens") && text.Contains("입력(캐시 포함): 11 tokens") && !text.Contains("999 tokens"), culture + " current account does not replace token report");
            Check(text.Contains("토큰 통계 출처: OLD_TOKEN_SOURCE") && text.Contains("계정 할당량 출처: CURRENT_ACCOUNT_SOURCE"), culture + " token and account provenance are separate");
            Check(text.Contains("토큰 통계 갱신: " + tokens.UpdatedAt.ToLocalTime().ToString("HH:mm:ss")) && text.Contains("계정 할당량 갱신: " + account.UpdatedAt.ToLocalTime().ToString("HH:mm:ss")), culture + " token and account update times are separate");
            Check(json.Serialize(tokens) == tokenBefore && json.Serialize(account) == accountBefore && tokens.Describe() == legacy, culture + " both source snapshots and Describe contract unchanged");
            string unknown = tokens.DescribeForAccount(null);
            Check(unknown.Contains("할당량 남음(사용률 기준): 확인 불가") && unknown.Contains("계정 할당량 사용률: 확인 불가") && !unknown.Contains("한도 초기화:"), culture + " provider path change or query failure clears displayed quota and reset");
            Check(unknown.Contains("합계: 18 tokens") && unknown.Contains("현재 연결의 계정 한도 미확인") && unknown.Contains("계정 할당량 갱신: 미확인"), culture + " missing metadata preserves tokens with explicit unknown account source");
            var missing = new ProviderUsageSnapshot { Source = "CURRENT_NO_QUOTA" };
            string noQuota = tokens.DescribeForAccount(missing);
            Check(noQuota.Contains("할당량 남음(사용률 기준): 확인 불가") && !noQuota.Contains("한도 초기화:") && noQuota.Contains("계정 할당량 출처: CURRENT_NO_QUOTA"), culture + " new metadata without quota never revives cached quota");
            Check(json.Serialize(tokens) == tokenBefore && json.Serialize(account) == accountBefore, culture + " repeated presentation never mutates room or account cache");
        }
        var old = new ProviderUsageSnapshot { QuotaUsedPercent = 75 };
        foreach (double? invalid in new double?[] { null, -1, 101, Double.NaN, Double.PositiveInfinity })
            Check(old.DescribeForAccount(new ProviderUsageSnapshot { QuotaUsedPercent = invalid }).Contains("할당량 남음(사용률 기준): 확인 불가"), "invalid current quota cannot fall back to old 25 percent " + invalid);
        Check(new ProviderUsageSnapshot { Source = "ACCOUNT_ONLY_AS_USAGE" }.DescribeForAccount(new ProviderUsageSnapshot { QuotaUsedPercent = 25, Source = "CURRENT_ACCOUNT" }).Contains("토큰 통계 출처: CLI가 아직 토큰 통계를 제공하지 않음"), "account-only sample never claims token source");
    }
}
