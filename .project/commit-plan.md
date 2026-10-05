# 기능별 커밋·푸시

- 원격: `https://github.com/seoheejung/what-the-port-win.git`
- 브랜치: `main`; 사용자 생성 직후 커밋 없는 상태
- 작업 환경 제약: `.git/index.lock` 쓰기 거부, 권한 상승 실행의 자동 정책 거절, GitHub HTTPS 연결 차단
- 수행 상태: 커밋·푸시 미완료. 소스·검증·기능별 명령 준비 완료

## 그룹

1. Core·Win32 TCP·종료 정책·설정·CLI
2. WPF UI·키보드/스크린리더·차트·다중 모니터 위치 저장·앱 빌드
3. 한글 README·사용 가이드·데모 이미지·디자인/검증 기록
4. Windows 다운로드 사이트·모바일·가이드/404·브라우저 검증
5. 보안 findings·접근성 보고서·무해한 증거 재현

일반 사용자 터미널 또는 `.git` 쓰기·네트워크가 허용된 세션에서 실행:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/commit-features.ps1 -Push
```

기존 staged 변경이 있으면 중단. 이미 커밋된 그룹은 생략. 강제 푸시·히스토리 재작성 없음. Git 작성자 정보·원격 인증은 사용자의 기존 Git 설정 사용.
