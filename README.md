# SephiriaOne

> 개인적으로 놀려고 만든 잡탕 모드 (aka. 금수저런)

UI/명령어를 지원하긴 하지만 아직 부실한 점 양해 바랍니다.

## 설치 방법

- 릴리즈에서 받은 ZIP 파일내 SephiriaOne 폴더를 압축 해제합니다.
- 게임 설치 경로(Steam > `Sephiria` 우클릭 > 관리 > 로컬 파일 탐색)에서 `Sephiria/AddOns` 폴더를 찾아 위 내용물을 폴더째로 넣습니다. 만약 `AddOns` 폴더가 없다면 직접 생성합니다.

## 모드 기능

- 기본 스텟 배수 변경: 예를 들어 행운 스탯을 캐릭터의 3배로 설정하거나 마법서가속을 3배 더 빠르게 설정할 수 있습니다.
- [C 상태 메뉴의 능력치 27종 설정](docs/visible-stat-modifiers.md) (흡수, 피해 배율, 획득량 등)
- 보상 선택지 배수 변경
- 주사위 배수 변경
- 잎 배수 변경
- 닉네임 색상 변경
- 황금고블린 스폰 기능
- 힐러 포지션 추가
- 힐러 포지션 밀어주기용 박쥐 너프
- 항아리 스폰 확률 변경
- [무작위 이벤트 방 등장 확률 배수 변경](docs/random-events.md) (헌혈, 오벨리스크, 마법 분수 등)
- 팀킬 기능
- 초반에 얻은 아이템 판매 (코스튬/분수 포함) 허용
- 등등..

## 개발 관련

- OpenAI Codex `xhigh` effort + [superpowers](https://github.com/obra/superpowers)로 작업했습니다.
- [Visual Studio 2026](https://visualstudio.microsoft.com/downloads/)에서 작업했습니다. 설치시 인스톨러에서 `.Net desktop development` 개발 도구를 함께 설치해야 빌드가 가능합니다.
- 개발에 필요한 모든 문서는 `docs/` 디렉토리 하위에 있습니다. AI를 사용하여 개발할 경우를 대비하여 `AGENTS.md` 도 준비되어 있습니다. GitHub 계정이 있다면 우상단 `Fork` 기능을 활용하여 손쉽게 사본을 만들 수 있습니다.
- 수정 후 빠르게 게임에서 결과물을 확인할 수 있도록 배포 스크립트(Deploy Mod)가 내장되어 있습니다. `scripts/Deploy-Mod.ps1` 또는 Visual Studio 솔루션의 상단 `Deploy Mod` 버튼을 이용하세요. (로컬의 `C:\Program Files (x86)\Steam\steamapps\common\Sephiria\AddOns` 경로로 배포됩니다)

## 라이센스

GNU Affero General Public License (AGPL)

본 모드 수정시 더 많은 분들이 혜택을 누릴 수 있도록 소스코드를 공개해주시면 감사하겠습니다.

누구나 본 프로젝트의 코드와 산출물의 판매/배포/공유/변경을 자유롭게 할 수 있습니다.

## 크레딧 (Attribution)

https://github.com/Mira090 님이 작성하신 모드를 레퍼런스로 참고하였습니다.

Thanks!
