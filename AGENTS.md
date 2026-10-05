# What the Port · Windows

## 실행 원칙
- 사용자 요청 범위에서 구현·수정·로컬 검증을 끝까지 수행한다.
- 신규 의존성 및 lock 파일 갱신, 외부 쓰기·전송, 배포, 시크릿 열람, 파괴적 Git 작업은 사전 승인이 필요하다.
- Windows 10/11 x64, .NET Framework 4.8, WPF를 대상으로 한다. 기본 빌드는 Windows 내장 C# 컴파일러를 사용하고 NuGet/npm 의존성을 추가하지 않는다.
- 별도 요청 없이 서브에이전트를 사용하지 않는다. 위임 시 Goal / Constraints / Verification / Halt를 명시한다.

## 구현 규칙
- `src/Core`는 UI에 의존하지 않는다. TCP 조회, 프로세스 식별, 정책, 설정을 분리한다.
- 프로세스 식별은 PID + UTC 시작 시각으로 한다. 종료 직전에 같은 핸들에서 생성 시각·사용자·세션을 검증한다.
- 셸, 터미널, 에이전트, 시스템 및 데이터베이스 프로세스는 종료 대상에서 제외한다. 다른 리스너를 가진 하위 트리는 따로 소유한다.
- 자동 정리는 기본 Off. 지속적으로 관측한 idle 서버만 대상이며 경고 중인 서버와 보호 서버는 제외한다. 조회 실패 시 자동 정리를 수행하지 않는다.
- 사용자 환경 변수, 시크릿 파일, 에이전트 대화 로그를 읽지 않는다. 세션은 명시적인 링크 등록만으로 재개한다.
- 실제 URL/프로세스 작업과 `--demo`를 분리한다. 데모에서는 외부 실행과 실제 종료를 금지한다.
- 네트워크 요청, 텔레메트리, 자동 업데이트는 추가하지 않는다. Vercel은 사용자가 등록한 HTTPS 링크만 연다.
- 디자인의 기준은 `DESIGN.md` 및 원본 ZIP의 Swift UI이다. 오류·빈 상태·키보드 탐색도 구현한다.

## 검증 및 보고
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1`
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test.ps1`
- 스냅샷은 `dist/WhatThePort.exe --snapshot artifacts/screenshots`로 만든다.
- 프로세스 종료 통합 검증은 테스트가 생성한 프로세스만 대상으로 한다.
- 변경 파일·검증 결과·실제 미검증 제약만 간결하게 보고한다. 구현되지 않은 기능을 완료라고 쓰지 않는다.
