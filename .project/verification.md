# 검증 기록 · 2026-10-05

## 자동 검증

`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test.ps1`

| 영역 | 결과 |
|---|---|
| Framework C# 빌드 | 경고를 오류로 처리하고 성공, 다운로드 없음 |
| Core / 실제 프로세스 통합 | 73 passed, 0 failed |
| WPF UI 동작·접근성·위치 | 63 passed, 0 failed |
| CLI | JSON 파싱 및 잘못된 명령의 exit 1 확인 |
| 실제 앱 시작 | 시스템 트레이 생성, HWND 생성, 네이티브 스캔 성공 |
| 전역 단축키 | 이 환경에서 Ctrl+Alt+P 등록 성공 |

통합 테스트는 임의 포트의 IPv4·IPv6 TCP 서버를 직접 생성한다. 같은 PID의 여러 포트 그룹화, working set, CPU 샘플, 연결 수, PID 생성 시각 불일치 거부, 보호/오래된 스냅샷 거부, 선택 서버 종료, 다른 서버 유지, 종료 후 리스너 제거를 확인한다. 실제 사용자 서버에는 종료 테스트를 하지 않는다.

UI 테스트는 투명한 별도 데모 패널에서 실제 WPF 컨트롤·키 이벤트·UI Automation peer를 사용한다. 하단 고정, 저장된 물리 좌표 복원, 모니터 분리 보정, 차트 키보드 탐색, 포커스 유지, 링크·정리·설정·pin을 검증한다. 데모에서는 외부 실행과 실제 프로세스 종료 없음.

연결된 DISPLAY1·DISPLAY2·DISPLAY7·DISPLAY6의 4개 모니터에 실제 테스트 창을 배치해 각각의 작업 영역 안에 들어오는 것을 확인. 첫 실행의 주 모니터 선택 및 저장된 위치의 트레이 재열기 우선 적용 확인. 수동 드래그의 체감 및 모든 혼합 배율 조합의 시각 검수는 별도.

실앱 점검은 별도 설정 경로, 알림 Off, 자동 정리 Off로 실행하고 즉시 종료한다. 결과: `artifacts/live-smoke.txt`.

## 시각 검증

`dist/WhatThePort.exe --snapshot artifacts/screenshots`

`list.png`, `detail.png`, `cleanup.png`, `settings.png`, `links.png`, `empty.png`를 150% 해상도로 생성했다. 목록·상세·정리·설정·링크·빈 화면을 검토했다. 기본 밝은 스크롤바/선택 박스를 다크 스타일로 교체하고 입력칸 높이를 조정했다.

## 패키지

`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package.ps1`

`artifacts/WhatThePort-Windows-x64.zip`은 앱·접근성 설정·Core DLL·CLI·폰트·라이선스·README·문서·이미지 포함. 테스트 실행 파일·원본 ZIP 제외. 온라인 배포 미실시.

## 웹·보안 검수

- `node scripts/check-site.mjs`: 87개 통과. 홈·가이드·404의 320/390/768/1440px, 키보드 메뉴·탭, 접근성 트리, 내부 파일·앵커·ZIP, 404/405/403 응답 확인.
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/security-audit.ps1`: 5개 발견 사항의 evidence 6개 확인. High 2 / Medium 2 / Low 1. 수정 완료나 보안 인증을 의미하지 않음.
- 상세 결과: `docs/accessibility-audit.md`, `docs/security-audit.md`.

## 수동 확인이 필요한 항목

## 2026-10-05 추가 수정 검증

- 한국어 기본값·English 전환 저장, 헤더·트레이·입력·차트의 접근성 이름 번역.
- wmux·cagent·Docker·컨테이너 도구 보호 및 선택 불가 사유 표시.
- 서버 본체 성공/하위 실패를 구분한 일부 종료 결과. 실패 조합은 주입한 테스트 종료 함수로 검증; 실제 종료 통합 테스트는 생성한 fixture만 대상.
- 목록·정리 화면 열 좌표 일치, 제목/포트/메모리 행 정렬, 32px 헤더 버튼, 포커스 테두리의 레이아웃 영향 제거.
- `scripts/test.ps1`: Core 88 / UI 77 / CLI 2, live smoke 통과. 연결된 4개 모니터 작업 영역 검증.
- 한국어·영어 각각 6개 화면 캡처. 한국어 캡처를 README와 사이트에 반영.
- 사이트 `site/dist` → `docs` 이동. `.nojekyll`, 추적 가능한 ZIP·SHA-256, 프로젝트 경로의 중첩 404 복귀 적용.
- `node scripts/check-site.mjs`: 90개 통과. 320/390/768/1440px, 키보드·접근성 트리·링크·ZIP 무결성 확인.
- 앱 ZIP에서 사이트·사이트 다운로드 제외. 자기 포함 및 반복 빌드 용량 증가 방지.
- `.git/index.lock` 생성 권한 거부로 이번 변경의 커밋·푸시 미완료. 기능별 스크립트 갱신. Pages 설정·게시 미실행.

## 남은 환경 확인

- 사용자의 실제 Codex/Claude 로그인 세션 및 Vercel 주소로 재개/브라우저 이동. 명령 구성·유효성·데모 격리는 테스트했다.
- 실제 Windows 알림 배너 수신 여부(집중 지원·Windows 설정에 영향).
- 장시간 자동 정리 실운영. 후보/보호/관측시간/스캔 신선도 정책과 실제 종료 동작은 각각 검증했다.
- 다른 Windows 버전, ARM64, 다중 모니터·혼합 DPI 환경. 이번 빌드는 x64 환경에서 확인했다.
- 사용자 폴더 설치 및 로그인 시작 등록. 스크립트만 제공하며 이 세션에서는 실행하지 않았다.
