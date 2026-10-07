# What the Port · Windows

**모두 압축을 푼 뒤 `WhatThePort.exe`를 더블클릭하세요.**

Windows 10/11 x64 · .NET Framework 4.8 · 관리자 권한 불필요.

| 파일·폴더 | 용도 |
|---|---|
| **WhatThePort.exe** | **실행할 앱 본체** |
| wtp.exe | 터미널용 명령 도구. 인수 없이 실행하면 본체를 열고 종료 |
| WhatThePort.Core.dll | 서버 조회·정책 라이브러리. 본체와 함께 유지 |
| WhatThePort.exe.config | .NET 실행·접근성 설정. 본체와 함께 유지 |
| fonts | 화면에 쓰는 글꼴과 글꼴 라이선스. 본체와 함께 유지 |
| docs | 상세 사용 가이드와 설명 이미지 |
| README.md / LICENSE | 이 안내문과 프로그램 라이선스 |

파일 수는 실행 중인 프로그램 수가 아닙니다. `wtp.exe`를 인수 없이 실행해도 상주하는 앱은 본체 하나이며, 이미 실행 중이면 기존 창을 엽니다.

앱 화면의 RAM·CPU 수치는 감시하는 서버의 사용량입니다. What the Port 자체의 사용량은 Windows 작업 관리자에서 `WhatThePort.exe`를 확인하세요. 기본 조회 주기는 3초이고, 트레이에 숨긴 동안 화면을 다시 그리지 않습니다. 설정에서 조회 주기를 늘릴 수 있습니다.

- 트레이 아이콘 / **Ctrl+Alt+P**: 패널 표시·숨김
- **× / Esc**: 트레이로 숨김. 감시는 계속됩니다.
- 트레이 우클릭 → **앱 종료 / Quit**: 프로그램 종료
- 새 버전으로 교체할 때는 기존 앱을 종료한 뒤 새 `WhatThePort.exe` 실행
- 서버 자동 정리는 기본 **Off**

터미널에서 목록을 확인하려면 압축을 푼 폴더에서 실행하세요.

```powershell
.\wtp.exe list
.\wtp.exe list --json
.\wtp.exe --help
```

상세 안내: [사용 가이드](docs/usage.md).
