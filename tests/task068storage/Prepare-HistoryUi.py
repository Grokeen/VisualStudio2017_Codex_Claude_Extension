# TASK068 / CodexOrig / 2026-10-20: extract exact product method substrings;
# preserve source hashes and never overwrite prior fixture evidence.
from pathlib import Path
import hashlib,json,re,sys

repo=Path(__file__).resolve().parents[2]
output=Path(sys.argv[1])
if output.exists() and any(output.iterdir()):
    raise FileExistsError('Existing history UI evidence must not be overwritten.')
output.mkdir(parents=True,exist_ok=True)
hashes=[]
def actual(relative):
    path=repo/relative
    raw=path.read_bytes(); checksum=hashlib.sha256(raw).hexdigest()
    (output/(path.name+'.original')).write_bytes(raw)
    after=hashlib.sha256(path.read_bytes()).hexdigest()
    hashes.append(dict(Path=str(path),Snapshot=checksum,After=after,Stable=checksum==after))
    assert checksum==after
    return raw.decode('utf-8-sig').replace('\r\n','\n')
chat=actual('src/VS2017.AIAssistant/Views/AssistantControl.Chat.cs')
viewport=actual('src/VS2017.AIAssistant/Views/AssistantControl.Viewport.cs')
workspace=actual('src/VS2017.AIAssistant/Chat/ChatWorkspace.cs')
for name in ('MarkdownWorkspaceStore.cs','MarkdownWorkspaceStore.Committed.cs'):
    text=actual('src/VS2017.AIAssistant/Chat/'+name)
    (output/name).write_text(text,encoding='utf-8')
classes=[]
for name,nextname in [('ChatMessage','ChatSession'),('ChatSession','ChatRequestContext')]:
    match=re.search(r'    public sealed class '+name+r'\n.*?(?=    /// -+\n    /// <summary>\n    /// name         : '+nextname+')',workspace,re.S)
    assert match, name
    classes.append(match.group())
(output/'ChatModels.actual.cs').write_text('using System;\nusing System.Linq;\nusing System.Collections.ObjectModel;\nnamespace VS2017.AIAssistant.Chat {\n'+''.join(classes)+'\n}\n',encoding='utf-8')
methods=[]
for text,name in [(chat,x) for x in ('BindVisibleConversation','VisibleConversationChanged','PrependOlderConversationMessagesAsync','OwnsOlderConversationLoad','CancelOlderConversationLoad')]+[(viewport,x) for x in ('TryPrependConversationPage','CaptureConversationPrependAnchor','CompleteConversationPrependAsync','ArrangeConversationPrepend','FinishConversationPrepend','CancelConversationPrepend','VerifyConversationHistoryAccess','DisposeConversationViewport')]:
    match=re.search(r'^        private [^\n]*\b'+name+r'\([^\n]*\)\n',text,re.M)
    assert match, name
    start=match.start(); body=match.end()
    if text[body:text.index('\n',body)].strip().startswith('{ ') and text[body:text.index('\n',body)].strip().endswith('}'):
        end=text.index('\n',body)
    else:
        end=text.index('\n        }',body)+len('\n        }')
    methods.append(text[start:end]+'\n')
prefix='using System;\nusing System.Collections.Generic;\nusing System.Collections.ObjectModel;\nusing System.Collections.Specialized;\nusing System.IO;\nusing System.Linq;\nusing System.Threading;\nusing System.Threading.Tasks;\nusing System.Windows;\nusing System.Windows.Controls;\nusing System.Windows.Threading;\nusing VS2017.AIAssistant.Chat;\n'
(output/'HistoryUiMethods.actual.cs').write_text(prefix+'namespace VS2017.AIAssistant.Views { public partial class AssistantControl {\n'+''.join(methods)+'\n}}\n',encoding='utf-8')
(output/'source-hashes.json').write_text(json.dumps(hashes,indent=2),encoding='utf-8')
print('Extracted actual history methods: '+str(len(methods)))
