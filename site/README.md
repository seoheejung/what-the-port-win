# Windows 다운로드·소개 페이지

- 구성: 의존성 없는 정적 HTML / CSS / JavaScript
- 공개 루트: `site/dist`만 사용; 저장소 루트 공개 금지
- 준비: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/prepare-site.ps1`
- 로컬 미리보기: `node scripts/serve-site.mjs` → `http://127.0.0.1:4173`
- 다운로드: `site/dist/downloads/WhatThePort-Windows-x64.zip`
- 내용: 홈, 기능·화면 소개, 사용 가이드, 404, 파비콘, 모바일 메뉴, ZIP·체크섬
- 배포: 미실행. 프로젝트 AGENTS.md의 외부 쓰기·퍼블리싱 사전 승인 필요

실제 호스팅 적용 시 HTTPS, CSP, nosniff, Referrer-Policy 및 실제 HTTP 404 매핑 확인 필요. 로컬 서버의 헤더 설정은 `scripts/serve-site.mjs` 기준. 생산용 웹 서버로 사용하지 않음.
