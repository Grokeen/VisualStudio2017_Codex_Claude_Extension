using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using VS2017.AIAssistant.Views;

/// ---------------------------------------------------------------------
/// <summary>
/// name         : HelpContentChecks
/// desc         : 상세 안내의 공개 바인딩·실제 작업 구분·제공자 경계와 허위 수치 배제를 검사한다.
/// author       : CodexCode
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexCode : TASK-021 콘텐츠 전용 독립 검사, 계정·네트워크 사용 없음.
/// </summary>
/// ---------------------------------------------------------------------
internal static class HelpContentChecks
{
    private static readonly List<string> passed = new List<string>(), errors = new List<string>();

    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 설정 설명과 네 단계 CLI 카드의 사용자 요구를 결과 JSON으로 남긴다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 제품 DLL 참조와 실제 소스 독립 컴파일에 같은 검사 적용.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static int Main(string[] args)
    {
        Console.OutputEncoding=new UTF8Encoding(false);
        Check("Four XAML x:Static help properties are public read-only strings with readable paragraphs",()=>{
            foreach(string name in new[]{"Account","Model","Instructions","Display"}){
                var property=typeof(AssistantHelpContent).GetProperty(name,BindingFlags.Public|BindingFlags.Static);
                Assert(property!=null&&property.PropertyType==typeof(string)&&property.GetSetMethod()==null);
                string value=(string)property.GetValue(null);Assert(value.Split(new[]{"\n\n"},StringSplitOptions.None).Length>=5&&!value.Contains("\r")&&!value.Contains("<script"));
            }});
        Check("Account guidance separates executable selection, official login, connection verification and saving",()=>Assert(All(AssistantHelpContent.Account,".exe","PATH","공식 CLI","연결 확인","설정 저장","Git")));
        Check("Account guidance includes recovery and does not ask users to paste credentials",()=>Assert(All(AssistantHelpContent.Account,"오류","다시","암호","붙여넣지")));
        Check("Model guidance distinguishes capabilities, reasoning, speed and account entitlement",()=>Assert(All(AssistantHelpContent.Model,"직접","추론량","정확도","Fast","조직","거절")));
        Check("Usage guidance distinguishes reported tokens from unknown values, balance and billing",()=>Assert(All(AssistantHelpContent.Model,"CLI가 보고","0으로 추정하지","남은","최종 청구액")));
        Check("Instruction guidance distinguishes shared rules from conversation role and a particular request",()=>Assert(All(AssistantHelpContent.Instructions,"모든 프로젝트","Markdown","대화 입력란","역할·팀","다음 요청")));
        Check("Access guidance explains proposal review, native execution and actual OS authority boundaries",()=>Assert(All(AssistantHelpContent.Instructions,"수정안 비교","전체 access","OS 사용자 권한","관리자 권한","명령·테스트")));
        Check("Recovery guidance does not promise undo for all direct or external side effects",()=>Assert(All(AssistantHelpContent.Instructions,"외부 결과","모두 복구","중지·오류","자동 취소되었다고 가정하지")));
        Check("Display guidance explains fixed navigation, unsaved values, history and notification recovery",()=>Assert(All(AssistantHelpContent.Display,"목차","입력 중인 값","새 대화","A~Z","최근 알림","다시 보지 않기")));
        foreach(string provider in new[]{"Codex","Claude"}){
            string selected=provider;
            Check(selected+" CLI guide keeps four stable steps and actual executable guidance",()=>{
                var steps=AssistantHelpContent.CliSetupSteps(selected);
                Assert(steps.Length==4&&steps.Select(x=>x.Number).SequenceEqual(new[]{"1","2","3","4"}));
                Assert(steps.All(x=>x.Details.Count>=5)&&steps[0].Summary.Contains(selected));
                string text=String.Join("\n",steps.SelectMany(x=>x.Details));Assert(text.Contains(selected=="Codex"?"codex.exe":"claude.exe"));
                Assert(All(text,"공식","연결 확인","설정 저장","오류","실제","추정"));
            });
        }
        Check("Changing a returned guide array cannot corrupt a later opening",()=>{var first=AssistantHelpContent.CliSetupSteps("Codex");first[0]=null;Assert(AssistantHelpContent.CliSetupSteps("Codex")[0]!=null);});
        Check("Unknown and case-mismatched providers cannot become misleading executable instructions",()=>{
            foreach(string provider in new[]{null,"","codex","Claude;run-command","Unknown"}){bool rejected=false;try{AssistantHelpContent.CliSetupSteps(provider);}catch(ArgumentException){rejected=true;}Assert(rejected);}});
        Check("Help does not invent fixed model IDs, token balances, plan thresholds or numeric accuracy claims",()=>{
            string text=String.Join("\n",new[]{AssistantHelpContent.Account,AssistantHelpContent.Model,AssistantHelpContent.Instructions,AssistantHelpContent.Display}.Concat(new[]{"Codex","Claude"}.SelectMany(p=>AssistantHelpContent.CliSetupSteps(p).SelectMany(s=>s.Details))));
            Assert(!Regex.IsMatch(text,@"gpt-\d|claude-(?:opus|sonnet|haiku)-\d|\d+\s*%|\$\d|(?:잔여|남은)\s*\d+|Pro\s*(?:미만|이상)",RegexOptions.IgnoreCase));
        });
        var report=new{Succeeded=errors.Count==0,Passed=passed.Count,Failed=errors.Count,Checks=passed,Errors=errors,AssemblyPath=typeof(AssistantHelpContent).Assembly.Location,NetworkRequested=false,PersonalCredentialsRead=false,CheckedAt=DateTimeOffset.Now.ToString("O")};
        string json=new JavaScriptSerializer().Serialize(report);Directory.CreateDirectory(args[0]);File.WriteAllText(Path.Combine(args[0],"result.json"),json,new UTF8Encoding(true));Console.WriteLine(json);return errors.Count==0?0:1;
    }
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : All
    /// desc         : 설명에서 사용자 의사결정에 필요한 주제의 누락을 찾는다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 문장 전체 일치 대신 필요한 의미 구분 확인.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static bool All(string text,params string[] topics){return topics.All(text.Contains);}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Check
    /// desc         : 독립 검사 실패를 보존하며 다른 콘텐츠 경계 검사도 실행한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 부분 성공을 전체 성공으로 표시하지 않음.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Check(string name,Action action){try{action();passed.Add(name);}catch(Exception error){errors.Add(name+": "+error.Message);}}
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Assert
    /// desc         : 안내 계약을 만족하지 않으면 명시적으로 검사에 실패한다.
    /// author       : CodexCode
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexCode : 잘못된 콘텐츠의 조용한 통과 방지.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Assert(bool value){if(!value)throw new InvalidOperationException("Required help boundary was not observed.");}
}
