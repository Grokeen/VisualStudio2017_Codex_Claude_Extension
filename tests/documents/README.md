# 문서 첨부 본문 추출 검사

문서 원본을 변경하지 않고 본문을 첨부 참고 텍스트로 추출한다. 이미지·표·차트의 시각 배치를 재현하거나 문서를 편집하는 기능은 포함하지 않는다.

| 형식 | 추출 경로 |
| --- | --- |
| DOCX | `word/document.xml` 본문·문단·표 셀의 텍스트 |
| PPTX | 실제 발표 순서의 슬라이드 텍스트 |
| XLSX | 시트 순서·공유 문자열·인라인 문자열·저장된 셀 값. 수식을 실행하거나 재계산하지 않음 |
| ODT·ODP·ODS | `content.xml`의 문단 텍스트 |
| RTF | Unicode·16진수 코드페이지·문단. OLE·그림·스타일 데이터는 무시 |
| HTML | 정적인 본문. script·style·head 및 태그 속성은 제외 |
| TXT·MD·CSV·TSV·LOG·JSON·XML·YAML | UTF8 또는 BOM UTF16/UTF32 |
| PDF·DOC·PPT·XLS | 해당 PC에 등록된 Windows IFilter. 별도 프로세스에서 읽으며 실제 지원은 필터와 문서 상태에 따라 달라짐 |

PDF·구형 Office 추출은 Office 앱이나 매크로를 실행하지 않는다. Windows PDF 필터 중 `IPersistFile`을 지원하지 않는 구현은 읽기 전용 `IInitializeWithStream`으로 초기화한다. 본문 속성만 요청하고 외부 링크 검색 플래그를 사용하지 않으며 내장 문서 추출을 끈다. 스캔 PDF는 OCR을 수행하지 않으므로 텍스트가 없으면 OCR 후 다시 첨부하라는 안내를 반환한다.

입력은 16MB, 추출 본문은 **UTF8 100KB**까지 허용한다. 한글 문서도 바이트 수로 검사하며 내용을 조용히 잘라 보내지 않는다. ZIP은 2048항목·총 해제 크기 32MB·XML 항목당 4MB·구조 깊이 128로 제한한다. DTD·외부 XML 엔터티를 허용하지 않고 ZIP을 디스크에 풀지 않는다. 발표·시트는 최대 200구역이다. 문서 파싱은 15초 제한을 확인하고, Windows 필터 도우미는 Job Object의 15초 시간 제한·256MB 메모리 제한·프로세스 트리 취소로 격리한다.

API는 `DocumentAttachmentReader.CanRead`, `GetFilter`, `GetFormatSupport`, `Extract(path, cancellationToken)` 및 `ExtractAsync(path, cancellationToken)`이다. UI는 비동기 API를 사용한다. `GetFormatSupport`의 Windows IFilter 표시는 모든 PC에서 성공한다는 뜻이 아니며 추출 시 미설치·암호·손상을 안내한다.

```powershell
# 독립 소스·도우미 검사 (공유 제품 빌드 없음)
powershell -NoProfile -ExecutionPolicy Bypass -File tests/documents/Test-DocumentAttachments.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/documents/Test-DocumentAttachments.ps1 -Platform x86

# 고정된 실제 제품 DLL·패키지 도우미 검사
powershell -NoProfile -ExecutionPolicy Bypass -File tests/documents/Test-DocumentAttachments.ps1 -AssemblyPath <제품DLL> -WorkerPath <VS2017.AIAssistant.DocumentReader.exe> -Platform x86
```

결과는 고유한 `artifacts/dr/<ID>` 폴더에 기록한다. 현재 테스트는 47개이며 실제 Windows 필터로 PDF·DOC·PPT·XLS와 이미지 전용 PDF를 읽는다. 암호 보호 응답과 필터 미설치 오류는 격리된 fixture 도우미로 검증한다. x86 검사 프로세스에 AnyCPU 패키지 도우미를 제공하면 64비트 Windows에서 도우미는 64비트로 실행된다. 도우미 자체도 x86으로 검증하려면 `-WorkerPath` 없이 `-Platform x86`을 사용한다.

제품에는 추가 NuGet 라이브러리가 필요 없다. 문서 도우미는 `src/VS2017.AIAssistant.DocumentReader/VS2017.AIAssistant.DocumentReader.csproj`를 MSBuild 2017로 빌드하고 `.exe`와 `.exe.config`를 제품 DLL 옆과 VSIX에 포함한다. `DocumentFilterWorker.cs`는 도우미 프로젝트에서만 컴파일한다.

구현 근거는 Microsoft의 [IFilter 인터페이스](https://learn.microsoft.com/en-us/windows/win32/api/filter/nn-filter-ifilter), [초기화 속성 요청](https://learn.microsoft.com/en-us/windows/win32/api/filter/nf-filter-ifilter-init), [외부 링크·내장 문서 제어 플래그](https://learn.microsoft.com/en-us/windows/win32/api/filter/ne-filter-ifilter_init), [텍스트 버퍼 계약](https://learn.microsoft.com/en-us/windows/win32/api/filter/nf-filter-ifilter-gettext)이다. 구형 Office fixture의 출처·라이선스는 [fixtures/NOTICE.md](fixtures/NOTICE.md)에 기록했다.
