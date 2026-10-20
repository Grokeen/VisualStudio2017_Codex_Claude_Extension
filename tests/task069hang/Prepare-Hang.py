# TASK069 / CodexOrig / 2026-10-20: immutable inputs and exact UI method extraction.
from pathlib import Path
import hashlib,json,re,sys
repo=Path(__file__).resolve().parents[2]
product_repo=Path(sys.argv[2]).resolve() if len(sys.argv)>2 else repo
output=Path(sys.argv[1]); output.mkdir(parents=True,exist_ok=False)
hashes=[]
def snapshot(relative):
    path=(product_repo if relative.startswith('src/') else repo)/relative; raw=path.read_bytes(); digest=hashlib.sha256(raw).hexdigest()
    (output/(path.name+'.original')).write_bytes(raw)
    assert hashlib.sha256(path.read_bytes()).hexdigest()==digest
    hashes.append(dict(Path=str(path),Sha256=digest))
    return raw.decode('utf-8-sig').replace('\r\n','\n')
for name in ('MarkdownWorkspaceStore.cs','MarkdownWorkspaceStore.Committed.cs','ConversationCollaboration.cs'):
    (output/name).write_text(snapshot('src/VS2017.AIAssistant/Chat/'+name),encoding='utf-8')
chat=snapshot('src/VS2017.AIAssistant/Chat/ChatWorkspace.cs')
match=re.search(r'    public sealed class ChatMessage\n.*?(?=    /// -+\n    /// <summary>\n    /// name         : ChatSession)',chat,re.S)
assert match
(output/'ChatMessage.actual.cs').write_text('using System;\nnamespace VS2017.AIAssistant.Chat {\n'+match.group()+'\n}',encoding='utf-8')
options=snapshot('src/VS2017.AIAssistant/Views/AssistantControl.Options.cs')
experience=snapshot('src/VS2017.AIAssistant/Views/AssistantControl.Experience.cs')
assert 'new ConversationCollaborationStore(workspaceStore.RootPath, () => !Dispatcher.CheckAccess())' in experience
assert re.search(r'private void DisposeExperience\(\)\n        \{\n            StopAgentIdentityRetry\(\);',experience)
methods=[]
for name in ('RefreshAgentIdentity','ScheduleAgentIdentityRetry','AgentIdentityRetryTick','StopAgentIdentityRetry'):
    match=re.search(r'^        private [^\n]*\b'+name+r'\([^\n]*\)\n',options,re.M); assert match,name
    end=options.index('\n        }',match.end())+len('\n        }')
    methods.append(options[match.start():end])
(output/'AgentUi.actual.cs').write_text('using System; using VS2017.AIAssistant.Chat;\nnamespace Task069HangFixture { public partial class AssistantControl {\n'+'\n'.join(methods)+'\n}}',encoding='utf-8')
(output/'HangChecks.cs').write_text(snapshot('tests/task069hang/HangChecks.cs'),encoding='utf-8')
(output/'source-hashes.json').write_text(json.dumps(hashes,indent=2),encoding='utf-8')
print(str(output))
