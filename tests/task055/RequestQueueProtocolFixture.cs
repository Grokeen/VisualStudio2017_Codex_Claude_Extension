using System;
using System.IO;
using System.Threading;
/// ---------------------------------------------------------------------
/// <summary>
/// name         : RequestQueueProtocolFixture
/// desc         : 순차 전체 액세스 검사용 로컬 Claude stream-json 응답을 수동 게이트로 지연한다.
/// author       : CodexOrig
/// create date  : 2026-10-20
/// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 실제 자식 프로세스·큐 순서 검증.
/// </summary>
/// ---------------------------------------------------------------------
internal static class RequestQueueProtocolFixture
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 격리된 요청 큐 검사에서 Main 경로를 연결한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-20
    /// update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 입력 FIFO 검증·문맥 보존.
    /// </summary>
    /// ---------------------------------------------------------------------
    private static void Main(string[] args)
    {
        Console.InputEncoding=Console.OutputEncoding=new System.Text.UTF8Encoding(false);
        if(Array.IndexOf(args,"--version")>=0){Console.WriteLine("2.1.302");return;}
        var json=new System.Web.Script.Serialization.JavaScriptSerializer();
        if(Array.IndexOf(args,"--json-schema")>=0){
            string prompt=Console.In.ReadToEnd();
            string room=prompt.Contains("EDIT-A")?"A":prompt.Contains("EDIT-B")?"B":"unknown";
            string directory=Environment.GetEnvironmentVariable("TASK055_ROOM_DIR")??Environment.CurrentDirectory;
            File.WriteAllText(Path.Combine(directory,"start-edit-"+room),System.Diagnostics.Process.GetCurrentProcess().Id.ToString());
            var editWatch=System.Diagnostics.Stopwatch.StartNew();
            while(!File.Exists(Path.Combine(directory,"release-edit-"+room))){
                if(editWatch.ElapsedMilliseconds>20000)throw new TimeoutException("Edit fixture gate not released");
                Thread.Sleep(5);
            }
            object[] edits=prompt.Contains("PROPOSAL")?new object[]{new{old_text="orig",new_text="updated-"+room}}:new object[0];
            Console.WriteLine(json.Serialize(new{type="result",subtype="success",is_error=false,structured_output=new{summary="fixture edit "+room+" completed",edits=edits}}));
            return;
        }
        string line;
        while((line=Console.ReadLine())!=null) {
            if(line.Contains("\"type\":\"control_request\"")) {
                var request=(System.Collections.Generic.Dictionary<string,object>)json.DeserializeObject(line);
                Console.WriteLine(json.Serialize(new{type="control_response",response=new{subtype="success",request_id=Convert.ToString(request["request_id"]),response=new{}}}));Console.Out.Flush();continue;
            }
            if(!line.Contains("\"type\":\"user\"")) continue;
            string room=line.Contains("ROOM-A")?"A":line.Contains("ROOM-B")?"B":null;
            if(room!=null){
                File.WriteAllText(Path.Combine(Environment.CurrentDirectory,"start-"+room),System.Diagnostics.Process.GetCurrentProcess().Id.ToString());
                var roomWatch=System.Diagnostics.Stopwatch.StartNew();
                while(!File.Exists(Path.Combine(Environment.CurrentDirectory,"release-"+room))){
                    if(roomWatch.ElapsedMilliseconds>20000)throw new TimeoutException("Room fixture gate not released");
                    Thread.Sleep(5);
                }
                Console.WriteLine("{\"type\":\"result\",\"subtype\":\"success\",\"is_error\":false,\"result\":\"fixture room "+room+" completed\"}");Console.Out.Flush();
                continue;
            }
            string trace=Path.Combine(Environment.CurrentDirectory,"queue-process-events.txt");
            int index=File.Exists(trace)?File.ReadAllLines(trace).Length/2+1:1;
            File.AppendAllText(trace,"start"+index+"\n");
            File.WriteAllText(Path.Combine(Environment.CurrentDirectory,"process-"+index+".pid"),System.Diagnostics.Process.GetCurrentProcess().Id.ToString());
            File.WriteAllText(Path.Combine(Environment.CurrentDirectory,"request-"+index+".json"),line);
            var watch=System.Diagnostics.Stopwatch.StartNew();
            while(!File.Exists(Path.Combine(Environment.CurrentDirectory,"release-"+index))) {
                if(watch.ElapsedMilliseconds>20000) throw new TimeoutException("Fixture gate not released");
                Thread.Sleep(5);
            }
            File.AppendAllText(trace,"end"+index+"\n");
            bool fail=line.Contains("FAIL FULL");
            Console.WriteLine(fail?"{\"type\":\"result\",\"subtype\":\"error_during_execution\",\"is_error\":true,\"result\":\"controlled fixture failure\"}":"{\"type\":\"result\",\"subtype\":\"success\",\"is_error\":false,\"result\":\"fixture completed\"}");Console.Out.Flush();
        }
    }
}
