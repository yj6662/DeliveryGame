# DELIVERY RUN 작업 계획서 (WBS + 단계별 로드맵)
- 기준 문서: **DELIVERY RUN GDD MASTER v2.0 (2026-02)**  
- 목적: 신규 프로젝트를 “전체 프로젝트 → 큰 목표 기능(에픽) → 세부 기능(피처) → 작업(Task)” 단위로 최대한 세분화하여, 개발/기획/아트/오디오/QA가 같은 체크리스트로 진행할 수 있게 한다.
- 범위: GDD에 정의된 Phase A~D, 시스템 사양, 아키텍처/데이터/Addressables 표준, 수용 기준(acceptance) 반영
Unity Version: 6000.3.9f1

---

## 0. 용어/규칙(고정)
- **런(1회 플레이 세션)**: 7분(420초), 음악 선택 0:00/3:00/6:00, Last Order 6:00~7:00
- **시간 기준**: 런 타이머/주문 타이머/이벤트 타이머는 `GameClock`(unscaled)로 일관
- **음악 선택 중 정지 정책**: 게임플레이만 정지, UI/오디오는 유지
- **주문 정책**: 수락 제한 5초, 활성 주문 슬롯 최대 3, 앱 오버레이는 주행 중 열리며 게임은 멈추지 않음(리스크 증가)
- **음악 장르(v2 고정)**: hiphop/ballad/edm/jazz/lofi/rock/classic/disco
- **평판 정책**: 5.0~0.0, 0.0 즉시 런 종료 + 그 시점까지 정산
- **통신 원칙**: 시스템 간 직접 참조 최소화, 이벤트 버스(pub/sub) 중심
- **런타임 로딩 원칙**: UI/Audio는 Addressables 기본, `Resources`는 부트스트랩 수준만 제한 사용
- **Addressables 그룹 표준**: UI_Common/UI_Lobby/UI_Run/Audio_BGM/Audio_SFX/Audio_UI/Shared_Fallback 고정(임의 신규 그룹 금지)

- **World layout rule (roads + blocks; buildings on blocks only; player road-only)**: roads are drivable, blocks are non-drivable building islands.
- **Pickup/Delivery interaction rule (nearest-road + F)**: spawn InteractPoint on nearest road to building, then complete by `F`.
---

## 1. 프로젝트 전체 목표(Top-level Goals)
### 1.1 제품/경험 목표
1) 7분 완결의 **짧고 강한 세션**  
2) 런 중 3회 **음악 선택 = 즉시 룰 변화(모디파이어)**  
3) **배달 리스크(온도/흔들림/평판)** 와 보상을 최적화하는 판단  
4) 런 결과가 메타(월드 확장/업그레이드)로 연결  

### 1.2 “완료” 정의(최종)
- Phase D 수용 기준 + 안정성/성능 규칙 충족
- 코어 루프/메타 루프가 최소 콘텐츠로 “반복 플레이 가능한 형태”로 작동
- 데이터 주도로 밸런스/콘텐츠 확장이 가능한 구조 완성

---

## 2. 개발 단계(마일스톤) 로드맵
> 각 Phase는 “데모 가능한 빌드”를 목표로 한다.

### Phase A: Core Vertical Slice (최소 재미/최소 완결)
**산출물**
- Core/Lobby/Run/Loading 씬 골격
- 7분 런 타이머
- 주문 1개 흐름(수락→픽업→배송→정산)
- 음악 선택 1회(0:00) + 선택 결과를 런에 반영(최소 1개 모디파이어)
- 결과/정산 화면 → 로비 복귀

**완료 기준(DoD)**
- 컴파일 에러 0, 로비→런→로비 전환 안정
- Addressables로 HUD/모달 로드 성공
- Audio Addressables 로드/재생 최소 1개 성공

---

### Phase B: Full Core Loop (GDD 코어 루프 완성)
**산출물**
- 음악 선택 3회(0/3/6분) + pause/resume 정상
- 주문 슬롯 3 / 5초 수락 제한 / 오퍼 만료 / 실패 처리
- Temperature + Spill + 배송 품질 점수 산출
- 평판 시스템(0.0 즉시 런 종료 + 정산)
- “앱 오버레이 주행 중 오픈(리스크 증가)” 구현

---

### Phase C: Meta Integration (런 바깥의 성장)
**산출물**
- 런 재화(세션) vs 총 재화(메타) 분리, 런 종료 정산
- 섹터 해금, 업그레이드 적용
- 저장/로드(해금/업그레이드/재화/설정)

---

### Phase D: Polish & Content (완성도/확장)
**산출물**
- UX 정리(알림/가독성/피드백/튜토리얼 최소)
- 밸런스 튜닝(데이터만으로)
- 콘텐츠 확장(트랙/시너지/계약/지역/업그레이드)
- 성능/안정성 최적화, QA 시나리오 커버

---

## 3. 공통 작업 규약(전 파트 공통)
### 3.1 태그 규칙
- [ENG] 프로그래밍
- [DES] 기획/밸런스/규칙/스펙
- [UI] UI/UX 설계/구현
- [ART] 모델/아이콘/배경/이펙트
- [AUDIO] BGM/SFX/믹싱
- [QA] 테스트/체크리스트/버그 재현
- [TOOLS] 에디터 툴/파이프라인

### 3.2 Definition of Done(공통)
- 기능 요구 충족 + 예외/실패 케이스 처리
- 이벤트 구독/해제 쌍 보장, 런 종료 시 상태 정리 완료
- Addressables 키/라벨/그룹 규약 준수
- 로그/디버그 정보(최소) 제공
- 테스트 시나리오 1개 이상 통과

---

# 4. WBS: 전체 프로젝트 → 큰 목표 기능 → 세부 기능 → 작업(Task)

---

## 4.1 프로젝트 기반(Foundation) — “프로젝트가 깨지지 않게”
### 4.1.1 (Epic) 저장소/브랜치/빌드 베이스
#### (Feature) Unity 프로젝트 생성/패키지 기준 확정
- [ENG] Unity LTS 버전 선정 및 기록(README)
- [ENG] 필수 패키지 설치/버전 고정: Addressables, Input System(사용 시), TextMeshPro, URP(사용 시)
- [ENG] 프로젝트 세팅 프리셋: Quality, Layers/Tags, Physics, Time, PlayerSettings(플랫폼)
- [QA] “빈 프로젝트 빌드” 성공 확인(PC 기준)

#### (Feature) Git/폴더 구조/코딩 규약
- [ENG] `.gitignore`, `README`, `CONTRIBUTING`(간단 규칙) 작성
- [ENG] 폴더 구조를 GDD 6장 기준으로 생성(Scenes/Scripts/Data 등)
- [ENG] Assembly Definition(asmdef) 도입 여부 결정 및 기본 분리(Managers/Delivery/Music/UI/Data)
- [ENG] 네이밍/namespace 규칙 문서화(예: DeliveryRun.Core 등)

---

### 4.1.2 (Epic) Addressables 표준 세팅(초기 고정)
#### (Feature) 그룹/키/라벨 고정 및 검증 장치
- [ENG][TOOLS] Addressables 그룹 생성: UI_Common/UI_Lobby/UI_Run/Audio_BGM/Audio_SFX/Audio_UI/Shared_Fallback
- [ENG][TOOLS] 그룹명/키 프리픽스/라벨 템플릿 문서화
- [ENG][TOOLS] “표준 외 신규 그룹 생성 감지” 에디터 검증(빌드 전 체크 스크립트)
- [QA] 라벨로 로드 테스트(예: `ui:run`로 HUD 로드)

---

### 4.1.3 (Epic) 이벤트 중심 아키텍처 뼈대
#### (Feature) EventBus/MessageHub 구축
- [ENG] 이벤트 모델 결정(typed struct events vs enum+payload)
- [ENG] 구독/해제 API, 중복 구독 방지 옵션
- [ENG] 디버그 모드: 최근 이벤트 로그(개발 빌드 한정)
- [ENG] “런 종료 시 일괄 구독 해제” 패턴 가이드 제공

#### (Feature) GameClock(unscaled) 표준화
- [ENG] `GameClock` 구현: unscaled delta 기반 tick, pause 영향 옵션
- [ENG] 타이머 유틸: `Schedule(delay, callback)`/`Cancel(handle)`
- [ENG] 런 타이머/주문 타이머가 동일 시스템 사용하도록 강제

---

## 4.2 씬/부트스트랩/전환(Core Runtime)
### 4.2.1 (Epic) CoreScene(영속 매니저) + LoadingScene
#### (Feature) Core 부팅 파이프라인
- [ENG] CoreScene 생성(씬에 영속 오브젝트 1개: `CoreRoot`)
- [ENG] `CoreRoot` 구성: EventBus, GameClock, AudioManager, AddressablesService, SaveService(Phase C), Analytics(Phase D)
- [ENG] 부트 순서(Init order) 정의 및 로그

#### (Feature) 씬 전환(로비↔런) + 로딩 화면
- [ENG] SceneRouter/SceneFlow 구현
  - 로비→로딩→런
  - 런 종료→로딩→결과/로비
- [UI] LoadingScene UI(스피너/팁/진행률)
- [ENG] Addressables prewarm(선로딩) 옵션: 런 진입 시 HUD/Audio 최소 세트
- [QA] 씬 전환 30회 반복 안정성 체크(메모리/중복 오브젝트)

---

## 4.3 런 세션 시스템(Run Session System)
### 4.3.1 (Epic) 런 상태머신 + 타임라인 트리거
#### (Feature) 상태 정의 및 전이
- [ENG] 상태: Ready → Running → PauseForChoice → Ended 구현
- [ENG] 전이 이벤트 정의:
  - StartRun
  - ReachMusicChoice(time=0/180/360)
  - LastOrderStart(time=360)
  - TimeExpired(time=420)
  - RatingZero
- [ENG] 런 종료 원인 enum 정의(시간 만료/평판 0/강제 종료 등)

#### (Feature) 런 타이머(7분) + UI 업데이트
- [ENG] GameClock 기반 런 카운트다운
- [UI] HUD에 남은 시간 표시(mm:ss)
- [QA] 음악 선택 중에도 런 시간이 손해 없이 흐르는지 확인(“게임플레이만 정지” 준수)

#### (Feature) 런 종료 처리(정리/정산/전환)
- [ENG] 런 종료 시 정리 루틴 표준화
  - 타이머 cancel
  - 코루틴 stop
  - 모디파이어 remove
  - UI close/reset
  - 임시 데이터 flush
- [ENG] Ended → Result UI 표시, 로비 복귀 루트
- [QA] 조기 종료(평판 0)에서도 정리/정산 정상

---

## 4.4 음악 드래프트 & 모디파이어(Music Draft & Modifier System)
### 4.4.1 (Epic) 음악 선택 3회 + 룰 변화 시스템
#### (Feature) 데이터(SO) 모델 정의
- [ENG][DES] SO 정의/필드:
  - MusicGenreSO(장르 id, 설명, 태그)
  - MusicTrackSO(트랙 id, 장르, 길이/클립, 기본 모디파이어)
  - MusicSynergySO(조합 규칙, 발동 조건, 추가 모디파이어)
- [DES] Phase A용 최소 데이터(장르 2, 트랙 6, 시너지 1) 작성
- [QA] 런타임 로드/참조 에러 검증

#### (Feature) 드래프트(후보 3개 제시) 로직
- [ENG] “지정 시점”에 후보 3개 생성 규칙(중복 방지, 가중치/희귀도 옵션)
- [ENG] 이전 선택/시너지 고려 여부(Phase B부터 확장)
- [ENG] 선택 결과 이벤트 발행: `OnMusicChosen(trackId, timeStamp)`

#### (Feature) 선택 UI(모달) + 정지 정책 구현
- [UI] 음악 선택 모달 UI(후보 3 카드: 이름/효과/리스크)
- [ENG] PauseForChoice 진입 시:
  - 플레이어 조작/물리/AI/주행 update 정지(시간 스케일 or 입력 차단)
  - UI/오디오 유지
- [UI] 선택 타이머/입력(선택, 상세보기, 자동 선택 정책 여부)
- [QA] 0:00/3:00/6:00 정확히 트리거되는지 검증

#### (Feature) 모디파이어 적용/해제(누적/원인 추적)
- [ENG] Modifier 모델:
  - sourceId(트랙/시너지/업그레이드 등), stat, add/mul, duration(optional)
- [ENG] ModifierStack 서비스:
  - add/remove by sourceId
  - 현재 적용값 조회 API
- [ENG] 시스템 연동 포인트:
  - 주문 보상/평판 델타/온도 감소율/스필 민감도/조향 민감도 등
- [QA] 트랙 변경 시 즉시 효과 반영 + 런 종료 시 초기값 복구

---

## 4.5 주문 & 배달(Order & Delivery)
### 4.5.1 (Epic) 오퍼 생성/수락/슬롯/만료/완료 전체 흐름
#### (Feature) 계약/주문 데이터 모델
- [ENG][DES] SO 정의:
  - ContractTypeSO(종류, 기본 보상, 기본 제한)
  - ContractSO(픽업/배송 포인트 타입, 거리/난이도, 보상, 실패 패널티)
  - OrderConfigSO(수락 제한 5초, 슬롯 3, 스폰 주기 등)
- [DES] 테스트용 계약 5종 작성(짧/중/길, 리스크 차등)
- [QA] 데이터 교체만으로 보상/난이도 변하는지 확인

#### (Feature) 오퍼 생성/표시/만료(수락 제한 5초)
- [ENG] OfferSpawner:
  - 런 진행에 따라 오퍼 생성(Phase A: 1개 고정)
  - Offer TTL 5초(수락 제한)
- [UI] 오퍼 알림(토스트/카드) + 카운트다운
- [ENG] 만료 이벤트 발행: `OfferExpired` → 평판/경제 연동(Phase B)

#### (Feature) 수락/활성 주문 슬롯(최대 3)
- [ENG] ActiveOrderPool:
  - accept 시 슬롯 체크
  - 최대 3 유지
  - 슬롯 가득 찼을 때 UX(수락 불가/교체 불가/경고)
- [UI] 주문 앱/오버레이에서 활성 주문 리스트 표시
- [QA] 슬롯 가득 찬 상태에서 새 오퍼 등장/수락 시도 케이스

#### (Feature) 픽업 → 배송 완료 판정
- [ENG] 도로 스폰형 InteractPoint + F 상호작용(Nearest Road 규칙; 건물 기준 가장 가까운 도로 지점 스폰)
- [ENG] 주문 상태머신(Offered/Accepted/PickedUp/Delivered/Failed)
- [ENG] 수락 시 Pickup InteractPoint를 Restaurant Building의 Nearest Road에 스폰, Pickup 완료 시 Delivery InteractPoint를 Destination Building의 Nearest Road에 스폰
- [ENG] Success/Fail/Expire/Run End/Scene Change 시 InteractPoint를 즉시 정리하고 중복/유령 오브젝트를 금지
- [ENG] 경로/목표 핀(미니맵 여부는 Phase D, Phase A/B는 월드 마커)
- [QA] 픽업 없이 배송 시도, 반대로 배송 실패 등 예외 케이스

#### (Feature) 실패 처리(만료/파손/시간 초과 등)
- [ENG] 실패 원인 enum + 공통 처리 루틴(평판 델타, 보상 0, 로그)
- [UI] 실패 피드백(사운드/텍스트)
- [QA] 실패 연쇄 발생 시 UI 스팸 방지

---

## 4.6 음식 상태(Food State: Temperature + Spill)
### 4.6.1 (Epic) 주행 기반 품질 시스템
#### (Feature) Temperature 모델(시간/운전 상태/모디파이어)
- [ENG][DES] Temperature 계산식/파라미터를 RatingConfig/EconomyConfig와 분리하여 FoodConfig로 관리(또는 OrderConfig 확장)
- [ENG] 기본 감소율 + 이벤트 영향(정지/가속/비/지역 이벤트 등 Phase D)
- [UI] HUD에 온도 게이지 표시
- [QA] 프레임레이트 변동에도 동일 결과(시간 기반)

#### (Feature) Spill 모델(급조향/급정지/충돌/앱 오픈 리스크)
- [ENG] 운전 이벤트 감지:
  - 조향 변화량(angular velocity), 감속/충돌 임펄스
- [ENG] Spill 누적/복구 규칙(데이터화)
- [ENG] 앱 오버레이 오픈 중 리스크 증가 반영(스필 민감도 상승)
- [UI] Spill 경고 임계치 알림
- [QA] “의도치 않은 스필 폭주” 방지(클램프/쿨다운)

#### (Feature) 배송 시 품질 점수 산출
- [ENG] 품질 점수 = f(Temperature, Spill) (데이터 기반 커브/테이블)
- [ENG] 품질이 평판/보상에 반영되는 연결점 정의
- [UI] 결과/정산에 “품질 등급” 표시

---

## 4.7 평판/평점(Rating System)
### 4.7.1 (Epic) 평판 델타 규칙 + 조기 종료(0.0)
#### (Feature) 평판 모델 + 이벤트 기반 델타 계산
- [ENG][DES] RatingConfigSO(원인별 델타: 만료, 충돌, Spill, 품질, 연속 성공 보너스 등)
- [ENG] `RatingService`:
  - ApplyDelta(reason, amount)
  - Clamp(0~5)
  - OnRatingChanged, OnRatingZero 이벤트
- [UI] HUD에 평판 표시(숫자 + 색/경고)
- [QA] 동시에 여러 이벤트 발생 시 델타 합산 순서/중복 방지

#### (Feature) 스트릭/완충(연속 성공 보너스)
- [ENG] 스트릭 카운터(연속 성공 시 보너스/감쇠)
- [DES] 보너스 곡선 데이터화(과도한 스노우볼 방지)
- [QA] 실패 후 스트릭 리셋/부분 유지 정책 검증

#### (Feature) 0.0 즉시 런 종료 + 그 시점까지 정산
- [ENG] RatingZero → RunSession Ended 트리거 연결(이벤트)
- [ENG] 정산은 “완료된 주문 + 진행 중 처리 정책” 반영
- [QA] 0.0 도달 프레임에서 UI/오디오/씬 전환 중 오류 없는지

---

## 4.8 경제/메타(Economy & Meta)
### 4.8.1 (Epic) 런 재화/총 재화 분리 + 정산
#### (Feature) 세션 재화/메타 재화 모델
- [ENG][DES] EconomyConfigSO(기본 보상, 수수료/패널티, 메타 환산 규칙)
- [ENG] `EconomyService`:
  - SessionBalance(런 중)
  - MetaBalance(영구)
  - AddReward(source), ApplyPenalty(reason)
- [UI] 런 HUD 재화 변화 피드백(+숫자 플로팅/토스트)
- [QA] 런 종료/조기 종료 모두 정산 일관

#### (Feature) 런 종료 결과 화면(정산 내역)
- [UI] 결과 화면:
  - 총 수익, 주문 성공/실패/만료 수
  - 평균 품질/평판 변화
  - 선택한 트랙/시너지 요약
- [ENG] 정산 리포트 모델(`RunReport`) 생성/전달
- [QA] 데이터 누락/NULL 방지(항상 값 존재)

---

### 4.8.2 (Epic) 업그레이드 시스템(Phase C)
#### (Feature) UpgradeSO + 적용(모디파이어와 통합)
- [ENG][DES] UpgradeSO(가격, 효과, 해금 조건, 레벨)
- [ENG] 업그레이드 적용은 ModifierStack을 통해 통일
- [UI] 로비 업그레이드 화면(구매/적용/설명)
- [QA] 구매 후 런에 즉시 반영/저장/로드 유지

---

## 4.9 월드/섹터(World/Sector)
### 4.9.1 (Epic) 섹터 잠금/해금 + 씬 동기화(Phase C)
#### (Feature) RegionSO + 섹터 상태 관리
- [ENG][DES] RegionSO(섹터 id, 해금 비용, 연결 관계)
- [ENG] SectorState(locked/unlocked) 저장 구조
- [UI] 로비 월드 맵(최소: 리스트/노드 UI)
- [ENG] 해금 이벤트 → 월드 오브젝트 활성화/비활성화 연결
- [QA] 저장/로드 후 섹터 상태 정확히 복원

---

## 4.10 UI 시스템(UI/UX)
### 4.10.1 (Epic) 런 HUD / 주문 앱 / 음악 선택 / 결과 / 로비
#### (Feature) UI 아키텍처(프레젠터/뷰/바인딩) 정립
- [UI][ENG] MVVM 패턴
- [ENG] Addressables UI 로더(instantiate/release 핸들 관리)
- [UI] Safe Area 대응(모바일 대비)
- [ENG] 토스트/알림 스팸 방지 시스템(쿨다운/병합)

#### (Feature) 런 HUD(핵심 정보 한눈)
- [UI] 남은 시간, 평판, 활성 주문, 온도/스필, 모디파이어/시너지, 재화 표시
- [ENG] 데이터 바인딩(이벤트 구독 기반)
- [QA] 런 중 HUD 누락/겹침/성능 문제 체크

#### (Feature) 주문 앱 오버레이(주행 중 열림, 게임은 멈추지 않음)
- [UI] 오버레이: 활성 주문 리스트/상세/목표 표시
- [ENG] 오버레이 오픈 상태 플래그 → 운전 리스크 시스템에 전달
- [QA] 오버레이 열고 조작 시 입력 충돌(주행 입력/스크롤) 방지

#### (Feature) 음악 선택 모달(3회)
- 4.4에 포함(공통 UI 컴포넌트로 분리)

#### (Feature) 로비 UI(메타 허브)
- [UI] Run 시작 버튼, 업그레이드/섹터 진입, 설정, 최근 런 기록
- [ENG] Run 시작 요청 → SceneRouter 호출
- [QA] 로비 상태가 저장/로드와 일치

---

## 4.11 사운드 시스템(Sound System)
### 4.11.1 (Epic) BGM 레이어/상태 유지 + SFX
#### (Feature) AudioManager(영속) + Addressables 로드
- [AUDIO][ENG] BGM/SFX/UI 채널 분리(믹서 그룹)
- [ENG] Addressables 로드/캐시/해제 정책
- [ENG] 로딩 실패 폴백(무음/기본 클립)
- [QA] 씬 전환 후 오디오 지속/중복 재생 없음

#### (Feature) 음악 선택 결과의 즉시 청각 피드백
- [AUDIO] 트랙별 샘플(또는 루프) 준비
- [ENG] 선택 즉시 BGM 전환/레이어 변화
- [QA] 0:00/3:00/6:00 전환 시 클릭/끊김 최소화(페이드)

#### (Feature) 핵심 SFX(수락/실패/경고/성공)
- [AUDIO] UI 클릭, 경고(평판/스필), 성공/실패 SFX 제작
- [ENG] 이벤트 기반 SFX 트리거 매핑 테이블

---

## 4.12 데이터 주도 설계(Data/Configs)
### 4.12.1 (Epic) SO 세트 완성 + 샘플 데이터 + 밸런스 교체 가능
#### (Feature) Config SO 정의/관리
- [ENG][DES] RunConfigSO(런 시간/선택 시점/Last Order)
- [ENG][DES] OrderConfigSO(슬롯/수락 제한/스폰)
- [ENG][DES] RatingConfigSO(델타 규칙)
- [ENG][DES] EconomyConfigSO(보상/패널티/환산)
- [ENG][DES] “데이터만 교체해서 밸런스 변경” 검증

#### (Feature) 데이터 로딩/참조 표준
- [ENG] 부트스트랩 Resources에서 ConfigCatalog(주소만) 로드 → 실제는 Addressables 또는 직접 참조(선택)
- [ENG] 런 시작 시 필요한 데이터 검증(누락 시 에러 UI)

---

## 4.13 텔레메트리/로그/디버그(Phase D 핵심)
### 4.13.1 (Epic) 필수 로그 수집(분석/밸런스)
#### (Feature) 필수 로그 이벤트(런/주문/평판/음악/정산)
- [ENG] 로그 스키마:
  - RunStart/RunEnd(원인)
  - OfferSpawn/OfferAccept/OfferExpire
  - OrderPickup/OrderDeliver/OrderFail
  - RatingDelta(reason, amount)
  - MusicChoice(trackId)
  - Settlement(summary)
- [QA] 로그가 과도하게 스팸되지 않는지, 성능 영향 최소화

#### (Feature) 개발용 디버그 UI(토글)
- [ENG][UI] 현재 모디파이어 목록, 실시간 온도/스필 수치, 평판 델타 히스토리
- [QA] 릴리즈 빌드에서 비활성화 확인

---

## 4.14 저장/로드(Save/Load) — Phase C
### 4.14.1 (Epic) 메타 진행 저장
#### (Feature) 저장 데이터 스키마 정의
- [ENG] SaveData:
  - MetaBalance
  - UnlockedSectors
  - PurchasedUpgrades + levels
  - Settings(audio, controls)
  - LastRuns summary(optional)
- [ENG] 버전 관리(마이그레이션) 최소 구조

#### (Feature) 저장/로드 구현
- [ENG] 로컬 JSON/바이너리 선택 및 구현
- [QA] 세이브 손상/버전 불일치 폴백(새로 생성/백업)

---

## 4.15 콘텐츠 제작(최소 → 확장)
### 4.15.1 (Epic) 플레이 가능한 최소 콘텐츠 세트
#### (Feature) 테스트 월드/포인트
- [ART] 임시 도시 블록아웃(도로/코너/장애물 최소)
- [ENG] 픽업/배송 포인트 프리팹/마커
- [QA] 내비 없이도 목적지 찾을 수 있는 수준의 가시성(마커/표지)
- [ENG] Buildings are placed on blocks only, and pickup/delivery are road-side interact points (nearest-road spawn + F interaction).

#### (Feature) 트랙/시너지/계약 콘텐츠 확장(Phase D)
- [DES] 트랙 20+, 시너지 10+ 목표
- [DES] 계약 타입 다양화(거리/위험/보상)
- [QA] 선택 편향/사기 조합 여부 플레이테스트

---

## 4.16 QA/수용 기준(Acceptance) & 테스트 시나리오
### 4.16.1 (Epic) GDD 수용 기준을 테스트 케이스로 변환
#### (Feature) 핵심 시나리오(최소 15개)
- [QA] 런 7분 완주
- [QA] 음악 선택 3회 pause/resume 정상
- [QA] 주문 5초 수락 제한 정상
- [QA] 슬롯 3 제한 정상
- [QA] 앱 오버레이 주행 중 오픈 시 리스크 증가 반영
- [QA] Temperature/Spill이 이벤트에 반응
- [QA] 평판 0 즉시 종료 + 정산 정상
- [QA] Addressables UI/Audio 로드/해제 누수 없음
- [QA] 씬 전환 반복 안정

#### (Feature) 자동화/반자동 도구(선택)
- [ENG][TOOLS] “런 1회 자동 플레이(간이)” 시뮬레이터(키 이벤트/타이머)
- [ENG][TOOLS] Addressables 검증/리포트 자동 생성

- [QA] 블록 위 진입 불가 + 도로 스폰형 픽업/배송 상호작용(F) 정상
---

# 5. Phase별 “실행용 체크리스트”(우선순위 포함)

## 5.1 Phase A 체크리스트(P0만)
- [P0] Foundation: 폴더 구조, Addressables 그룹 고정, EventBus + GameClock
- [P0] CoreScene + SceneRouter + LoadingScene
- [P0] RunSession(7분 타이머, 상태머신, 종료/정리)
- [P0] MusicChoice 1회(0:00) + 모달 UI + 모디파이어 1개 적용
- [P0] Order 1개 고정 플로우(수락→픽업→배송→정산)
- [P0] Run HUD(시간/주문 상태/재화 최소)
- [P0] 오디오 로드/재생 1개(BGM) + UI 클릭 SFX 1개
- [P0] 결과 화면 + 로비 복귀

## 5.2 Phase B 체크리스트(P0/P1)
- [P0] 음악 선택 3회 + 시너지(최소 1개) + 누적 모디파이어 스택
- [P0] 오퍼 스폰/만료/슬롯3/수락5초/실패 처리
- [P0] Temperature + Spill + 품질 점수 + 평판 델타
- [P0] 평판 0 조기 종료 + 정산
- [P1] 앱 오버레이 UX 개선(목표 고정/상세보기)
- [P1] 경고/피드백(SFX/토스트) 정리

## 5.3 Phase C 체크리스트(P0/P1)
- [P0] Economy 메타/세션 분리 + 정산 → 메타 반영
- [P0] Sector 해금 + 업그레이드 구매/적용(ModifierStack 통합)
- [P0] Save/Load(메타 데이터)
- [P1] 로비 월드맵 UI 개선(노드/애니메이션)

## 5.4 Phase D 체크리스트(P0/P1/P2)
- [P0] 텔레메트리/로그 스키마 적용 + 디버그 UI 토글
- [P0] 성능 최적화(alloc 제거, 풀링, Update 최소화)
- [P0] QA 시나리오 전부 통과
- [P1] 콘텐츠 확장(트랙/시너지/계약/업그레이드/지역)
- [P1] UX 폴리시(가독성/경고/튜토리얼 최소)
- [P2] 빌드/배포 파이프라인, 크래시 리포트 등

---

## 6. 부록: 역할별 “이번 주 시작” 추천 작업 묶음
- **ENG 시작 묶음**: Addressables 표준 세팅 → EventBus/GameClock → SceneRouter/CoreRoot → RunSession(타이머/상태)  
- **UI 시작 묶음**: HUD 틀(시간/평판/주문 슬롯) → 음악 선택 모달 → 결과 화면  
- **DES 시작 묶음**: Phase A용 최소 SO 데이터(트랙/계약/설정) 작성 + 델타 규칙 초안  
- **AUDIO 시작 묶음**: BGM 2개(테스트 루프) + UI 클릭/경고 SFX 3종  
- **QA 시작 묶음**: Phase A 수용 기준 체크리스트 문서화 + 전환 반복/누수 테스트 템플릿


---

## 7. Next Sprint (2026-02-26)

### 7.1 P0 (즉시 진행)
- [ENG] Traffic 신호 루프 안정화:
  - 교차로 신호 위상(Vertical/Horizontal Green/Yellow) 결정적 순환 유지
  - NPC 교차로 진입 시 신호 위상 준수(정지/진행) 검증
- [ENG][QA] PlayMode 자동검증 추가/유지:
  - `Core -> Lobby -> Run` 진입 후 신호 서비스/시각화 생성 확인
  - 신호 위상 변경(phase change) 확인 테스트 유지
- [ENG][AUDIO] 교통 튜닝 기간 임시 BGM mute 유지(테스트 집중용)

### 7.2 P1 (다음 묶음)
- [ENG] Isometric 카메라 근접 오브젝트 clipping 완화(occlusion handling)
- [ENG][ART] 낮/저녁 시간대 전환 품질 개선(런 타임라인 동기화)
- [ENG][ART] Traffic 차량 변형/스폰 밸런싱(차선 밀도 기준)

### 7.3 실행 커맨드(회귀 검증)
- [QA] `Tools/RunTests_TrafficNpc_PlayMode.cmd`
- [QA] `Tools/RunTests_TrafficSignal_PlayMode.cmd`
