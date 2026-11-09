# M3U Radio Player for Android

.NET 10 (Android) と Avalonia UI で構築された、クロスプラットフォーム対応のネットラジオプレーヤーです。  
ManagedBass (BASS) エンジンを採用し、Android 9 のレガシー環境から Android 17 (Target SDK 36) の最新 OS まで安定したバックグラウンド再生を提供します。

---

## 主な特徴・機能

* **高音質ストリーミング再生**
  * MP3、AAC、FLAC（`libbassflac.so`）の高音質ネイティブデコードに対応。
* **ローカル HTTP ループバック中継プロキシ**
  * アプリ内 `HttpListener`（`127.0.0.1:51234`）経由でストリームを受信。
  * Android 9 でのルート証明書失効や Android 17 での SSL/TLS ネゴシエーション失敗（`BASS_ERROR_SSL` / エラー 10）を根本回避。
  * `Content-Type` と `Icy-MetaData: 1` を中継し、FLAC デコードと ICY 曲名表示を両立。
* **リアルタイムメタデータ表示**
  * ICY タグ（Shoutcast/Icecast）および Ogg/FLAC Vorbis Comment から曲名・アーティスト名をリアルタイムに抽出・表示。
* **フォアグラウンドサービスによるバックグラウンド再生**
  * Android 14+ / Android 17 の厳格なサービス起動要件に対応（`ForegroundServiceType.MediaPlayback`）。
  * 画面オフ時や他アプリ利用時でも途切れず再生を維持。
* **柔軟な M3U プレイリスト読み込み**
  * 本体ストレージ（`/storage/emulated/0/Playlists` 等）および外部 SD カード内の `.m3u` を自動スキャン。
  * Android 11〜13+ の Scoped Storage に対応し、起動時に `MANAGE_EXTERNAL_STORAGE`（全ファイルアクセス権）を自動案内。

---

## 必要要件

* .NET 10 SDK（Android ワークロード導入済み）
* Android デバイス / エミュレータ（Android 9.0 / API 28 〜 Android 17 / API 36）
* 署名用キーストアファイル（`my-release-key.keystore`）

---

## プロジェクト構成

```text
M3URadioPlayer.Android/
├── lib/
│   ├── arm64-v8a/        # libbass.so, libbassflac.so
│   └── armeabi-v7a/      # libbass.so, libbassflac.so
├── Resources/
│   └── mipmap-xxxhdpi/   # ic_launcher.png (アプリアイコン)
├── MainActivity.cs       # 権限チェック・ライフサイクル制御
├── MainWindow.axaml(.cs) # UI・再生制御・ローカルプロキシ・タグ解析
├── RadioPlaybackService.cs # フォアグラウンド再生サービス・通知管理
├── my-release-key.keystore # リリース署名用キーストア
└── M3URadioPlayer.Android.csproj
```

---

## ビルド ＆ インストール

### リリースビルド（署名済み APK）

```powershell
dotnet clean
dotnet publish -c Release -f net10.0-android
```

生成パス:  
`bin\Release\net10.0-android\publish\com.barippi.m3uradioplayer-Signed.apk`

### 実機インストール

```powershell
adb install -r "bin\Release\net10.0-android\publish\com.barippi.m3uradioplayer-Signed.apk"
```

---

## Credits & Licenses

* **Application Code**: Licensed under the [MIT License](LICENSE).
* **[Avalonia UI](https://github.com/AvaloniaUI/Avalonia)**: MIT License.
* **[ManagedBass](https://github.com/Japyna/ManagedBass)**: MIT License.
* **[BASS Audio Library](https://www.un4seen.com/)**: Copyright (c) Un4seen Developments Ltd.  
  This software uses the BASS audio library and BASS FLAC add-on for non-commercial playback.