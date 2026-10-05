# README 이미지 제작 기록

- 방식: 내장 `image_gen` 도구, 신규 이미지 생성
- 최종 파일: `docs/images/cover.jpg` (JPEG 품질 88, 135,176 bytes; 원본 PNG 대비 90.6% 절감)
- 용도: README 소개용 일러스트; 실제 UI 캡처와 구분 표시
- 실제 UI 이미지: `WhatThePort.exe --snapshot artifacts/screenshots` 출력 6개를 `docs/images`에 복사

## 생성 프롬프트

```text
Use case: ads-marketing
Asset type: wide README cover illustration for What the Port for Windows, approximately 2.4:1 landscape.
Primary request: a restrained, polished editorial illustration about keeping local development servers visible and organized in a small Windows system-tray utility.
Scene/backdrop: near-black charcoal fading subtly to blue-gray, matching a dark desktop utility.
Subject: on the right, three small dimensional dark blocks with softly illuminated port numbers ":3000", ":5173", ":8080", fine cyan, lavender and pink edge accents, understated lines suggesting memory history, and a tiny amber dot-grid motif. These are illustrative objects, not an application screenshot.
Composition: large clean typography on the left, generous breathing room, balanced and minimal. Soft studio lighting, subtle depth, no busy circuitry or futuristic holograms.
Text verbatim: "What the Port" as the main heading; "for Windows" below; "로컬 개발 서버를 한눈에" as the smaller Korean subtitle. Accurate legible type.
Constraints: no browser frame, no invented application UI, no fake badges, no extra claims, no Apple branding, no watermark. Dark charcoal #17191E, blue-gray #242B39, white #F5F5F7, accents cyan #6EC7ED, lavender #B599F0, pink #EB9CD4, amber #FFB224.
```
