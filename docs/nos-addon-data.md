# NoS AddonDataと共通ベタクル連携（β18）

## 方針と確認範囲

添付された公開API `TBCLEvent.UpdatePlayerData(byte,int)` / `UpdateSnapshotData(int)` と、TBCLスナップショットの `AddonData` を共通の入口にする。
整数だけでは座標一覧、録音時刻、単語、音声認識の戻り通知を運べないため、全体の整数に端末内TCPポートを公開し、詳細な状態と戻り通知は一本のループバック接続へ集約する。外部サーバーや録音データのDB格納は追加しない。

2026-11-04時点の手元のNoS DLLと、前回取得した公式masterには新APIがない。添付の公開メソッド形状に基づく互換実装、模擬イベント・メモリ読み取り、実際のTCP接続を使う結合テストまで実施。新NoS実機でのイベント発火・構造体配置・複数アドオン併用は未確認。正確な新スキーマ番号は推測して固定していない。

## 状態と戻り通知の統合

| 共通チャンネル | 内容 |
| --- | --- |
| heroes | スーパーヒーロー／イーヴィルヒーローの変身状態 |
| portable | 携帯メガホンの使用中状態・追加範囲 |
| megaphones | メガホンの蓄積量・範囲、バズーカのチャージ／発射状態 |
| dying | ダイイングボイスの録音時刻・秒数・位置・範囲・ループ |
| interviews | インタビュアーの録音・停止・視認対象・会議再生時刻 |
| jamming | ボイスジャミンガーのミュート・ローパス・ピッチ |
| echoes | ビビリ・携帯メガホン死亡時のエコーイベント |
| muted | 発言時間制限ミュートの予約チャンネル（今回の配布では空） |
| speech | ヒーロー／NGワードの認識要求・単語・期限 |

戻り通知は `sample`（本人と、ホストが確認したWeb・モバイルの音量・発話状態・バージョン）、`ready`（認識準備）、`word`（認識結果）の3種類。認識のPC各自／Web・モバイルのみホスト分担は維持する。音声のPCM・録音再生・通話のP2P経路は既存機構を使い、この接続には音声本体を流さない。役職のRPC同期も維持する。

## アドオン側

`AddonLinkRuntime` がローカルプレイヤーとゲームの寿命ごとに `AddonLinkServer` を持つ。ロビーにも対応し、退出・プレイヤー変更・ラウンド変更時は接続と状態を破棄する。通信スレッドはJSONを検証して上限付きキューに積み、ゲームスレッドだけが役職・Unity APIを操作する。キューから取り出す時点でも通知の期限を確認する。

各役職の従来の発行データを `AddonLinkState` に集め、全9チャンネルを100msごとに一つの完全なスナップショットとして送る。無効化は明示的な空配列で通知。一般の状態は発行が1秒途切れると解除し、エコーイベントは3秒で失効。発言時間制限のミュートは、発行が途切れただけで解除しない。明示的な空配列またはゲーム終了で解除する。

`TanukiAddonDataBridge` は起動中のNebulaAPI／Nebulaアセンブリの公開型からイベントと公開メソッドを検出する。ゲームの寿命に紐付けて購読し、一度作ったデリゲートで更新する。隠しメンバーやNoSプロセスメモリへの書き戻しは行わず、`UseHiddenMembers=false` を維持。

## 整数の形式

新方式の全体 `AddonData = 0x54420000 | TCPポート`（ポート1〜65535）。プレイヤーは上位16ビット `0x544C`。

| 下位ビット | 用途 |
| --- | --- |
| 0〜1 | 0:未変身、1:スーパーヒーロー、2:イーヴィルヒーロー。3は無効 |
| 2〜7 | 携帯メガホン追加範囲×2（0〜60、0.5刻み） |
| 8 | 携帯メガホン使用中。追加範囲0でも使用中を表せる |
| 9 | 発言時間制限ミュート |
| 10〜12 | ボイスジャミンガーのミュート／ローパス／ピッチ変更 |
| 13〜15 | メガホン／ダイイングボイス／インタビュアー情報あり |

ビット9〜15は概要・診断用。時刻・対象・解除を含む完全な効果は共通状態を使用し、概要だけで録音や妨害を再構成しない。変身と携帯メガホンは従来通り整数からも復元できる。旧方式の全体 `0x544B0001`、プレイヤー `0x544B0000`（ビット9〜15は0のみ）も受信可能。

AddonDataはアドオン共用整数であり、識別子は所有権を保証しない。他アドオンに上書きされて一致しなくなった場合、その値は解釈しない。同じPIDの共通接続案内へ切り替えられる。

## 接続・検証・互換性

- 新NoSではメタデータで解決したAddonDataオフセットを読み、公開ポートを優先する。x86/x64の構造体サイズ・境界、全体／プレイヤーのオフセット対、書換え途中の読み取りも検証する。
- 新APIがないNoSやロビーでは `%LOCALAPPDATA%/TanukiBCL/NoS/addon-link-<PID>.json` 一つから同じ接続へ入る。案内は1秒更新、2.5秒失効。従来の役職別ファイルを常時走査しない。
- TCPは127.0.0.1にだけ接続。`hello` でプロトコル1・PID・32桁nonceを照合し、ゲームごとの32桁sessionを受け取る。以後はPID・session・増加シーケンス・時刻を照合。古い接続・別プロセス・再送を混同しない。これは端末内の取り違え防止であり、同一ユーザーの任意プログラムに対する権限分離ではない。
- JSON行は128KiB未満、JSON深さ16（C#）、1回の読取で64行、接続最大4、ゲーム側キュー128、1フレームの処理64まで。通知は500msで失効、未来の許容100ms。受信状態は700msで失効する。
- 全チャンネル・ID重複・値域・型を一括検証してから採用。部分的に壊れた状態を混ぜない。正常な空配列は古いファイルの状態へ戻さず、そのまま解除として採用する。
- クライアントが検証済みの新しい状態を受信確認している間だけ、役職別JSON状態の継続書込を抑える。接続を開いただけでは抑えない。確認が500ms途切れるとアドオンは従来出力を再開する。旧BCL用のUDP窓口も残す。
- 通常版・Lite版は同じElectron実装、.NET版は同じプロトコルを使う。.NET版はメモリ読取500msとは独立にTCPを100msで更新する。Web・モバイルはホストが取得した既存のゲーム状態形式を利用し、PIDやTCPポートを配布しない。

## 開発元と検証コマンド

アドオンはこのリポジトリ。PCは `C:/Users/tamo-/TanukiBCL-hero-speech/repo`、.NETは `D:/VSCode/TanukiBCL-net-hero-speech`、Web・モバイルは `C:/Users/tamo-/TanukiBCL-mobile-hero-effects` の作業ツリー。β18 / PC 3.2.62 / .NET 3.2.62-net-beta.1 / Web・モバイル3.58のリリース用作業ツリーで構成を確認する。

```text
dotnet run --project tests/AddonData/AddonDataTests.csproj
dotnet run --project tests/AddonLink/AddonLinkTests.csproj
dotnet run --project tests/GateTests.csproj

# PC作業ツリー（結合テストには上のAddonLinkTestsビルドが必要）
node scripts/test-nos-addon-data.mjs
node scripts/test-nos-addon-link.mjs D:/VSCode/TanukiBCL-NoS-Addon/tests/AddonLink/bin/Debug/net8.0/AddonLinkTests.dll
npm run typecheck
npm run check:nesting

# .NET作業ツリー
dotnet run --project src/TanukiBCL.VoiceProbe -- --addon-link-self-test D:/VSCode/TanukiBCL-NoS-Addon/tests/AddonLink/bin/Debug/net8.0/AddonLinkTests.dll
dotnet run --project src/TanukiBCL.VoiceProbe -- --nos-snapshot-self-test
```

結合テストはアドオン本体と同じサーバーソースで2プロセスを実行し、PC/.NETの実クライアントから全チャンネル、認識結果・音量の戻り、PID分離、空状態の解除、不完全／期限切れ状態の拒否、接続先選択を検証する。アドオンは `nos_check`、既存の認識・録音・ジャミング・ヒーロー・NGワードの回帰テストも実行する。
