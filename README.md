# SephiriaOne

> 개인적으로 놀려고 만든 잡탕 모드 (aka. 금수저런)

UI/명령어를 지원하긴 하지만 아직 부실한 점 양해 바랍니다.

ESC 메뉴 우상단에서 모드 메뉴를 열 수 있습니다.

## 설치 방법

- [릴리즈](https://github.com/preco21/SephiriaOne/releases)에서 받은 ZIP 파일내 SephiriaOne 폴더를 압축 해제합니다.
- 게임 설치 경로(Steam > `Sephiria` 우클릭 > 관리 > 로컬 파일 탐색)에서 `Sephiria/AddOns` 폴더를 찾아 위 내용물을 폴더째로 넣습니다. 만약 `AddOns` 폴더가 없다면 직접 생성합니다.

## 모드 기능

- [GitHub 릴리즈 업데이트 알림 및 설치](docs/github-updates.md): `/one update install` 또는 모드 패널 상단 **업데이트 → 업데이트 설치**로 설치한 뒤 게임을 재시작합니다.
- 기본 스탯 배수 변경: 예를 들어 행운 스탯을 캐릭터의 3배로 설정하거나 마법서가속을 3배 더 빠르게 설정할 수 있습니다.
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
- [팀킬·데스매치 참가자 선택](docs/selective-friendly-fire.md): 선택이 없으면 모두 참가. 모두 선택 후 일부를 제외하거나 선택 초기화로 전체 참가를 복원할 수 있습니다.
- [PvP 흰 테두리 빵 제한](docs/pvp-white-bread.md): 모든 PvP 모드에서 참가자의 추가 피격 무적 시간을 제거하며, PvP 종료 시 기본 효과로 즉시 복구됩니다.
- [PvP 방어 MP 비용](docs/pvp-balancing.md#guard-mp-cost-0580-2026-10-16): PvP 밸런스 보정 시 최대 MP의 10%를 기본으로 소모합니다. 기존 방어 효율·완벽한 방어 감소 효과를 유지하며, 전투 → PvP 밸런스 보정에서 실시간 조절할 수 있습니다.
- [PvP 지속 피해 간격](docs/pvp-dot-intervals.md): 화상·플라즈마 화상·동상·중독의 간격을 각각 설정합니다. 기본 0.75초이며, 전투 → 지속 피해 간격에서 실시간 조절할 수 있습니다.
- [폭탄 술래잡기](docs/play-tag.md): `/one tag start` 또는 술래잡기 탭에서 시작합니다. 처치한 상대에게 폭탄을 넘기며 제한 시간이 줄어들고, 한 번 폭발하면 종료됩니다.
- [보은 석판 지급](docs/requital-slates.md): 아이템 탭에서 1인당 수량(0~20개)을 설정하고, 시작 시 자동 지급하거나 지금 지급할 수 있습니다. 자동 지급분만 설정 변경으로 회수합니다.
- [PvP HP 포션 무제한](docs/pvp-potions.md): 전투 → 보급에서 설정합니다. 기본 켜짐이며, 참가자의 일반·대형 HP 포션에만 적용됩니다.
- 초반에 얻은 아이템 판매 (코스튬/분수 포함) 허용
- 등등..

## 개발 관련

- OpenAI Codex `xhigh` effort + [superpowers](https://github.com/obra/superpowers)로 작업했습니다.
- [Visual Studio 2026](https://visualstudio.microsoft.com/downloads/)에서 작업했습니다. 설치시 인스톨러에서 `.Net desktop development` 개발 도구를 함께 설치해야 빌드가 가능합니다.
- 개발 자료는 [문서 색인](docs/README.md)에서 찾을 수 있습니다. 일반 개발 지식, 게임 도메인 조사 결과, 기능별 설계와 참고 자료를 `docs/`에 정리했습니다. 다른 AI 에이전트도 이 자료를 참고하여 작업하도록 `AGENTS.md`에 안내되어 있습니다. GitHub 계정이 있다면 우상단 `Fork` 기능을 활용하여 손쉽게 사본을 만들 수 있습니다.
- 수정 후 빠르게 게임에서 결과물을 확인할 수 있도록 배포 스크립트(Deploy Mod)가 내장되어 있습니다. `scripts/Deploy-Mod.ps1` 또는 Visual Studio 솔루션의 상단 `Deploy Mod` 버튼을 이용하세요. (로컬의 `C:\Program Files (x86)\Steam\steamapps\common\Sephiria\AddOns` 경로로 배포됩니다)

## 라이센스

GNU Affero General Public License (AGPL)

본 모드 수정시 더 많은 분들이 혜택을 누릴 수 있도록 소스코드를 공개해주시면 감사하겠습니다.

누구나 본 프로젝트의 코드와 산출물의 판매/배포/공유/변경을 자유롭게 할 수 있습니다.

## 크레딧 (Attribution)

https://github.com/Mira090 님이 작성하신 모드를 레퍼런스로 참고하였습니다.

ありがとう！ Thanks! 
