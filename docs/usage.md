# 사용 가이드

[README로 돌아가기](../README.md)

## 프로젝트와 세션 연결

서버 상세 → **Link session & preview** → 폴더·에이전트·세션 ID·미리보기 주소 입력 → **Save links**.

| 항목 | 기준 |
|---|---|
| 프로젝트 폴더 | 서버가 실행된 실제 폴더 |
| 에이전트 | Codex / Claude Code |
| 세션 ID | 로컬 CLI에서 재개 가능한 실제 세션 ID |
| 미리보기 | `https://….vercel.app` 형식의 등록 주소 |
| 연결 키 | 프로젝트 폴더 + 포트; 다른 프로젝트의 포트 재사용 시 이전 링크 승계 방지 |

상세의 **Session: Codex** 표시는 부모 프로세스 기준 연관 정보. 정확한 세션 연결 완료 여부와 별개이며, 재개에는 세션 ID 등록 필요.

재개 전제: 선택한 에이전트 CLI 설치·로그인, 해당 CLI에서 접근 가능한 세션. 데스크톱 전용 세션의 CLI 재개 가능 여부는 사용 도구의 지원 범위에 따라 별도 확인 필요.

### CLI 링크 등록

```powershell
.\dist\wtp.exe link --port 3000 --folder C:\dev\my-app --agent codex --session YOUR_SESSION_ID
.\dist\wtp.exe link --port 5173 --folder C:\dev\other-app --agent claude --session YOUR_SESSION_ID --preview https://my-preview.vercel.app
```

`wtp link` 실행 시 해당 폴더·포트의 링크 레코드 교체. 세션과 미리보기 동시 유지 시 같은 명령에 두 항목 모두 입력.

Vercel API 인증·배포 없음. 등록된 HTTPS 주소의 브라우저 열기만 지원. PowerShell·Windows Terminal 외의 사용자 지정 터미널 명령 미지원.

## 패널 위치

제목을 드래그하면 위치 자동 저장. 숨김·트레이 재열기·앱 재실행 후 복원. Ctrl+Shift+방향키로 키보드 이동. 저장 위치가 없으면 첫 실행은 주 모니터, 트레이 클릭은 해당 모니터 기준. 모니터 연결 해제 시 보이는 작업 영역으로 보정.

초기화: Settings → **Reset panel position**. 핀(◇/◆)은 포커스를 잃었을 때 창 유지 여부이며, 위치 저장과 별개.

## 단축키

| 키 | 동작 |
|---|---|
| Ctrl+Alt+P | 트레이 패널 표시·숨김 |
| Esc | 목록 복귀 / 패널 숨김 |
| Ctrl+, | 설정 |
| Ctrl+Shift+방향키 | 패널 이동·위치 저장 |
| 차트의 ← / → / Home / End | 이전·다음·첫·마지막 표본 |
| Tab / ↑ / ↓ / Enter | 컨트롤·서버 선택 / 상세 열기 |
| C | 목록에서 정리 화면 |
| O | 상세에서 로컬 URL 열기 |
| T | 상세에서 프로젝트 터미널 열기 |
| A | 상세에서 등록된 에이전트 세션 재개 |
| V | 상세에서 등록된 Vercel 미리보기 열기 |

전역 단축키 충돌 시 트레이 아이콘으로 패널 열기. 창의 ×는 숨김, 트레이 우클릭 → **Quit**은 앱 종료.

## 설정

| 항목 | 기본값·동작 |
|---|---|
| 샘플 주기 | 3초; 설정 범위 1–30초 |
| 메모리 경고 | 2 GB |
| 증가량 경고 | 500 MB / 10분; 0은 끄기 |
| CPU 경고 | 전체 논리 코어 대비 80%, 연속 3회 |
| 자동 정리 | Off / Ask / Automatic; 기본 Off |
| idle 기준 | 4시간; 연결 없음·CPU 2% 미만의 연속 관측 |
| 터미널 | PowerShell / Windows Terminal |
| 전체 리스너 | 사용자 정의 실행 파일·일반 앱의 TCP 리스너 포함 |

보호·경고 서버의 자동 정리 제외. 조회 실패·오래된 스냅샷의 자동 정리 중단. 실시간 요청 로그를 읽지 않는 표본 기반 idle 판정.

설정 저장 위치: `%LOCALAPPDATA%\WhatThePort\settings.json`.
프로젝트 링크 위치: 같은 폴더의 `links.json`.
손상된 설정: 자동 정리가 꺼진 기본값 복구 및 화면 안내. 수동 JSON 수정 후 앱 재실행 필요.

## 설치·업데이트·제거

```powershell
# 시작 메뉴 등록
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/install.ps1
# 로그인 시 자동 시작 등록
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/install.ps1 -StartWithWindows
```

| 작업 | 방법 |
|---|---|
| 설치 위치 | `%LOCALAPPDATA%\Programs\WhatThePort` |
| PATH | 변경 없음 |
| 업데이트 | 트레이 → Quit 후 새 빌드·폴더 교체·재설치 |
| 제거 | 앱 종료 후 설치 폴더 및 시작 메뉴·시작프로그램의 `What the Port.lnk` 삭제 |
| 설정 보존 | `%LOCALAPPDATA%\WhatThePort` 폴더 유지 |

미서명 로컬 빌드. 코드 서명·자동 업데이트·온라인 배포 없음.

## 측정·종료 관련 참고

- **CPU** — 전체 논리 코어 대비 비율; 프로세스별 코어 사용률과 차이 가능
- **메모리** — resident working set; 공유 페이지 중복 및 실제 회수량 차이 가능
- **프로젝트 폴더** — 접근 가능한 x64/WOW64 프로세스의 현재 폴더 조회; 실패 시 프로세스 이름 표시
- **에이전트 식별** — 이름 변경·중간 셸에 따른 연관 식별 실패 가능
- **프로세스 종료** — 선택 트리의 즉시 종료; 저장·정리 훅 보장 없음
- **자동 재시작 서버** — 상위 npm 감시 프로세스가 재시작한 경우 원래 터미널에서 중지 필요
- **알림** — Windows 알림 설정·집중 지원에 따른 배너 미표시 가능
