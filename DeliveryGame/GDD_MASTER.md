# DELIVERY RUN GDD MASTER

- Version: 1.0
- Date: 2026-02-25
- Purpose: 신규 프로젝트 재시작을 위한 단일 기준 기획서
- Scope: 기획, 시스템 구조, 데이터 구조, 제작/검증 기준

---

## 1) 게임 정의

### 1.1 한 줄 정의
플레이어는 7분 동안 배달 런을 수행하며, 런 중 3번의 음악 선택으로 규칙을 바꾸고, 수익으로 월드를 확장한다.

### 1.2 장르/경험
- 장르: 캐주얼 액션 + 운영 전략 + 라이트 메타 성장
- 관점: 3D 주행 기반(도시 이동, 상호작용 포인트 중심)
- 핵심 경험:
  - 짧은 세션 안에서 빠른 판단
  - 음악 선택으로 플레이 스타일 실시간 변화
  - 배달 리스크(온도/흔들림/평판)와 보상 최적화

---

## 2) 제품 원칙

1. 짧고 강한 세션: 1회 7분 완결
2. 선택이 곧 룰 변화: 음악 선택은 즉시 런 규칙에 반영
3. 실수는 비용, 숙련은 복구: 평판/음식 상태/주문 의사결정의 상호작용
4. 런 결과의 누적 가치: 수익이 월드 확장과 업그레이드로 이어짐
5. 시스템 결합은 이벤트 중심: 도메인 간 직접 참조 최소화

---

## 3) 코어 루프

### 3.1 런 루프 (In-Run)
1. 런 시작
2. 음악 선택 (1차)
3. 주문 수락 -> 픽업 -> 배송 -> 정산 반복
4. 음악 선택 (2차)
5. 반복 운영
6. 음악 선택 (3차)
7. 막판 운영(Last Order)
8. 런 종료/정산

### 3.2 메타 루프 (Out-of-Run)
1. 런 수익 누적
2. 월드 섹터 확장
3. 업그레이드 구매/적용
4. 다음 런 난이도/효율 변화

---

## 4) Canonical 규칙 (신규 프로젝트 기준)

### 4.1 런 타임라인
- 런 시간: 7분 (420초)
- 음악 선택 시점: 0:00 / 3:00 / 5:00
- Last Order 구간: 6:00 ~ 7:00

### 4.2 중단/시간 정책
- 음악 선택 중: 게임플레이만 정지
- UI/오디오는 유지
- 런 시간은 정지 손해가 없도록 `GameClock`(unscaled 기반)으로 계산

### 4.3 주문 정책
- 수락 제한: 5초
- 활성 주문 슬롯: 최대 3
- 앱 오버레이는 주행 중 열리며, 게임은 멈추지 않음
- 앱 오픈 중 조작 리스크 증가(조향 민감, Spill 위험 증가)

### 4.4 평판 정책
- 범위: 5.0 -> 0.0
- 0.0 도달 즉시 런 종료
- 조기 종료 시점까지의 수익 정산

---

## 5) 핵심 시스템 사양

### 5.1 Run Session System
- 책임:
  - 런 상태머신(Ready, Running, PauseForChoice, Ended)
  - 페이즈/타이밍 트리거
  - 조기 종료/정상 종료 처리
- 입력:
  - 음악 선택 이벤트
  - 평판 0 이벤트
  - 시간 만료
- 출력:
  - 런 시작/시간 업데이트/페이즈 도달/런 종료 이벤트

### 5.2 Music Draft & Modifier System
- 책임:
  - 지정 시점에 후보 3개 제공
  - 선택 결과를 런 모디파이어로 변환
  - 선택 누적에 따른 시너지 규칙 계산
- 설계:
  - 트랙/장르/시너지 모두 데이터(SO) 기반
  - 모디파이어는 소스 단위로 add/remove 가능해야 함

### 5.3 Order & Delivery System
- 책임:
  - 오퍼 생성/만료/수락
  - 활성 주문 풀 관리(슬롯 제한)
  - 픽업 -> 배송 완료 판정
- 설계:
  - 오퍼/활성 주문 상태 분리
  - 실패/만료/성공이 평판/경제에 동일 규약으로 연결

### 5.4 Food State System (Temperature + Spill)
- Temperature:
  - 시간 경과, 운전 상태, 이벤트, 모디파이어 영향을 받음
- Spill:
  - 급조향/급정지/충돌/앱 오픈 리스크에 반응
- 판정:
  - 배송 시 두 상태를 결합해 품질 점수 산출
  - 품질 점수는 평판/보상에 반영

### 5.5 Rating System
- 책임:
  - 주문 만료, 충돌, Spill, 배송 품질 기반 델타 계산
  - 임계치(0.0) 도달 시 종료 신호 발행
- 정책:
  - 리스크 이벤트는 하락, 안정/정확 배송은 회복
  - 연속 성공은 스트릭 보너스로 완충

### 5.6 Economy & Meta System
- 책임:
  - 런 재화(세션) + 총 재화(메타) 분리 관리
  - 런 종료 정산
  - 섹터 해금/업그레이드 반영
- 정책:
  - 메타는 런 난이도와 효율에 영향
  - 밸런스는 데이터 테이블로 조정 가능해야 함

### 5.7 World/Sector System
- 책임:
  - 섹터 잠금/해금 상태 관리
  - 씬 내 섹터 오브젝트 활성화 제어
- 정책:
  - 해금 상태는 저장/로드 가능 구조
  - 해금 이벤트는 UI와 즉시 동기화

### 5.8 UI System
- 책임:
  - 런 HUD, 주문 앱, 음악 선택, 결과 화면
  - 안전 영역, 가독성, 알림 중복 제어
- 정책:
  - 핵심 정보는 한눈에 확인 가능
  - 입력 중복/토스트 스팸 방지
  - UI 프리팹/뷰는 Addressables로 로드하고, 씬 직접 참조를 최소화

### 5.9 Sound System
- 책임:
  - BGM 레이어 재생, SFX 재생, 씬 전환 시 오디오 상태 유지
  - 음악 선택 결과를 즉시 청각 피드백으로 반영
- 정책:
  - BGM/SFX 클립은 Addressables로 로드
  - 로딩 실패 시 안전한 폴백(무음 또는 기본 클립) 제공
  - 런타임 중복 로드를 줄이기 위해 핸들 캐시/해제를 명시적으로 관리

---

## 6) 신규 프로젝트 구조 (정돈된 기본 골격)

### 6.1 Scene 구조
- `Assets/Scenes/Core/` : CoreScene (영속 매니저)
- `Assets/Scenes/Lobby/` : LobbyScene (메타 허브)
- `Assets/Scenes/Run/` : RunScene/TestRunScene (실제 플레이)
- `Assets/Scenes/Loading/` : LoadingScene (전환 시각화)

### 6.2 Script 구조
- `Assets/Scripts/Managers/Core/` : 코어 인프라, 이벤트 허브, 레지스트리
- `Assets/Scripts/Managers/Subs/` : SubManager 구현
- `Assets/Scripts/Delivery/` : 런/주문/음식/평판 도메인
- `Assets/Scripts/Music/` : 음악/시너지/모디파이어 도메인
- `Assets/Scripts/UI/` : 프레젠터/뷰/바인딩
- `Assets/Scripts/Data/` : SO 정의/런타임 데이터 모델

### 6.3 Data 구조
- `Assets/Data/` : 모든 SO 인스턴스의 기본 위치
- `Assets/Resources/` : 부트스트랩 수준에서만 제한 사용
- `Assets/AddressableAssetsData/` : UI/Audio 포함 런타임 로딩 에셋 그룹 관리
- UI/사운드 에셋은 `Resources` 대신 Addressables를 기본 경로로 사용

### 6.4 Addressables 그룹 네이밍 표준 (고정)

아래 그룹명은 신규 프로젝트에서 고정 사용한다.

| 용도 | Group Name | Key Prefix | Label |
|---|---|---|---|
| 공통 UI | `UI_Common` | `ui/common/` | `ui`, `ui:common` |
| 로비 UI | `UI_Lobby` | `ui/lobby/` | `ui`, `ui:lobby` |
| 런 UI | `UI_Run` | `ui/run/` | `ui`, `ui:run` |
| 배경음(BGM) | `Audio_BGM` | `audio/bgm/` | `audio`, `audio:bgm` |
| 효과음(SFX) | `Audio_SFX` | `audio/sfx/` | `audio`, `audio:sfx` |
| UI 사운드 | `Audio_UI` | `audio/ui/` | `audio`, `audio:ui` |
| 폴백/공용 | `Shared_Fallback` | `shared/fallback/` | `shared`, `fallback` |

규칙:
- Group 이름은 위 표 외 신규 생성 금지(예외는 기술 리드 승인 필요)
- Key는 소문자 + 슬래시 경로 규칙 고정
- 하나의 에셋은 1개 Group을 기본으로 하며, 중복 수록 금지

### 6.5 컴팩트 구조 유지 원칙 (고정)

- 최상위 구조는 `Scenes`, `Scripts`, `Data`, `Prefabs`, `Audio`, `AddressableAssetsData`를 기본으로 유지
- 새 폴더 추가는 기존 구조에 배치 불가할 때만 허용
- `Scripts` 하위에 도메인 중복 폴더(예: `Helpers`, `Utils2`, `Temp`) 생성 금지
- 기능 1개당 진입점 클래스 1개를 원칙으로 하고, 파일 분할은 책임 증가 시에만 수행
- 임시/실험 자산은 전용 작업 브랜치에서만 유지하고 본선 병합 전 정리
- 런타임 로딩 대상(UI/Audio)은 Addressables만 사용하고 `Resources` 중복 배치 금지

---

## 7) 아키텍처 원칙

### 7.1 결합 규칙
- 시스템 간 직접 참조 금지(필수 의존 제외)
- 이벤트 버스(pub/sub) 기반 통신
- 도메인 데이터는 payload로 전달

### 7.2 시간 규칙
- 게임플레이: timeScale 영향
- UI/오디오/세션 클럭: unscaled 시간
- 주문 타이머/이벤트 타이머/런 타이머는 동일 기준(`GameClock`) 사용

### 7.3 안정성 규칙
- 구독/해제 쌍 보장
- 런 종료 시 코루틴/타이머/버프/임시 상태 전부 정리
- 중복 초기화, 중복 이벤트 발행 방지

### 7.4 성능 규칙
- Update 경로에서 LINQ/할당 최소화
- 반복 탐색(`Find*`)은 캐시/주기 제한
- UI 피드백 오브젝트는 재사용 중심

---

## 8) 데이터 주도 설계

신규 프로젝트의 기본 SO 셋:
- `MusicGenreSO`, `MusicTrackSO`, `MusicSynergySO`
- `ContractTypeSO`, `ContractSO`
- `WorldEventSO`
- `RegionSO`
- `UpgradeSO`
- `RunConfigSO`, `OrderConfigSO`, `RatingConfigSO`, `EconomyConfigSO`

원칙:
- 수치/확률/테이블은 코드 하드코딩 대신 SO/JSON으로 분리
- 실험 밸런스는 데이터 교체만으로 가능해야 함
- UI/사운드 런타임 에셋은 Addressable key/label 규약으로 참조

---

## 9) UX/플로우 명세

### 9.1 기본 플로우
1. CoreScene 부팅
2. Lobby 진입
3. Run 시작 요청
4. Loading 표시
5. Run 플레이
6. 결과/정산
7. Lobby 복귀

### 9.2 런 HUD 핵심 정보
- 남은 시간
- 평판
- 활성 주문/수락 대기
- Temperature/Spill
- 현재 모디파이어/시너지
- 재화 변화

### 9.3 실패/위험 피드백
- 평판 임계 경고(시각/청각)
- Spill 임계 경고
- 주문 만료 직전 경고

---

## 10) 밸런스/텔레메트리 정책

### 10.1 1차 밸런스 목표
- 평균 런 완주율/조기 종료율을 측정 가능한 형태로 정의
- 주문 성공률, 만료율, 충돌률, Spill 발생률을 핵심 KPI로 관리
- 음악 선택별 승률 편차가 과도하지 않도록 조정

### 10.2 필수 로그
- 런 시작/종료/종료 원인
- 주문 발행/수락/만료/완료
- 평판 델타 원인
- 음악 선택 이벤트
- 정산 내역

---

## 11) 개발 단계 제안 (신규 프로젝트)

### Phase A: Core Vertical Slice
- Core/Lobby/Run/Loading 씬 골격
- 7분 런 타이머 + 1개 주문 + 1회 음악 선택 + 정산

### Phase B: Full Core Loop
- 3회 음악 선택
- 주문 슬롯/수락 제한/품질 판정
- 평판 조기 종료

### Phase C: Meta Integration
- 섹터 해금
- 업그레이드 적용
- 저장/로드

### Phase D: Polish & Content
- UX 정리
- 밸런스 튜닝
- 콘텐츠 확장

---

## 12) 수용 기준 (Acceptance)

- 컴파일 에러 0
- 런 7분 진행/조기 종료 정상
- 음악 선택 3회에서 pause/resume 정상
- 주문 5초 수락/슬롯 3 제한 정상
- Temperature/Spill이 주행 이벤트에 반응
- 평판 0 즉시 종료 + 정산 정상
- 로비 -> 런 -> 로비 전환 안정
- Addressables 기반 UI 프리팹 로드 성공(핵심 HUD/모달)
- Addressables 기반 사운드 로드/재생 성공(BGM/SFX, 씬 전환 후 유지)
- Addressables 그룹/키/라벨이 `6.4` 표준안과 일치
- 불필요한 신규 폴더 없이 `6.5` 컴팩트 구조 원칙 준수

---

## 13) 범위 제외 (Out of Scope, 초기)

- 대규모 실시간 멀티플레이
- 복잡한 시네마틱 연출 파이프라인
- 과도한 시스템 동시 도입(교통 AI 확장, 고급 경제 시뮬레이션 등)

---

## 14) 문서 운영 규칙

- 본 문서를 신규 프로젝트의 Single Source of Truth로 사용
- 기획 변경은 먼저 본 문서 업데이트 후 구현
- 구현과 불일치 발견 시:
  1. 기획 의도 확인
  2. 본 문서 수정
  3. 코드/데이터 동기화
