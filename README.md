# M3U Radio Player for Android

[English]  
A lightweight, high-performance internet radio player for Android built with **.NET 10** and **Avalonia UI**.  
Powered by the native **ManagedBass (BASS)** audio engine, it provides robust background streaming, low-latency playback, and seamless edge-to-edge support across legacy devices (Android 9.0) up to modern releases (Android 17 / Target SDK 36).

[日本語]  
**.NET 10 (Android)** と **Avalonia UI** で構築された、Android 向けの軽量・高音質ネットラジオプレーヤーです。  
**ManagedBass (BASS)** ネイティブオーディオエンジンを採用し、Android 9 のレガシー環境から最新の Android 17 (Target SDK 36) まで、安定したバックグラウンド再生と快適な操作性を提供します。

---

## Features / 主な特徴・機能

* **High-Quality Audio Streaming / 高音質ストリーミング再生**
  * Native hardware-accelerated decoding for MP3, AAC, and FLAC streams via `libbassflac.so`.
  * MP3、AAC、および `libbassflac.so` による FLAC ネイティブ再生に対応。

* **Local HTTP Loopback Proxy / ローカル中継プロキシ機構**
  * Embedded internal `HttpListener` (`127.0.0.1:51234`) bypasses Android 9 legacy root CA expirations and strict Android 17 TLS/SSL negotiation failures (`BASS_ERROR_SSL` / Error 10).
  * Relays `Content-Type` headers alongside `Icy-MetaData: 1` to deliver flawless FLAC decoding and real-time ICY title streaming concurrently.
  * アプリ内ループバックプロキシにより、古い証明書の失効や最新 OS の SSL/TLS ネゴシエーション失敗を根本回避。ストリームの安定受信と FLAC デコード、ICY メタデータ取得を両立。

* **Real-Time Metadata Display / リアルタイム曲名表示**
  * Automatically parses ICY tags (Shoutcast/Icecast) and Ogg/FLAC Vorbis Comments to display streaming song titles and artists in real time.
  * Shoutcast/Icecast の ICY タグや Ogg/FLAC Vorbis Comment から曲名・アーティスト情報をリアルタイム抽出して画面・通知に表示。

* **Audio Interruption & Focus Management / 割り込み処理・フォーカス制御**
  * **Headphone Unplug (`BecomingNoisy`)**: Pauses playback instantly when wired headphones are unplugged or Bluetooth audio disconnects to prevent accidental speaker blasting.
  * **Call & Ringtone Handling**: Pauses during incoming phone calls—including silent/manner mode calls—and active phone conversations, resuming playback automatically after hanging up.
  * イヤホン切断時の自動停止、および通常着信・マナーモード着信・通話中の一時停止と終話後の自動復帰に対応。

* **Notification Controls / 通知バーからの直接操作**
  * Foreground playback service features an integrated "Stop" action button in the notification shade, allowing instant one-tap stopping from the lock screen without reopening the app.
  * 再生中通知に「停止」ボタンを配置し、ロック画面や通知シェードからワンタップで即座に完全停止可能。

* **Edge-to-Edge & Dark Theme / モダン UI 最適化**
  * Native `WindowInsetsCompat` handling prevents UI overlaps with system status bars and 3-button navigation bars on Android 14, 15, and 16+.
  * Fully unified dark theme palette (`#1E1E1E`).
  * AndroidX の WindowInsets によりステータスバーやナビゲーションバーとの被りを解消。アプリ全体をダークテーマで統一。

* **M3U Playlist Scanner / 柔軟な M3U プレイリスト読み込み**
  * Automatically discovers `.m3u` files in internal storage (`/storage/emulated/0/Playlists`) and external SD cards.
  * Full compatibility with Android 11+ Scoped Storage via `MANAGE_EXTERNAL_STORAGE`.
  * 端末内ストレージや外部 SD カード内の `Playlists` フォルダから `.m3u` ファイルを自動検出。

---

## Requirements / 動作環境

* **Target Device**: Android 9.0 (API 28) ~ Android 17 (API 36) (ARM64-v8a / ARMeabi-v7a)
* **Build Environment**: .NET 10 SDK with Android Workload (`dotnet workload install android`)

---

## Project Structure / プロジェクト構成

```text
M3URadioPlayer.Android/
├── lib/
│   ├── arm64-v8a/        # libbass.so, libbassflac.so
│   └── armeabi-v7a/      # libbass.so, libbassflac.so
├── Resources/
│   └── mipmap-xxxhdpi/   # ic_launcher.png
├── MainActivity.cs       # Permissions, Lifecycle & WindowInsets
├── MainWindow.axaml(.cs) # UI, Playback controls, Local Proxy & About dialog
├── RadioPlaybackService.cs # Foreground Service, AudioFocus, Telephony & Notification
├── my-release-key.keystore # Release signing keystore (git ignored)
├── LICENSE               # MIT License & Third-party notices
└── M3URadioPlayer.Android.csproj
```

---

## Build & Run / ビルド・実行手順

### 1. Release Build (Signed APK) / 署名ビルド

```powershell
dotnet publish -c Release -f net10.0-android -p:AndroidSigningKeyPass="YOUR_KEYSTORE_PASSWORD" -p:AndroidSigningStorePass="YOUR_KEYSTORE_PASSWORD"
```

The signed APK will be generated at:  
署名済み APK は以下のパスに出力されます：  
`bin/Release/net10.0-android/publish/com.barippi.m3uradioplayer-Signed.apk`

### 2. Install to Device / 実機インストール

```powershell
adb install -r "bin/Release/net10.0-android/publish/com.barippi.m3uradioplayer-Signed.apk"
```

---

## Credits & Licenses / ライセンス情報

* **Application Code**: Licensed under the [MIT License](LICENSE).  
  Copyright (c) 2026 Barippi.
* **[Avalonia UI](https://github.com/AvaloniaUI/Avalonia)**: MIT License.  
  Copyright (c) .NET Foundation and Contributors.
* **[ManagedBass](https://github.com/Japyna/ManagedBass)**: MIT License.  
  Copyright (c) Mathew Sachin.
* **[BASS Audio Library](https://www.un4seen.com/)**: Copyright (c) 1999-2026 Un4seen Developments Ltd.  
  Used under the BASS freeware license for non-commercial software.