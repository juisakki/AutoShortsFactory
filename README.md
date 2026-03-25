# AutoShortsFactory 🎬

> **AI 기반 유튜브 쇼츠·롱폼 영상 자동 생성 & 업로드 시스템** (.NET 8)

트렌드 수집부터 대본 생성, TTS, 렌더링, 썸네일, 업로드까지 전 과정을 완전 자동화합니다.

---

## 주요 기능

| 기능 | 설명 |
|------|------|
| 🔍 **트렌드 수집** | Google Trends RSS + YouTube 인기 + 뉴스 RSS 3개 소스 통합 |
| 🤖 **AI 주제 선정** | GPT-4o가 쇼츠화 가능성 판단 + SQLite 중복 방지 |
| 📝 **AI 대본 생성** | 쇼츠(15~60초) / 롱폼 TOP10·역사·과학 해설 |
| 🎙️ **TTS** | Edge TTS 한국어 (ko-KR-SunHiNeural) |
| 🎬 **B-Roll** | Pexels API 키워드 기반 스톡 영상 자동 다운로드 |
| 📑 **자막** | TTS 실제 길이 기반 SRT 정밀 타이밍 |
| 🎵 **BGM** | 저작권 무료 BGM 자동 믹싱 (-15~-20dB) |
| 🖥️ **렌더링** | FFmpeg: 쇼츠(1080×1920) / 롱폼(1920×1080) |
| 🖼️ **썸네일** | SkiaSharp 텍스트+이미지 자동 합성 |
| ✅ **품질 검사** | FFProbe blackdetect/silencedetect (검은화면 10%↑·무음 30%↑ 실패) |
| 📤 **YouTube 업로드** | YouTube API v3 OAuth 2.0 + AI 제목·설명·해시태그 자동 생성 |
| ⏰ **스케줄링** | Quartz.NET (쇼츠 3건 @06/10/14시, 롱폼 1건 @20시) |
| 🔄 **자동 재시도** | 실패 시 최대 3회 지수 백오프 재시도 (@매 30분) |
| 📊 **일일 리포트** | 생산/업로드 결과 JSON + 콘솔 출력 (@22시) |

---

## 일일 생산 스케줄

| 시간 | 유형 | 수량 |
|------|------|------|
| 06:00 | 쇼츠 | 1건 |
| 10:00 | 쇼츠 | 1건 |
| 14:00 | 쇼츠 | 1건 |
| 20:00 | 롱폼 | 1건 |
| 22:00 | 일일 리포트 | — |
| 매 30분 | 실패 재시도 | — |

---

## 시작하기

### 1. 필수 사전 설치

```bash
# .NET 8 SDK
# https://dotnet.microsoft.com/download/dotnet/8

# FFmpeg (PATH에 등록)
# https://ffmpeg.org/download.html

# Edge TTS CLI
pip install edge-tts
```

### 2. 리포지토리 클론

```bash
git clone https://github.com/juisakki/AutoShortsFactory.git
cd AutoShortsFactory
```

### 3. API 키 설정

`src/AutoShortsFactory/appsettings.json`을 열고 아래 값을 설정하세요:

```json
{
  "OpenAI": {
    "ApiKey": "sk-...",
    "Model": "gpt-4o"
  },
  "Pexels": {
    "ApiKey": "YOUR_PEXELS_API_KEY"
  }
}
```

### 4. YouTube OAuth 설정

1. [Google Cloud Console](https://console.cloud.google.com/)에서 YouTube Data API v3 프로젝트 생성
2. OAuth 2.0 클라이언트 ID(데스크톱 앱) 생성
3. `client_secret.json` 다운로드 후 실행 파일 옆에 배치

### 5. BGM 파일 준비 (선택 사항)

저작권 무료 BGM 파일(`.mp3`, `.wav`)을 `bgm/` 폴더에 넣어두세요.
[Pixabay Music](https://pixabay.com/music/) 등에서 무료 다운로드 가능합니다.

### 6. 실행

```bash
cd src/AutoShortsFactory
dotnet run
```

**첫 실행 시 대화형 설정 화면**이 나타납니다:

```
═══════════════════════════════════════════════════
   🎬 AutoShortsFactory - 초기 설정
   (Enter 입력 시 자동 설정이 적용됩니다)
═══════════════════════════════════════════════════

1. 틈새 주제를 입력하세요 (예: IT, 과학, 역사, 먹방 / Enter=자동): IT

2. 정보 깊이를 입력하세요 (1:가볍게 ~ 5:전문적 / Enter=3): 3

3. 타겟층을 입력하세요 (예: 10대, 20대, 직장인, 전연령 / Enter=전연령):

4. 말투/스타일을 선택하세요:
   [1] 친근한 반말   [2] 정중한 존댓말
   [3] 유머러스       [4] 전문적/뉴스체
   → 선택 (Enter=1 친근한 반말): 1
```

설정은 `UserProfile.json`에 저장되며 이후 실행 시 자동 로드됩니다.

---

## 프로젝트 구조

```
AutoShortsFactory/
├── src/AutoShortsFactory/
│   ├── Models/              # 도메인 모델 (VideoProject, Topic, Script 등)
│   ├── Data/                # EF Core SQLite (중복 주제·업로드 이력)
│   ├── Services/
│   │   ├── Trend/           # 트렌드 수집 (Google/YouTube/RSS)
│   │   ├── TopicRanker/     # AI 주제 선정
│   │   ├── Script/          # AI 대본 생성
│   │   ├── Tts/             # TTS (Edge TTS)
│   │   ├── Asset/           # B-Roll (Pexels API)
│   │   ├── Subtitle/        # SRT 자막 생성
│   │   ├── Bgm/             # BGM 믹서
│   │   ├── Render/          # FFmpeg 렌더링
│   │   ├── Thumbnail/       # SkiaSharp 썸네일
│   │   ├── Quality/         # 품질 검사 (FFProbe)
│   │   ├── Metadata/        # AI 메타데이터 생성
│   │   ├── Upload/          # YouTube 업로드
│   │   └── Report/          # 일일 리포트
│   ├── Pipeline/            # 쇼츠/롱폼 파이프라인 + 오케스트레이터
│   ├── Scheduling/          # Quartz.NET Job (생성·재시도·리포트)
│   ├── Setup/               # 첫 실행 대화형 설정
│   ├── Program.cs           # 호스트 + DI + Serilog + Quartz
│   └── appsettings.json     # API 키·설정
├── logs/                    # 로그 파일 (자동 생성)
├── reports/                 # 일일 리포트 JSON (자동 생성)
├── workspace/               # 작업 파일 (TTS·에셋·영상 등)
└── bgm/                     # BGM 파일 보관
```

---

## 환경 변수

`appsettings.json` 대신 환경 변수로도 설정 가능합니다:

```bash
export OpenAI__ApiKey="sk-..."
export Pexels__ApiKey="your-key"
```

---

## 기술 스택

| 항목 | 기술 |
|------|------|
| 프레임워크 | .NET 8 (Worker Service) |
| AI/LLM | OpenAI API (GPT-4o) |
| TTS | Edge TTS (ko-KR-SunHiNeural) |
| 트렌드 | Google Trends RSS / YouTube API / 뉴스 RSS |
| 스톡 영상 | Pexels API |
| 렌더링 | FFMpegCore (FFmpeg 래퍼) |
| 썸네일 | SkiaSharp |
| 유튜브 업로드 | Google.Apis.YouTube.v3 (OAuth 2.0) |
| DB | SQLite (EF Core) |
| 스케줄링 | Quartz.NET |
| 로깅 | Serilog |

---

## 라이선스

MIT License
