# 🎬 AutoShortsFactory

AI 기반 유튜브 쇼츠 & 롱폼 영상 자동 생성 및 업로드 시스템

> .NET 8 기반으로 트렌드 수집부터 유튜브 업로드까지 전 과정을 완전 자동화합니다.

---

## 📋 주요 기능

| 기능 | 설명 |
|------|------|
| 🔍 **트렌드 자동 수집** | Google Trends, YouTube 인기 동영상, 뉴스 RSS 3개 소스 통합 수집 |
| 🤖 **AI 주제 선정** | GPT-4o가 쇼츠/롱폼 적합성 판단, 중복·민감·저작권 위험 자동 필터 |
| 📝 **AI 대본 생성** | 쇼츠(15~60초) + 롱폼 TOP10·역사/과학 해설 대본 자동 작성 |
| 🔊 **TTS 음성 생성** | Edge TTS 한국어 (`ko-KR-SunHiNeural`) 자동 음성 합성 |
| 🎬 **B-Roll 자동 다운로드** | Pexels API 키워드 기반 스톡 영상 자동 검색 및 다운로드 |
| 📌 **자막 자동 생성** | TTS 실제 재생 시간 기반 정밀 SRT 자막 타이밍 생성 |
| 🎵 **BGM 자동 믹싱** | 저작권 무료 BGM 자동 선택 및 -15~-20dB 믹싱 |
| 🎞️ **FFmpeg 렌더링** | 쇼츠(1080×1920), 롱폼(1920×1080) 자동 렌더링 |
| 🖼️ **썸네일 자동 생성** | SkiaSharp 기반 텍스트+이미지 합성 썸네일 생성 |
| ✅ **품질 자동 검사** | 검은 화면·무음 구간·해상도·길이 자동 검사 |
| 📤 **유튜브 자동 업로드** | YouTube Data API v3 OAuth 2.0 인증 + 제목/설명/태그 자동 생성 |
| 🔄 **실패 자동 재시도** | 최대 3회 지수 백오프 재시도 (1분/5분/15분) |
| 📊 **일일 리포트** | 생산/업로드 결과 요약 + JSON 파일 저장 |

---

## 🏗️ 아키텍처

```
AutoShortsFactory/
├── src/AutoShortsFactory/
│   ├── Models/              # 데이터 모델 (VideoProject, Topic, Script 등)
│   ├── Data/                # EF Core SQLite (주제 이력, 업로드 이력)
│   ├── Services/
│   │   ├── Trend/           # 트렌드 수집 (Google, YouTube, RSS)
│   │   ├── TopicRanker/     # AI 주제 선정
│   │   ├── Script/          # AI 대본 생성
│   │   ├── Tts/             # TTS 음성 생성
│   │   ├── Asset/           # B-Roll 에셋 다운로드
│   │   ├── Subtitle/        # 자막 생성
│   │   ├── Bgm/             # BGM 믹싱
│   │   ├── Render/          # FFmpeg 렌더링
│   │   ├── Thumbnail/       # 썸네일 생성
│   │   ├── Quality/         # 품질 검사
│   │   ├── Metadata/        # AI 메타데이터 생성
│   │   ├── Upload/          # 유튜브 업로드
│   │   └── Report/          # 일일 리포트
│   ├── Pipeline/            # 쇼츠/롱폼 파이프라인 + 오케스트레이터
│   ├── Scheduling/          # Quartz.NET Job (VideoCreation, Retry, Report)
│   └── Setup/               # 초기 설정 (InitialSetup)
```

---

## 🔄 파이프라인 흐름도

```
[트렌드 수집]
  Google Trends + YouTube + RSS
         ↓
[AI 주제 선정]
  GPT-4o 쇼츠화 가능성 판단 + 중복 방지
         ↓
[AI 대본 생성]
  쇼츠: 15~60초 / 롱폼: TOP10·역사·과학
         ↓
[TTS 음성 생성]
  Edge TTS (ko-KR-SunHiNeural)
         ↓
[B-Roll 다운로드]
  Pexels API 키워드 기반
         ↓
[자막 생성]
  TTS 실제 길이 기반 SRT
         ↓
[BGM 선택]
  bgm/ 폴더에서 랜덤 선택
         ↓
[FFmpeg 렌더링]
  쇼츠: 1080×1920 / 롱폼: 1920×1080
         ↓
[품질 검사]
  검은화면·무음·해상도·길이 검증
         ↓
[AI 메타데이터 생성]
  제목·설명·태그·카테고리
         ↓
[썸네일 생성]
  SkiaSharp 텍스트+이미지 합성
         ↓
[유튜브 업로드]
  YouTube API v3 OAuth 2.0
         ↓
[DB 저장 + 리포트]
```

---

## 📅 일일 생산 스케줄

| 시간 | 유형 | 수량 | 비고 |
|------|------|------|------|
| 06:00 | 쇼츠 | 1건 | 아침 트렌드 수집 |
| 10:00 | 쇼츠 | 1건 | 오전 트렌드 수집 |
| 14:00 | 쇼츠 | 1건 | 오후 트렌드 수집 |
| 20:00 | 롱폼 | 1건 | TOP10 또는 역사/과학 |
| 22:00 | 리포트 | - | 일일 생산 결과 요약 |
| 매 30분 | 재시도 | - | 실패 항목 자동 재시도 |

---

## 🔧 사전 요구사항

| 항목 | 버전 | 용도 |
|------|------|------|
| **.NET 8 SDK** | 8.0+ | 런타임 |
| **FFmpeg** | 6.0+ | 영상 렌더링 및 품질 검사 |
| **Python 3** | 3.8+ | Edge TTS 실행 |
| **edge-tts** | 최신 | 한국어 TTS 생성 |
| **Google OAuth** | - | `client_secret.json` 필요 |
| **OpenAI API Key** | - | GPT-4o 사용 |
| **Pexels API Key** | - | 스톡 영상 다운로드 |

---

## 🚀 설치 가이드

### 1. 저장소 클론

```bash
git clone https://github.com/juisakki/AutoShortsFactory.git
cd AutoShortsFactory
```

### 2. Python edge-tts 설치

```bash
pip install edge-tts
```

### 3. FFmpeg 설치

**Windows:**
```powershell
winget install FFmpeg
```

**macOS:**
```bash
brew install ffmpeg
```

**Linux (Ubuntu/Debian):**
```bash
sudo apt install ffmpeg
```

### 4. 프로젝트 빌드

```bash
dotnet build src/AutoShortsFactory/AutoShortsFactory.csproj
```

---

## 🔑 API 키 설정

### appsettings.json 수정

```json
{
  "OpenAI": {
    "ApiKey": "sk-...",
    "Model": "gpt-4o"
  },
  "Pexels": {
    "ApiKey": "your-pexels-api-key"
  }
}
```

### Google OAuth 설정 (`client_secret.json`)

1. [Google Cloud Console](https://console.cloud.google.com/) 접속
2. 새 프로젝트 생성 또는 기존 프로젝트 선택
3. **API 및 서비스** → **사용 설정된 API** → **YouTube Data API v3** 사용 설정
4. **OAuth 동의 화면** 설정
5. **사용자 인증 정보** → **OAuth 2.0 클라이언트 ID** 생성 (데스크톱 앱)
6. `client_secret.json` 다운로드 → 프로젝트 루트에 저장

---

## ▶️ 첫 실행

```bash
cd src/AutoShortsFactory
dotnet run
```

**첫 실행 시 초기 설정 화면이 나타납니다:**

```
═══════════════════════════════════════════
   🎬 AutoShortsFactory - 초기 설정
═══════════════════════════════════════════
1. 틈새 주제 (예: IT, 과학, 역사, 먹방...)
   → 입력 또는 Enter(자동): IT

2. 정보 깊이 (1:가볍게 ~ 5:전문적)
   → 입력 또는 Enter(자동=3): 3

3. 타겟층 (예: 10대, 20대, 직장인, 전연령...)
   → 입력 또는 Enter(자동=전연령): 20대

4. 말투/스타일
   [1] 친근한 반말  [2] 정중한 존댓말
   [3] 유머러스    [4] 전문적/뉴스체
   → 선택 또는 Enter(자동=1): 1
═══════════════════════════════════════════
```

설정은 `UserProfile.json`에 저장되어 다음 실행 시 자동 로드됩니다.

---

## 📁 폴더 구조 설명

| 폴더 | 설명 |
|------|------|
| `output/` | 완성된 MP4 영상 + 썸네일 저장 |
| `temp/` | TTS 오디오, B-Roll 등 임시 파일 |
| `bgm/` | BGM MP3 파일 저장 (직접 추가) |
| `fonts/` | 자막용 폰트 파일 저장 |
| `reports/` | 일일 리포트 JSON 파일 저장 |
| `logs/` | Serilog 로그 파일 |

> ⚠️ `bgm/` 폴더에 `.mp3` 파일을 직접 추가해야 BGM이 적용됩니다. (저작권 무료 음악 사용)

---

## ⚙️ appsettings.json 설명

| 설정 키 | 설명 | 기본값 |
|---------|------|--------|
| `OpenAI:ApiKey` | OpenAI API 키 | 필수 입력 |
| `OpenAI:Model` | 사용할 GPT 모델 | `gpt-4o` |
| `YouTube:ClientSecretPath` | OAuth 클라이언트 시크릿 파일 경로 | `client_secret.json` |
| `Pexels:ApiKey` | Pexels API 키 | 필수 입력 |
| `EdgeTts:Voice` | TTS 음성 이름 | `ko-KR-SunHiNeural` |
| `EdgeTts:PythonPath` | Python 실행 경로 | `python` |
| `Schedule:ShortsCron` | 쇼츠 생성 Cron | `0 0 6,10,14 * * ?` |
| `Schedule:LongFormCron` | 롱폼 생성 Cron | `0 0 20 * * ?` |
| `Retry:MaxRetries` | 최대 재시도 횟수 | `3` |
| `Retry:BackoffSeconds` | 재시도 대기 시간(초) | `[60, 300, 900]` |

---

## ❓ 문제 해결 FAQ

**Q: `client_secret.json` 파일이 없다는 오류가 나옵니다.**
> A: Google Cloud Console에서 OAuth 2.0 클라이언트 ID를 생성하고 JSON 파일을 프로젝트 루트에 저장하세요.

**Q: TTS 음성이 생성되지 않습니다.**
> A: `pip install edge-tts` 로 edge-tts를 설치하고, `appsettings.json`의 `EdgeTts:PythonPath`가 올바른지 확인하세요.

**Q: FFmpeg 오류가 발생합니다.**
> A: FFmpeg가 시스템 PATH에 등록되어 있는지 확인하세요. `ffmpeg -version` 명령으로 테스트합니다.

**Q: 품질 검사에서 자꾸 실패합니다.**
> A: 쇼츠는 15~60초, 롱폼은 3~15분 분량의 영상만 통과합니다. BGM 또는 TTS 오디오가 올바르게 생성되었는지 확인하세요.

**Q: 유튜브 업로드 시 OAuth 인증 창이 나타납니다.**
> A: 첫 실행 시 브라우저 인증 창이 열립니다. 유튜브 계정으로 로그인하면 토큰이 로컬에 저장되어 이후에는 자동으로 인증됩니다.

**Q: OpenAI API 키를 설정하지 않으면 어떻게 되나요?**
> A: API 키 없이도 실행 가능합니다. 이 경우 샘플 대본과 기본 메타데이터가 사용됩니다.

---

## 📜 라이선스

본 프로젝트는 개인 자동화 용도로 제작되었습니다.

- **저작권**: 생성된 콘텐츠의 저작권 및 저작권 문제는 사용자 책임입니다.
- **API 이용약관**: OpenAI, YouTube Data API, Pexels API의 이용약관을 준수하여 사용하세요.
