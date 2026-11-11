# クローン（β19）

役職と、PC通常版／Lite共通・.NET版・Web／モバイル版の録音再生を実装した。NoSの既存の偽プレイヤーと経路歩行を使う。β19として配布する。

NoS上の生成・自動歩行・再録音での撤去・再召喚・本人死亡時の撤去と、各版のテスト音声による録音再生を確認した。実マイク・複数人でのキルや聴感は未確認。[検証結果](testing/2026-11-10/clone.md)。

## 確定した仕様

- クルーメイト版とマッドメイト版の両方に対応する。
- 本人の声を10秒間録音する。録音し直すことができる。
- 録音済み音声を繰り返し話すクローンを召喚する。
- クローンの姿と名前は本人と同じ。同時に召喚できるのは本人につき1体。
- クローンはマップを自動で歩き続ける。
- 会議に入るか、他プレイヤーにキルされるとクローンが消滅する。
- 消滅後にクールタイムを開始し、終了後に再召喚できる。

## 実装

- 録音と召喚のボタンを分ける。録音中は残り10秒のカウントを表示する。
- クルーメイト役職として定義し、NoSのマッドメイトへの割り当てに対応する。
- 再召喚のクールタイムはロビー設定で変更可能にする。
- 再召喚クールタイムは0〜120秒、5秒刻みで初期値30秒。音声範囲は1〜10、0.5刻みで初期値3.5。録音は10秒固定。
- クローンの発声位置はクローンの現在位置。既存の距離減衰・受信音量を適用する。
- 参加者数、投票枠、タスク数、勝利判定には数えない。本体へのキルと区別し、クローンのキルで本体を死亡させない。
- NoS既存の偽プレイヤー生成・キル判定・位置同期・経路移動を再利用する。
- 移動先の選択と経路探索は一定間隔で行い、全端末で毎フレーム探索しない。
- 会議開始、ゲーム終了、役職解除時には移動とループ音声の両方を停止する。
- ベタクルの録音・位置付き再生を共通化して利用し、アドオンから共通ローカル接続経由で録音ID、クローンID、位置、再生状態を送る。
- 通常版/Lite版、.NET版、Web・モバイル版の受信・再生も必要。音声本体をローカル状態JSONやDBへ保存しない。
- ソース・依存ライブラリ・音声認識モデル・配布物を新たな作業コピーへ丸ごと複製しない。

## 発話表示の偽装

- ロビーのクローン設定「クローンの発声を本人の発話表示に偽装する」で切り替える。初期値はオン。クルーメイト・マッドメイトの両配役に適用する。
- オンでは、クローンの録音音声が実際に聞こえる端末だけで、本人のベタクルの発話枠を光らせる。本人の位置や現在のマイク入力とは別に、録音の再生位置のPCMを判定する。
- 無音部分、範囲外、遮蔽物、受信ミュート・音量ゼロ・通信妨害、会議開始、消滅時には偽装表示を消す。無音の判定は短い120msの窓で行う。
- オフでも録音と再生は続く。本物のマイクの発話表示は通常どおり動作する。通常のオウムは偽装しない。
- 同じ parrots 状態に spoofTalking の真偽値を追加した。古い状態で項目が省略されていた場合は偽装しない。各端末の表示専用であり、サーバーへ偽のVADを送信しない。
- PC通常版／Lite共通、Web／モバイル版、.NET版に対応。対応する開発版のベタクルとアドオンが必要。

## 中断・上書き・音声連携

- 会議途中の未完成録音は破棄する。完成済み録音は会議後も使える。
- 本人の死亡・切断、試合終了、役職解除では分身と録音状態を解放する。
- 再録音時は存在中のクローンを消して、前の録音を上書きする。消滅時から再召喚のクールタイムを数える。
- 移動先はNoSの経路データから選ぶ。到着後は次の目的地へ移動する。経路探索の試行回数と再試行間隔を制限し、制御者だけが歩行を実行する。
- 音声は既存の `parrots` 連携に `mode: "clone"` を付けて共有する。クローン専用では本人の声・10秒固定・完成済み1件だけを受け付け、自動録音要求は送らない。
- 同じPCMをクライアント内でループするため、ループごとのRPCや録音バッファのコピーは不要。移動中は分身座標に合わせて距離減衰と左右方向を更新する。ミュート・音量・通信妨害などの既存制限も引き継ぐ。
- 内部機能を再利用するため `UseHiddenMembers=true` とした。生成・歩行の内部型への依存は `CloneRuntime.cs` にまとめた。アドオンとベタクルの対応する開発版の両方が必要。
- 録音時に未接続だった端末には、その録音を後から復元しない。DBや録音ファイルは追加していない。

## NoS API調査（2026-11-09）

手元のNoSソースと公式masterを調査した。

- `Virial.Game.IFakePlayer`、`IPlayerlike.GetPlayerlike`、偽プレイヤーのキルイベントは公開されている。
- 生成側の `Nebula.Player.FakePlayerController` と `FakePlayerParameters` はinternal。
- `FakePlayerController.SpawnSyncFakePlayer` でネットワーク同期した偽プレイヤーを作れるが、通常のアドオンから生成側の型を利用できない。
- `NavVerticesHelpers.CalcPath` は公開されているが、移動側の `WalkPath` と `IPlayerLogics` はinternal。
- NoSの既存ドッペルゲンガー役職は、この生成・移動処理を使用している。
- 現在のアドオン設定は `UseHiddenMembers=false`。調査のためにこの設定を変更していない。

参考:
https://github.com/Dolly1016/Nebula-Public/blob/master/NebulaPluginNova/Player/FakePlayer.cs
https://github.com/Dolly1016/Nebula-Public/blob/master/NebulaPluginNova/Utilities/Pathfinding.cs
https://github.com/Dolly1016/Nebula-Public/blob/master/NebulaPluginNova/Roles/Crewmate/Doppelganger.cs

## 開発者の案内を受けた再調査（2026-11-10）

Doppelganger／Sculptorを参考にすればよい、という開発者の案内を受け、公式masterの両役職と偽プレイヤー・経路移動の実装を再確認した。

- 既存の偽プレイヤー生成、外見・名前、位置同期、経路歩行、会議時の撤去は流用候補になる。一から歩行キャラクターを作る必要はない。
- 今回のクローンには、Sculptorの `KillCharacteristics.Disappear` が合う。Doppelgangerの `KillAllAndLeaveBodyOne` をそのまま使うと、本体も巻き込む挙動になるため採用しない。
- 既存役職は目的地を指定して歩かせる。目的地の自動選択・到着後の次の移動・行き止まり時の再選択は、今回の自動徘徊のために追加する。
- 10秒録音、再録音、位置付きループ再生とベタクル各版への連携は追加実装が必要。
- インストール済みNoS v3.7.1.1の参照で小さなコンパイル確認を実施した。`UseHiddenMembers=false` では、`FakePlayerController`、`FakePlayerParameters`、`NavVerticesHelpers.WalkPath` の3件がCS0122となった。[確認用ソースと結果](../tests/CloneApiProbe/README.md)。

この再調査では、既存処理を再利用する実装経路と公開範囲を分けて確認した。その後のユーザーの実装指示を受け、内部機能を使う構成で役職を実装した。`UseHiddenMembers=true` の確認用ソースと本体はコンパイル成功。公開リリースは行っていない。

公式参照:

- https://github.com/Dolly1016/Nebula-Public/blob/master/NebulaPluginNova/Roles/Crewmate/Doppelganger.cs
- https://github.com/Dolly1016/Nebula-Public/blob/master/NebulaPluginNova/Roles/Impostor/Sculptor.cs
- https://github.com/Dolly1016/Nebula-Public/blob/master/NebulaPluginNova/Player/FakePlayer.cs
- https://github.com/Dolly1016/Nebula-Public/blob/master/NebulaPluginNova/Utilities/Pathfinding.cs

## 公開APIの追加要望文（未投稿）

アドオンから「本人と同じ姿・名前の偽プレイヤー」を1体召喚し、マップ内を自動で歩かせる役職を作成したいです。
偽プレイヤーはキル対象になり、キルされると本体に影響せず消滅します。会議開始や役職解除時にも解放します。
位置は連動するボイスチャットへ渡し、その地点から録音音声を再生する用途です。

現在はIFakePlayerの参照やキルイベントを利用できますが、生成処理のFakePlayerController/FakePlayerParametersと、
経路移動のWalkPath/IPlayerLogicsがinternalのため、UseHiddenMembers=falseのまま生成・移動できる入口が見つかりませんでした。

以下を公開APIとして利用できると助かります。

1. 外見の元プレイヤー、生成位置、キル時に本体を巻き込まない特性、寿命を指定した偽プレイヤーの生成。
2. IFakePlayerを指定した目的地までNoSの経路探索で歩かせる処理と、移動のキャンセル。
3. 現在の位置・有効状態の取得、会議開始時や能力解除時の明示的な解放。

既存のドッペルゲンガー等と同じ位置同期・キル判定・マップの移動設備に対応でき、
アドオンごとに内部実装や経路データを複製しなくて済む形を希望します。


配布版：タヌキ丼β19、PC通常版／Lite3.2.64、.NET3.2.64-net-beta.1、Web／モバイル3.60。
