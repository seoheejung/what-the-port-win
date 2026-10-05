# Windows 다운로드·소개 페이지

- 구성: 의존성 없는 정적 HTML / CSS / JavaScript
- 공개 루트: `docs/`; `docs/.nojekyll`로 Jekyll 생략
- 준비: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/prepare-site.ps1`
- 로컬 미리보기: `node scripts/serve-site.mjs` → `http://127.0.0.1:4173`
- 다운로드: `docs/downloads/WhatThePort-Windows-x64.zip` — 실제 다운로드를 위해 Git 추적 대상 포함
- 내용: 홈, 기능·화면 소개, 사용 가이드, 404, 파비콘, 모바일 메뉴, ZIP·체크섬
- 배포: 미실행. 프로젝트 AGENTS.md의 외부 쓰기·퍼블리싱 사전 승인 필요

실제 호스팅 적용 시 HTTPS, CSP, nosniff, Referrer-Policy 및 실제 HTTP 404 매핑 확인 필요. 로컬 서버의 헤더 설정은 `scripts/serve-site.mjs` 기준. 생산용 웹 서버로 사용하지 않음.

## GitHub Pages 설정

저장소 **Settings → Pages → Build and deployment → Deploy from a branch → main → /docs → Save**.

게시 후 예상 주소: `https://seoheejung.github.io/what-the-port-win/`.
GitHub 저장소의 `blob/main/docs/index.html` 링크는 소스 보기 주소이며 웹사이트 주소와 별개.

현재 404의 복귀·스타일 경로는 `/what-the-port-win/` 기준. 저장소 이름 변경이나 사용자 지정 도메인 적용 시 `404.html`과 미리보기·검증 스크립트의 prefix 변경 필요.

기준: [GitHub 공식 게시 소스 설정 안내](https://docs.github.com/en/pages/getting-started-with-github-pages/configuring-a-publishing-source-for-your-github-pages-site).

## 준비·검증

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/prepare-site.ps1
node scripts/serve-site.mjs
# 다른 터미널
node scripts/check-site.mjs
```

프로젝트 경로 미리보기: `http://127.0.0.1:4173/what-the-port-win/`.
검증 범위: 320~1440px 화면, 키보드 메뉴·탭, 대체 텍스트, 로컬 링크, 중첩 주소 404, ZIP·SHA-256.

앱 패키지에는 Markdown 사용 문서·이미지만 포함. 사이트 다운로드 ZIP의 자기 포함·반복 빌드 시 용량 증가 방지.
로컬 서버의 보안 응답 헤더는 GitHub Pages에 자동 이전되지 않음.
