# README 삽입 문안 / TASK-027

아래 블록은 저장소 root README에 넣는 경로를 기준으로 작성했다. TASK-024가 정식 화면과 대조한 뒤 필요한 장면만 선택한다. 검토본 단계에서는 “지원 완료” 문장으로 바꾸지 않는다. 이 폴더의 gallery.html에서 모든 GIF를 미리 볼 수 있다.

```markdown
## 그림으로 살펴보는 사용 흐름

아래는 사용 순서를 설명하는 그림입니다. 실제 제품을 촬영한 화면이 아니며 배포 버전의 화면과 다를 수 있습니다. [단계별 안내](docs/visual-guide-orig3/화면안내_보완가이드.md)와 [정지·재생을 선택하는 갤러리](docs/visual-guide-orig3/gallery.html)도 함께 보세요.

**Markdown과 링크** · 원문을 작성하고 미리보기를 읽은 뒤, 이동할 대상을 확인합니다.

![Markdown과 링크 - 설명용 예시](docs/visual-guide-orig3/images/01-markdown.gif)

**접이식 설명** · 요약부터 읽고 필요한 그림과 설명만 펼칩니다.

![접이식 설명 - 설명용 예시](docs/visual-guide-orig3/images/02-help.gif)

**스크롤과 돌아가기** · 긴 본문을 읽고 우측 상단에서 안내 목록으로 돌아갑니다.

![스크롤과 돌아가기 - 설명용 예시](docs/visual-guide-orig3/images/03-navigation.gif)

**설치와 로그인** · CLI 설치, 공식 인증, 상태 확인, 실제 첫 요청을 따로 확인합니다.

![설치와 로그인 - 설명용 예시](docs/visual-guide-orig3/images/04-account.gif)

**자동·Light·Dark** · 작업 환경에 맞는 테마를 선택하고 메뉴와 설명의 가독성을 확인합니다.

![테마 선택 - 설명용 예시](docs/visual-guide-orig3/images/05-theme.gif)

**좁은 화면의 대화 목록** · 목록과 뒤의 대화를 구분하고 선택한 대화를 이어갑니다.

![대화 목록 - 설명용 예시](docs/visual-guide-orig3/images/06-history.gif)
```

정지 그림은 각 GIF와 같은 이름의 `-steps.png`를 사용한다. 앱 내부 고정 5개 키에 이 6개를 그대로 넣지 말고, 실제로 필요한 안내부터 자원 이름·등록·크기·누락 대안을 정해 적용한다. 미리보기만을 위해 CLI 설치나 계정 로그인을 실행할 필요는 없다.
