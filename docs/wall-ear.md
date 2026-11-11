# 壁に耳あり

クルーメイト役職。NoSのマッドメイトへの配役にも対応する。

設置オブジェクト・能力ボタン・役職アイコンは、肌色の人間の耳の透過画像 `addon/Resources/WallEar.png` を共用する。画像は256×256で、外側の輪郭・内側のひだ・耳たぶを描く。[制作プロンプト](design/wall-ear-sprite.md)。使用中の色分けはスプライトへの色付けで行う。

人間の耳の画像が能力ボタンと設置オブジェクトに表示されることをNoS上で確認済み。[設置状態の実ゲーム画像](testing/2026-11-10/wall-ear-human-placed-game.png)。

## 操作と設定

能力ボタン「耳を設置」でその場に耳を置く。自分が置いた耳から1.2以内に近づいてクリックすると、耳の周囲の声を壁越しに聞ける。
使用中は耳が緑色になり、移動できなくなる。同じ耳をもう一度クリックすると解除する。
耳は設置者だけに表示され、設置者だけが使用できる。同時に使う耳は1つ。

| 設定 | 範囲 | 初期値 |
| --- | --- | --- |
| 耳の最大設置数 | 1～10 | 3 |
| 耳を設置するクールダウン | 0～120秒、5秒刻み | 15秒 |
| 耳から聞こえる範囲 | 1～10、0.5刻み | 4 |

使用中や最大設置数に達した場合は追加設置できない。
会議では使用だけを終了し、耳は残す。死亡・切断・役職解除・ゲーム終了で撤去する。
タスク画面等で移動できなくなった場合や耳から離れた場合にも使用を終了する。
移動停止属性は使用開始時に一度だけ付け、終了時に除去する。毎フレームの属性RPCは送らない。

## 音声

聞く位置を耳へ切り替え、壁による遮音だけを解除する。本人の送話位置は変えない。
距離と左右方向は耳が基準。耳の使用中はカメラ受信やメガホンの距離拡張で耳の範囲外まで聞くことはない。
元々の無線受信権限は通常通りで、通常聞こえない幽霊の声や他陣営の非公開無線を盗聴できるようにはしない。
マイク・受信側のミュート、通信妨害、死者専用通話などの既存制限を維持する。

## ベタクル連携

通常版とLite版は `C:/Users/tamo-/TanukiBCL-hero-speech/repo` の共通実装、
.NET版は `D:/VSCode/TanukiBCL-net-hero-speech`、Web・モバイル版は
`C:/Users/tamo-/TanukiBCL-mobile-hero-effects` に追加した。
既存環境を使用し、新たな全体コピーやworktreeは作成していない。

共通接続に省略可能な `ears` チャンネルを追加。使用中のプレイヤーごとに `{ playerId, x, y, range }` を送る。
停止は空配列／該当行の削除で通知する。従来の9チャンネルも受信でき、耳の省略は盗聴解除として扱う。
共通接続がない場合は `ears-<PID>.json` を利用する。PID・時刻・期限・座標・範囲・人数・ID重複を検証する。
Web・モバイルはPCまたは.NETホストのゲーム状態 `nosWallEars` を受信する。
受信側でも会議・死亡・切断・ベント・耳からの距離を確認し、古い状態による遠隔聴取を防ぐ。
録音ファイルや追加の音声送信経路は作らない。
今回追加したコードを含む開発版のベタクルが必要。従来の公開版だけでは音声に反映されない。

## 確認状況（2026-11-10）

- NoSコンパイル、設置・再送拒否・距離・上限・会議・撤去の状態テスト。
- PC/Lite・Web/モバイル・.NETの実音声ポリシーで、Fungleの実際の遮音壁を使った通常遮音／耳による解除と、範囲・幽霊・通信制限を確認。
- PC/.NETからWeb・モバイルに渡すゲーム状態を確認。
- アドオンTCPサーバー2プロセスとPC/.NETの実クライアントで、耳の同期・旧形式互換・空状態の解除を確認。
- 各版の型・コンパイル確認。追加コードの制御ネストは3以下。
- .NET全体のネスト検査には、今回変更していない `src/TanukiBCL.Client/ClientSettings.cs` の `ClientSettingsStore.Load` に既存の深さ4の指摘が1件ある。
- NoSの1人フリープレイで、設置・クリック使用・実際の移動停止と解除、音声連携への座標反映、役職変更時の撤去を確認。実際の聴感・複数端末での実プレイは未確認。
- β19の公開前に開発版をゲームへ配置して検証した。[検証結果](testing/2026-11-10/README.md)を参照。

検証:

```text
# アドオン
dotnet run --project tests/GateTests.csproj -- --wall-ear-test
dotnet run --project tests/AddonLink/AddonLinkTests.csproj

# PC/Lite
node scripts/test-wall-ear.mjs
node scripts/test-nos-addon-link.mjs D:/VSCode/TanukiBCL-NoS-Addon/tests/AddonLink/bin/Debug/net8.0/AddonLinkTests.dll
npm run typecheck
npm run check:nesting

# .NET
dotnet run --project src/TanukiBCL.VoiceProbe -- --wall-ear-self-test
dotnet run --project src/TanukiBCL.VoiceProbe -- --addon-link-self-test D:/VSCode/TanukiBCL-NoS-Addon/tests/AddonLink/bin/Debug/net8.0/AddonLinkTests.dll

# Web・モバイル
node node_modules/typescript/bin/tsc --noEmit -p tsconfig.app.json
```


配布版：タヌキ丼β19、PC通常版／Lite3.2.64、.NET3.2.64-net-beta.1、Web／モバイル3.60。
