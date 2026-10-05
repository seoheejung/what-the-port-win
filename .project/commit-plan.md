# 기능별 커밋·푸시

- 원격: `https://github.com/seoheejung/what-the-port-win.git`
- 브랜치: `main`; 기존 구현 커밋 완료 상태 확인 (`93f9b77`까지)
- 작업 환경 제약: 이번 변경의 `git add`에서 `.git/index.lock` 생성 권한 거부
- 수행 상태: 이번 변경의 커밋·푸시 미완료. 로컬 수정·빌드·검증 및 기능별 명령 준비 완료. GitHub Pages 설정 미변경

## 그룹

1. Core 보호 프로세스 확장·일부 종료 결과·언어 설정 저장 및 테스트
2. 한국어/영어 UI·종료 결과 안내·행 정렬·접근성·실제 데모 캡처
3. GitHub Pages용 `docs` 이동·프로젝트 경로 404·ZIP·체크섬·브라우저 검증
4. 검증 기록·사용자 확인 목록·커밋 실행 스크립트

일반 사용자 터미널 또는 `.git` 쓰기·네트워크가 허용된 세션에서 실행:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/commit-features.ps1 -Push
```

기존 staged 변경이 있으면 중단. 이미 커밋된 그룹은 생략. 강제 푸시·히스토리 재작성 없음. Git 작성자 정보·원격 인증은 사용자의 기존 Git 설정 사용.
