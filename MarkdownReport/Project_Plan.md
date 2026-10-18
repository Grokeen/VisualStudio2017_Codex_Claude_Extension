

# Project_Plan


# VS2017 AI Assistant — 프로젝트 개발 명세서

> 문서 버전: v1.1\
> 작성일: 2026-10-17  
> 프로젝트 상태: v0.5.0 Markdown 대화·공통 규칙·블랙 UI·프로젝트 작업·테스트 실행기 구현 및 실제 VS2017/Codex 검증 완료, GitHub 배포 대기\
> 개발 언어: C# / WPF  
> 대상 IDE: Microsoft Visual Studio 2017 (15.x)  
> AI Provider: OpenAI Codex / Anthropic Claude Code

> 2026-10-18 요구사항 변경 (Role.md, CodexCode 반영): 확장에서 실행한 Codex/Claude가 소스를 수정할 수 있어야 한다. 초기 MVP에도 현재 파일 수정안 생성·비교·사용자 승인·편집기 적용·별도 저장·복구를 포함한다. 다중 파일 변경과 무승인 자동 적용은 후속 범위로 둔다. 구현·자동 검사 결과와 실제 IDE/AI 계정 검증 결과를 구분한다.

추가 Role 프로젝트 3~8번 및 기본 규칙 7번 (2026-10-18, TASK-008/009):

- [x] 문서 탭/도킹/분리로 이동 가능한 Chat 및 마지막 소스 문서 추적
- [x] Codex/Claude 탭, Provider별 대화·작성 중 입력·첨부 분리
- [x] PNG/JPEG·텍스트 파일 드롭/붙여넣기/선택 및 목록 확인·제거
- [x] 여러 차례 요청/응답 누적과 최근 대화 컨텍스트 전달
- [x] instruction·AI별 경로/모델·글자 크기 저장·복원 및 공식 CLI 계정 연결/확인
- [x] 사용자 소개·상세 사용법 중심 README 및 사용 안내 개편
- [x] 편집/Chat71·Foundation33·호환성44, 고정 패키지 실제 IDE35 및 독립 실제 Codex 멀티모달 검증

추가 Role 프로젝트8~11번 (2026-10-18, TASK-010/011):

- [x] 상단/우클릭 설정 진입 및 화면 중앙 팝업, 저장/취소 흐름
- [x] Chat·설정·사용 안내·메뉴의 일관된 VS 테마와 버튼 아이콘
- [x] 첫 사용 네 단계 안내와 다시 보지 않기 저장, 수동 재열기
- [x] 텍스트 편집 메뉴·Ctrl+V·기존 설정 보존
- [x] 회귀163개, 실제 VS2017/Codex50개, 독립 사용자 여정 검증

추가 Role 프로젝트2·4·5·7·8·9·10·12·13 및 대화 화면 요청 (2026-10-18, TASK-012/013):

- [x] Claude 첫 탭·공식 제공자 로고·반투명 배경, 일관된 블랙 계열 컨트롤
- [x] 상세 모델·생각 모드 실제 CLI 전달, Enter 전송·Ctrl+Enter 줄바꿈·IME 처리
- [x] 이미지 미리보기·hover 제거, 중앙 알림과 팝업·버튼·생각 중 애니메이션
- [x] 변경 파일 카드의 추가/삭제 줄 수·실제 IDE 소스 이동·검토/승인/저장/복구
- [x] 전역 instructions.md 및 대화별 front matter Markdown 자동 저장·복원
- [x] AI별 여러 대화·목록 토글·질문 제목·이름 변경·닫기/명시적 재열기
- [x] 저장 실패 시 대화 전환 중단·초안 보존, 설정 JSON/Markdown 실패 복구
- [x] SQL Server 스키마 참조와 빈 SQL 파일의 쿼리 작성·승인 저장 흐름
- [x] 권한·명령 확인 후 Git/SVN/TFVC 작업과 명시적 네이티브 배포 실행
- [x] 부채꼴 설정/도움말·그림형 CLI 설치 안내·확장되는 공통 규칙 편집란
- [x] 오른쪽 사용자 말풍선·왼쪽 AI 본문·실제 대화 생성 시각/측정 작업시간
- [x] Debug/Release 빌드 경고·오류0, 최종 저장52·설정37·프로젝트 작업48·확장 UI56 검증
- [x] 추가 대화 화면 최종 사본 편집92·Foundation33·호환성44·실제 VS2017/Codex58 PASS
- [x] 최상위 VisualStudio_AIChat_ExtentionTest.exe·재현 소스/빌드 스크립트, 전체8묶음 및 독립9개 PASS
- [ ] 작업자 종료 후 GitHub 배포 및 원격 패키지 해시 확정

현재 적용 범위는 VS2017 15.9이다. 대화는 프로젝트와 무관한 사용자별 LocalAppData Markdown 공간에 저장한다. 실제 Claude 계정과 VS2019/2022 패키지 지원, 선택 코드 추출, 다중 파일 자동 수정은 후속 범위다. SQL Server는 메타데이터만 읽으며 작성한 SQL을 DB에서 실행하지 않는다. 원격 SVN/TFVC·운영 DB·운영 배포 연결 검증은 남아 있다. Role의 tns는 TFS/TFVC로 해석했다. CLI의 직접 쓰기/셸 도구는 계속 제한하고, 소스는 한 파일의 수정안 승인·저장으로 적용하며 프로젝트 명령은 별도 검토 창에서 실행한다.

추가 요구 진행 체크리스트 (2026-10-18, TASK-004):

- [x] 단일 파일 수정안 데이터 및 원본·편집기·디스크 충돌 검사 구현
- [x] Codex/Claude CLI 어댑터 및 비교·승인·적용·저장·복구 UI 구현
- [x] VS2017 표준 Debug/Release 빌드 및 자동 회귀 검증
- [x] 실제 Codex 계정으로 합성 코드의 정확한 수정안 반환 확인
- [x] 실제 VS2017 EnvDTE 적용·저장·Undo 통합 검증 (TASK-005, 실제 Codex 포함 25/25 PASS)
- [ ] 실제 Claude 계정 응답 검증
- [ ] VS2019/VS2022 패키지 지원 및 IDE별 검증

---

## 1. 프로젝트 개요

### 1.1 프로젝트명

VS2017 AI Assistant

### 1.2 개발 배경

Visual Studio 2017은 최신 AI 코딩 도구의 공식 IDE 확장 지원이 제한적이다.

기존 개발 환경과 레거시 프로젝트를 유지하면서 Codex 및 Claude Code의 코드 분석, 생성, 수정 기능을 활용하기 위해 독립적인 VSIX 확장 프로그램을 개발한다.

### 1.3 최종 목표

Visual Studio 2017 내부에서 AI 코딩 어시스턴트를 직접 사용할 수 있도록 한다.

- IDE 내부에 AI 채팅 패널 제공
- Codex와 Claude Code 선택 및 전환
- 현재 솔루션과 프로젝트 구조 분석
- 코드 설명, 생성, 리팩터링 및 오류 분석
- 기존 파일 수정 및 변경 내용 비교
- 사용자의 승인에 따른 코드 변경 적용
- AI 대화 기록과 작업 세션 관리
- 기존 .NET Framework 프로젝트와의 호환성 유지

### 1.4 개발 원칙

1. Visual Studio 2017을 업그레이드하지 않는다.
2. AI 실행 환경과 기존 개발 프로젝트를 분리한다.
3. AI가 제안한 변경 사항은 사용자가 확인할 수 있어야 한다.
4. 기존 프로젝트의 프레임워크와 언어 버전을 준수한다.
5. Codex와 Claude를 공통 인터페이스로 연결한다.
6. AI 호출로 Visual Studio의 UI가 멈추지 않아야 한다.
7. 확장 프로그램 오류가 기존 솔루션이나 소스 코드를 손상시키지 않도록 한다.

---

## 2. 개발 환경 및 기술 스택

| 구성 요소 | 기술 |
|---|---|
| 개발 IDE | Visual Studio 2017 |
| 확장 프로그램 | VSIX / Legacy Visual Studio SDK |
| 개발 언어 | C# |
| UI | WPF / XAML |
| IDE 통합 | EnvDTE / Visual Studio SDK |
| AI 통신 | Process / Standard Input·Output |
| Codex | Codex CLI / app-server |
| Claude | Claude Code CLI |
| 데이터 형식 | JSON |
| 비동기 처리 | Task / async / await |
| 설정 저장 | JSON |
| 로그 | 로컬 파일 |
| 버전 관리 | Git |

### 호환성 요구사항

- VS2017 15.x에 설치 가능한 VSIX를 생성한다.
- VSIX 매니페스트의 지원 버전을 명시한다.
- 확장 개발 프로젝트는 VS2017과 호환되는 .NET Framework 및 C# 버전을 사용한다.
- 최신 VisualStudio.Extensibility SDK에 의존하지 않는다.
- 기존 .NET Framework 4.6.2 프로젝트를 분석하고 수정할 수 있어야 한다.
- AI가 생성하는 코드는 대상 프로젝트의 C# 언어 버전을 준수한다.

---

## 3. 전체 아키텍처

```text
Visual Studio 2017
│
├── VS2017 AI Assistant (VSIX)
│   │
│   ├── AI Tool Window (WPF)
│   │   ├── Chat UI
│   │   ├── Provider Selector
│   │   ├── Context Viewer
│   │   ├── Diff Viewer
│   │   └── Settings
│   │
│   ├── IDE Integration
│   │   ├── Solution Reader
│   │   ├── Project Explorer
│   │   ├── Active Document
│   │   ├── Selected Code
│   │   └── Error List Reader
│   │
│   ├── AI Provider Manager
│   │   ├── Codex Provider
│   │   │   └── Codex app-server
│   │   │
│   │   └── Claude Provider
│   │       └── Claude Code CLI
│   │
│   ├── Workspace Manager
│   │   ├── File Reader
│   │   ├── File Change Detector
│   │   └── Diff / Apply Manager
│   │
│   └── Session Manager
│       ├── Conversation History
│       ├── Task State
│       └── Logging
│
└── Existing Solution
    ├── *.sln
    ├── *.csproj
    ├── *.cs
    ├── *.xaml
    └── 기타 프로젝트 파일
```

### 3.1 아키텍처 핵심

VSIX는 IDE 통합과 사용자 인터페이스만 담당한다.

AI 실행과 추론은 외부 Codex 또는 Claude 프로세스에서 수행한다.

AI Provider마다 서로 다른 통신 방식을 사용하므로, 공통 인터페이스를 정의하여 기능을 통일한다.

### 3.2 AI Provider 인터페이스 예시

```csharp
public interface IAiProvider
{
    string Name { get; }

    Task<string> SendMessageAsync(
        string message,
        CancellationToken cancellationToken);

    Task StopAsync();
}
```

실제 구현에서는 세션 생성, 스트리밍 응답, 작업 이벤트, 승인 요청, 오류 처리를 추가한다.

Codex와 Claude의 통신 프로토콜은 다르므로 내부 어댑터에서 변환해야 한다.

---

## 4. 주요 기능 명세

### F-001. AI 채팅 패널

Visual Studio 내부에 독립적인 AI 도구 창을 제공한다.

요구사항:

- 채팅 메시지 입력 및 전송
- AI 응답 출력
- 긴 응답 스크롤
- 코드 블록 표시
- 코드 복사
- 작업 중지
- 오류 메시지 출력
- UI 비동기 처리

완료 기준:

- VS2017 내부에서 질문을 전송할 수 있다.
- Codex와 Claude의 응답을 정상 표시한다.
- 요청 처리 중에도 IDE가 멈추지 않는다.

### F-002. AI Provider 전환

사용자가 Codex와 Claude Code를 선택할 수 있도록 한다.

요구사항:

- Provider 선택 메뉴
- 설치 여부 확인
- 실행 파일 경로 설정
- 버전 확인
- 인증 상태 및 실행 오류 안내
- Provider별 세션 관리

완료 기준:

- IDE를 재시작하지 않고 Provider를 전환할 수 있다.
- Provider별 연결 실패가 다른 Provider 사용을 방해하지 않는다.

### F-003. 코드 컨텍스트 전달

현재 편집 중인 소스 정보를 AI에 전달한다.

지원 범위:

- 선택한 코드
- 현재 파일
- 현재 프로젝트
- 전체 솔루션
- 사용자 지정 파일

전달 정보:

```text
Solution: MySolution.sln
Project: MyProject.csproj
Framework: .NET Framework 4.6.2
Language: C#
File: MainWindow.xaml.cs
Selection: Line 20 ~ 45

User Prompt:
선택한 코드의 오류 원인을 분석해줘.
```

완료 기준:

- 선택한 코드와 파일 경로를 정확하게 전달한다.
- 저장되지 않은 편집 내용도 구분하여 처리한다.
- 프로젝트의 프레임워크 정보를 전달할 수 있다.

### F-004. 코드 분석 및 설명

선택한 코드를 대상으로 다음 작업을 수행한다.

- 코드 흐름 설명
- 클래스와 메서드 관계 분석
- 오류 원인 탐색
- 성능 문제 분석
- 리팩터링 제안
- 한국어 주석 생성
- 기존 코드의 개선 방향 제안

완료 기준:

- 기존 파일을 수정하지 않고 분석 결과를 제공한다.
- 분석에 사용된 파일과 범위를 확인할 수 있다.

### F-005. 솔루션 탐색

AI가 현재 솔루션 구조를 파악할 수 있도록 한다.

요구사항:

- 솔루션 파일 목록 확인
- 프로젝트별 파일 목록 확인
- 클래스 및 주요 파일 검색
- 프로젝트 참조 관계 확인
- 특정 문자열 검색
- 여러 파일 간 호출 관계 분석

대규모 솔루션에서는 파일 전체를 한 번에 전달하지 않는다.

관련 파일을 검색하여 필요한 부분만 제공하는 방식을 기본으로 한다.

### F-006. 코드 수정 및 적용

AI가 제안한 수정 사항을 IDE에서 확인하고 적용한다.

요구사항:

1. 수정 요청 수신
2. AI 수정안 생성
3. 기존 파일과 수정안 비교
4. 변경 파일 및 변경 위치 표시
5. 사용자 승인
6. 소스 파일 수정
7. 저장 및 결과 표시

안전 조건:

- 기본 모드는 읽기 전용으로 시작한다.
- 자동 수정은 별도로 허용해야 한다.
- 파일 수정 전에 변경 사항을 확인한다.
- 수정 전 파일 내용을 백업하거나 복구 가능한 상태를 유지한다.
- 사용자의 미저장 변경 내용을 덮어쓰지 않는다.
- Git Diff를 통해 변경 내용을 추적할 수 있어야 한다.

완료 기준:

- 승인하지 않은 변경이 적용되지 않는다.
- 여러 파일 변경도 파일별로 검토할 수 있다.
- 변경 충돌 발생 시 자동으로 덮어쓰지 않는다.

### F-007. 빌드 오류 분석

Visual Studio의 빌드 오류를 AI에 전달한다.

요구사항:

- Error List 정보 추출
- 오류 코드 및 메시지 전달
- 관련 파일과 위치 전달
- 원인 및 수정 방법 제안
- 수정 코드 미리보기

완료 기준:

- 선택한 오류의 파일, 줄 번호, 메시지를 정확히 전달한다.
- 오류가 발생한 파일과 관련 코드를 함께 분석한다.

### F-008. 세션 및 기록 관리

요구사항:

- AI 대화 세션 생성
- 세션 목록 표시
- 이전 대화 조회
- Provider별 세션 분리
- 대화 기록 삭제
- 프로젝트별 기록 구분

저장 대상:

- 대화 내용
- 요청 시간
- Provider
- 관련 파일 경로
- 실행 결과
- 오류 및 진단 로그

API 키, 인증 토큰, 비밀번호 등 민감 정보는 기록하지 않는다.

---

## 5. 사용자 인터페이스

### 5.1 메인 화면 구조

```text
┌───────────────────────────────────────┐
│ VS2017 AI Assistant                   │
├───────────────────────────────────────┤
│ Provider: [ Codex ▼ ]                 │
│ Session : [ New Chat ▼ ]              │
├───────────────────────────────────────┤
│                                       │
│ User                                  │
│ 선택한 메서드의 오류를 분석해줘.       │
│                                       │
│ Assistant                             │
│ 해당 메서드에서 NullReferenceException│
│ 발생 가능성이 있습니다.               │
│                                       │
│ [코드 보기] [수정안 비교]              │
│                                       │
├───────────────────────────────────────┤
│ Context                               │
│ [✓] Selected Code                     │
│ [ ] Current File                      │
│ [ ] Project                           │
│ [ ] Solution                          │
├───────────────────────────────────────┤
│ 메시지를 입력하세요...                 │
│                                       │
│                      [중지] [전송]     │
└───────────────────────────────────────┘
```

### 5.2 UI 설계 원칙

- Visual Studio의 기본 테마와 최대한 조화롭게 구성
- 좁은 도구 창에서도 사용 가능한 레이아웃
- 긴 코드 블록 가독성 확보
- 비동기 응답 표시
- 주요 명령에 단축키 연결 가능
- 불필요한 애니메이션 최소화

---

## 6. 프로젝트 폴더 구조

```text
VS2017-AI-Assistant/
│
├── src/
│   ├── VS2017.AIAssistant/
│   │   ├── Commands/
│   │   ├── ToolWindows/
│   │   ├── Views/
│   │   ├── ViewModels/
│   │   ├── Services/
│   │   ├── Models/
│   │   ├── Resources/
│   │   └── source.extension.vsixmanifest
│   │
│   ├── AIAssistant.Core/
│   │   ├── Interfaces/
│   │   ├── Providers/
│   │   │   ├── CodexProvider.cs
│   │   │   └── ClaudeProvider.cs
│   │   ├── Sessions/
│   │   ├── Workspaces/
│   │   └── Models/
│   │
│   └── AIAssistant.Infrastructure/
│       ├── Process/
│       ├── Configuration/
│       ├── Logging/
│       └── FileSystem/
│
├── tests/
│   ├── AIAssistant.Core.Tests/
│   ├── AIAssistant.Integration.Tests/
│   └── TestSolutions/
│
├── docs/
│   ├── ARCHITECTURE.md
│   ├── FEATURES.md
│   ├── DEVELOPMENT.md
│   ├── TESTING.md
│   └── CHANGELOG.md
│
├── README.md
└── .gitignore
```

처음에는 지나치게 많은 프로젝트로 분리하지 않아도 된다.

위 구조는 최종적인 목표 구조이며, MVP 단계에서는 VSIX 프로젝트와 Core 라이브러리를 중심으로 시작한다.

---

## 7. 개발 단계 및 작업 계획

### Phase 1. 개발 환경 및 VSIX 기초

목표: VS2017에서 정상 동작하는 확장 프로그램 구축

작업:

- [ ] Visual Studio 2017 확장 개발 워크로드 설치
- [ ] VSIX 프로젝트 생성
- [ ] Visual Studio SDK 호환성 확인
- [ ] WPF Tool Window 생성
- [ ] 도구 메뉴에서 AI 창 열기
- [ ] Experimental Instance 테스트
- [ ] VSIX 빌드 및 설치 테스트

완료 조건:

VS2017에서 AI 도구 창을 정상적으로 열고 닫을 수 있다.

### Phase 2. AI 연결

목표: Codex 및 Claude Code 연결

작업:

- [x] AI Provider 공통 인터페이스 설계
- [x] Codex 설치 및 실행 환경 확인
- [ ] Codex app-server 연결 구현
- [x] Claude Code CLI 실행 구현 (프로토콜 fixture 검증, 실제 계정 별도)
- [x] JSON 응답 파싱
- [x] 비동기 응답 표시
- [x] 작업 중지 및 프로세스 정리
- [x] Provider 선택 기능 구현

완료 조건:

동일한 WPF 채팅 창에서 Codex 및 Claude에 각각 질문하고 답변을 받을 수 있다.

### Phase 3. IDE 컨텍스트 통합

목표: Visual Studio의 코드 정보를 AI가 사용할 수 있도록 구현

작업:

- [ ] 현재 솔루션 경로 확인
- [x] 현재 프로젝트 정보 확인
- [x] 활성 문서 내용 가져오기
- [ ] 선택한 코드 추출
- [x] 여러 파일 첨부 (이미지/텍스트 합계4개)
- [x] 프레임워크 정보 확인
- [x] AI 요청에 컨텍스트 전달

완료 조건:

사용자가 선택한 코드와 프로젝트 조건에 맞는 응답을 생성할 수 있다.

### Phase 4. 코드 수정 기능

목표: AI가 제안한 코드를 안전하게 적용

작업:

- [x] 코드 수정안 데이터 구조 설계
- [x] 파일 변경 감지
- [x] Diff 비교
- [x] 변경 적용 승인 UI
- [ ] 선택적 코드 수정
- [x] 파일 충돌 검사
- [x] 수정 취소 및 복구 기능
- [ ] Git 변경 사항 확인 연계

완료 조건:

AI 수정안을 확인하고 사용자가 승인한 변경만 적용할 수 있다.

### Phase 5. 고급 기능

목표: 실제 개발 생산성 향상

작업:

- [ ] 빌드 오류 자동 수집
- [ ] 오류 관련 코드 분석
- [ ] 솔루션 내 파일 검색
- [ ] 여러 파일 리팩터링 지원
- [ ] 대화 기록 관리
- [ ] 프롬프트 템플릿 기능
- [x] 설정 UI
- [ ] 로그 및 오류 진단 기능

완료 조건:

VS2017 내부에서 코드 분석, 수정, 오류 해결까지 연결된 작업 흐름을 수행할 수 있다.

### Phase 6. 안정화 및 배포

작업:

- [x] VS2017 호환성 테스트
- [x] .NET Framework 4.6.2 테스트 프로젝트 검증
- [ ] 대규모 솔루션 테스트
- [ ] 프로세스 종료 및 재시작 테스트
- [ ] 장시간 사용 안정성 테스트
- [ ] 설치 및 제거 테스트
- [ ] 사용자 설정 유지 테스트
- [ ] Release VSIX 패키징
- [ ] README 및 설치 가이드 작성

---

## 8. 보안 및 권한 관리

### 기본 원칙

AI에는 사용자가 허용한 범위만 제공한다.

로컬 파일 접근과 코드 변경, 명령 실행은 서로 구분하여 관리한다.

### 권한 구분

| 작업 | 기본 정책 |
|---|---|
| 선택 코드 분석 | 사용자 요청 시 허용 |
| 현재 파일 분석 | 사용자 요청 시 허용 |
| 전체 솔루션 분석 | 범위 확인 후 허용 |
| 코드 변경 | 변경 내용 승인 후 적용 |
| 파일 삭제 | 별도 승인 필수 |
| 외부 명령 실행 | 별도 승인 필수 |
| 외부 네트워크 사용 | 조직 정책 및 사용자 권한 확인 |
| 민감 정보 전송 | 기본 차단 또는 사전 확인 |

### 필수 고려사항

- 회사 내부망과 외부망의 보안 정책 준수
- 소스 코드 외부 전송 가능 여부 확인
- 인증 토큰과 API 키의 평문 저장 방지
- AI 프로세스 실행 권한 최소화
- 임의 명령 실행에 대한 통제
- 로그에 개인정보 및 기밀 정보 남기지 않기
- 허용된 작업 디렉터리 범위 제한

내부망 프로젝트에서 사용할 경우 AI 실행이 가능한지 여부와 소스 코드의 외부 전송 허용 여부를 별도로 검토한다.

---

## 9. 테스트 전략

### 9.1 단위 테스트

- Provider 인터페이스
- JSON 파싱
- 설정 파일 처리
- 세션 상태 관리
- 파일 변경 비교
- 취소 및 오류 처리

### 9.2 통합 테스트

- Codex 프로세스 연결
- Claude 프로세스 연결
- 요청 및 응답 스트리밍
- 프로세스 강제 종료 후 복구
- 파일 분석과 수정 제안
- 변경 적용 및 복원

### 9.3 IDE 테스트

- VS2017 실행
- VSIX 로딩
- 메뉴 및 도구 창 표시
- 솔루션 열기와 닫기
- 프로젝트 전환
- 편집기 선택 영역 전달
- 빌드 오류 목록 접근

### 9.4 호환성 테스트

최소 다음 환경을 테스트한다.

- VS2017 15.x
- .NET Framework 4.6.2 C# 프로젝트
- WinForms 프로젝트
- WPF 프로젝트
- 일반 클래스 라이브러리
- 다중 프로젝트 솔루션

### 9.5 실패 시나리오

- Codex 미설치
- Claude Code 미설치
- 로그인 또는 인증 실패
- 네트워크 연결 끊김
- 요청 중 사용자 취소
- AI 응답 형식 오류
- 읽기 전용 파일
- 파일 수정 중 충돌
- VS 종료 중 AI 프로세스 실행
- 장시간 요청으로 인한 타임아웃

---

## 10. 주요 기술 위험 및 대응

| 위험 요소 | 대응 방법 |
|---|---|
| VS2017 SDK 제약 | Legacy VSSDK 사용 |
| CLI 통신 방식 변경 | Provider 어댑터 분리 |
| Codex app-server 프로토콜 변경 | 버전 고정 및 호환성 테스트 |
| Claude Code 출력 형식 변경 | JSON 파서 검증 및 버전 테스트 |
| IDE UI 멈춤 | 비동기 처리 |
| 솔루션 규모 증가 | 필요한 컨텍스트만 선택 |
| AI 수정으로 코드 손상 | 변경 승인 및 복원 |
| 저장되지 않은 코드 충돌 | 편집기 버퍼와 파일 상태 비교 |
| 인증 정보 노출 | 민감 정보 마스킹 및 안전한 저장 |
| 내부망 사용 제한 | 조직 보안 정책 검토 |

---

## 11. 버전별 개발 목표

### v0.1 — VSIX Foundation

- VS2017 확장 설치
- WPF 도구 창
- 기본 채팅 UI
- 설정 화면

### v0.2 — AI Connectivity

- Codex 연결
- Claude Code 연결
- Provider 전환
- AI 응답 표시

### v0.3 — Code Context

- 선택 코드 분석
- 현재 파일 분석
- 프로젝트 컨텍스트
- 솔루션 경로 연동

### v0.4 — Code Editing

- AI 수정안 생성
- Diff 비교
- 변경 승인
- 변경 적용 및 복구

### v0.5 — Developer Tools

- 빌드 오류 분석
- 관련 파일 검색
- 다중 파일 작업
- 대화 및 세션 기록

### v1.0 — Stable Release

- 기능 통합
- 안정성 및 호환성 검증
- 설치 패키지
- 사용자 매뉴얼
- 배포 준비 완료

---

## 12. MVP 개발 범위

첫 번째 개발 목표는 모든 기능을 구현하는 것이 아니다.

우선 아래 6개 기능만 완성한다.

1. Visual Studio 2017에 VSIX 설치
2. AI WPF 도구 창 실행
3. Codex와 Claude Code 선택
4. 질문 전송 및 응답 출력
5. 현재 선택한 코드 AI 전달
6. 오류 및 프로세스 종료 정상 처리

### MVP 완료 판정

다음 시나리오가 성공하면 MVP 개발을 완료한 것으로 본다.

```text
1. Visual Studio 2017 실행

2. C# 솔루션 열기

3. AI Assistant 도구 창 실행

4. 코드 20줄 선택

5. Codex 선택

6. "이 코드의 문제점을 분석해줘" 입력

7. AI 분석 결과 확인

8. Claude Code로 전환

9. 동일 코드에 대한 질문 전송

10. Claude 응답 확인

11. 요청 중지 및 재요청 테스트

12. Visual Studio 정상 종료
```

2026-10-18 추가 Role.md 지시에 따라 이 MVP에도 현재 파일의 AI 수정안 생성·비교·사용자 승인·적용·저장·복구를 포함한다. 기본적으로 승인 전에는 소스를 변경하지 않으며, 무승인 자동 적용과 다중 파일 변경은 후속 범위로 둔다.

---

## 13. 개발 진행 규칙

### 작업 수행

- 한 번에 하나의 주요 기능 단위를 구현한다.
- 작업 전 영향 범위를 확인한다.
- 기존 정상 동작을 보존한다.
- 가능한 범위에서 자동화 테스트를 작성한다.
- 완료한 작업은 체크리스트에 반영한다.

### 코드 품질

- C# 클래스와 메서드의 역할을 명확히 한다.
- 필요한 부분에 자연스러운 한국어 주석을 작성한다.
- 중복 코드를 최소화한다.
- 불필요한 추상화와 과도한 클래스 분리를 피한다.
- 오류 처리를 생략하지 않는다.
- AI Provider별 종속성을 공통 기능과 분리한다.

### 완료 보고 형식

```text
[작업 ID]
TASK-000

[작업명]
Codex Provider 연결

[변경 파일]
- CodexProvider.cs
- ProviderManager.cs

[구현 내용]
- Codex 프로세스 실행
- 응답 메시지 파싱

[테스트 결과]
- 정상 응답: PASS
- 취소 처리: PASS
- 오류 처리: PASS

[미완료]
- 해당 없음

[다음 작업]
Claude Provider 연결
```

---

## 14. 최종 완료 조건

다음 조건을 모두 충족하면 프로젝트 v1.0을 완료한다.

- [ ] Visual Studio 2017에서 안정적으로 실행된다.
- [ ] Codex와 Claude Code를 모두 사용할 수 있다.
- [ ] 코드 및 솔루션 컨텍스트를 전달할 수 있다.
- [ ] AI의 코드 수정안을 검토할 수 있다.
- [ ] 사용자가 승인한 변경만 적용된다.
- [ ] 기존 개발 프로젝트를 손상시키지 않는다.
- [ ] 요청 취소와 오류 복구가 정상 작동한다.
- [ ] 프로젝트별 대화 기록을 관리할 수 있다.
- [ ] .NET Framework 4.6.2 프로젝트에서 검증했다.
- [ ] VSIX 설치 파일과 사용 설명서를 제공한다.

---

## 15. 개발 시작 지시사항

이 프로젝트의 최초 작업은 Phase 1부터 시작한다.

우선 Visual Studio 2017 SDK 및 VSIX 호환성을 확인하고, 빈 WPF 도구 창을 가진 확장 프로그램을 만든다.

이후 Codex 및 Claude CLI 통신을 각각 독립적으로 검증한다.

초기 단계에서 자동 코드 변경, 다중 파일 수정, 고급 에이전트 기능을 한꺼번에 개발하지 않는다.

각 단계가 완료되면 테스트를 수행한 후 다음 단계로 진행한다.

### 최초 작업 명령

> Visual Studio 2017에서 동작하는 VSIX 기반 AI Assistant를 개발한다. 프로젝트 명세서의 Phase 1부터 수행하되 VS2017 호환성을 최우선으로 한다. C#과 WPF를 사용하고, 확장 프로그램이 정상 설치되며 AI 도구 창을 열 수 있는 최소 기능부터 구현한다. 기존 개발 프로젝트는 수정하지 말고 독립적인 솔루션으로 작성한다. 완료 후 변경 파일, 구현 내용, 테스트 결과, 미완료 사항을 보고한다.

---

## 16. 공식 참고 문서

- Visual Studio SDK: https://learn.microsoft.com/en-us/visualstudio/extensibility/
- VSIX Tool Window: https://learn.microsoft.com/en-us/visualstudio/extensibility/creating-an-extension-with-a-tool-window
- OpenAI Codex: https://developers.openai.com/codex/
- Codex app-server: https://developers.openai.com/codex/app-server/
- Claude Code: https://code.claude.com/docs/
- Claude Code CLI: https://code.claude.com/docs/en/cli-reference

---

문서 종료 — VS2017 AI Assistant Project Specification v1.0
