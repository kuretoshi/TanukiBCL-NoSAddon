# ヒーローコスチューム v2

利用者から提供されたNoSのHat・Visor規格、300×375pxの通常・梯子参考画像、`Contents.json`を基に再作成した素材セット。

| 役職 | 通常 | 梯子上昇 | 梯子下降 |
|---|---|---|---|
| スーパーヒーロー | [Main](MoreCosmic/hats/tWg4uMeMb/HOb85tfub/Main.png) | [Climb](MoreCosmic/hats/tWg4uMeMb/HOb85tfub/Climb.png) | [ClimbDown](MoreCosmic/hats/tWg4uMeMb/HOb85tfub/ClimbDown.png) |
| イーヴィルヒーロー | [Main](MoreCosmic/hats/tWg4uMeMb/0Wg0o9Ic/Main.png) | [Climb](MoreCosmic/hats/tWg4uMeMb/0Wg0o9Ic/Climb.png) | [ClimbDown](MoreCosmic/hats/tWg4uMeMb/0Wg0o9Ic/ClimbDown.png) |

通常・梯子用の6枚は背景透過RGBA PNG、300×375px。通常用は青いスーツと赤マント／黒紫のスーツと紫マント。梯子用は背面とグローブを描き、バイザーや胸のマークは描かない。オーラ・光・効果音は画像に含めない。

歩行用は各役職に `Move.png`（2400×375px、横8コマ）を追加。`Length=8, AnimSync=1` でNoSのRunアニメーションに同期する。停止中はMain、歩行中だけMoveを使う。`export-walk.ps1` が生成原本の4列×2行を横一列へ並べ、各コマの頭頂・頭の中心を揃える。コマごとの拡縮はせず共通倍率で出力し、全身が伸縮・上下するのを避ける。追加原本は `source/super-walk.png`, `source/evil-walk.png`、組み込みimage_genへ渡した指示は `walk-prompts.json`。

## 基準位置

キャンバス全体を共通の座標として保ち、透明な余白を切り詰めない。通常用は参考画像の頭頂Y=116、足元Y=308、足の中心X=159を基準とする。梯子上昇は頭頂Y=90、足元Y=301、胴体中心X=158.5を基準とする。片足が上がるため、上昇用を足先の左右位置で揃えない。下降用は参考画像の頭頂・足元と同じ位置を基準とする。

`source/` は組み込み `image_gen` による高解像度原本。`export.ps1` は原本のアルファを保ち、基準位置に合わせて縮小・配置する素材出力処理。出力に使用した座標と倍率は [alignment.json](alignment.json)、生成指示の全文は [prompts.json](prompts.json) に保存した。参考資料は `reference/` に保存する。

## MoreCosmic定義

[MoreCosmic/Contents.json](MoreCosmic/Contents.json) はUTF-8、BOMなし。`Main` と `Climb` に加え、現在のNoSソースで対応を確認した `ClimbDown` と歩行用 `Move` を定義した。既定は `FPS=1`、Moveは `AnimSync` によるフレーム選択を使う。固定配色なので `Adaptive=false`、`Bounce=false`。この設定だけでは全身の上下動を抑えきれず、帽子ノード1も実機でY=0.35～0.90へ動いた。歩行中は帽子の位置同期だけを一時停止し、NoSの立ち姿と同じ基準位置 `(0, 0.575, 0)` と左右のオフセットへ固定する。`defaultHatVisorPosition` は実機ではゼロだったため使用しない。画像選択・左右反転は継続し、停止・梯子・通気孔・変身終了時は同期コンポーネントを元の有効状態へ戻す。

変身専用のため `IsHidden=true` とし、通常の衣装一覧には表示しない。梯子画像にグローブを含むため `HideHands=true`。このセットは全身を覆うHatレイヤーとして用意しており、別のVisor素材は登録しない。

画像の整理用に `MoreCosmic/hats/<作者ハッシュ>/<名前ハッシュ>/` へ配置する。ローカル／アドオンZIP用の `Address` は **hats/からの相対パス全体**。作者・名前フォルダが自動で補われるのはオンライン衣装のキャッシュ読み込みだけなので、ZIP内の定義にはこれらのフォルダも明記する。`ClimbDown` 等の扱いと固定ピボットは `NebulaPluginNova/Modules/Cosmetics/MoreCosmic.cs` を参照した。

`TanukiHeroCostumes-v2.zip` は上記MoreCosmicフォルダを格納した素材ZIP。アドオンのルートへ統合するためのもので、単独でAddonsへ投入するアドオンZIPではない。

## 確認範囲

通常・梯子用6枚と歩行用2枚の寸法・アルファ・内容、歩行の8分割と同期設定、NoSのZIPローダーが実際に使用する全画像参照先を確認した。再確認用のコマンドは `check-package.ps1 -ZipPath <アドオンZIP>`。

`addon/MoreCosmic` と変身処理を統合し、開発版ZIPをNoSへ配置した。NoS v3.5.3.8のエアシップ・ローカルフリープレイで、両ヒーローの全身衣装・オーラ、歩行、会議室の梯子の上昇・下降と通常姿勢への復帰、変身終了後の元の帽子・服・バイザーの復元を確認済み。実際の `Ladder.Use()` で専用のClimb／ClimbDown画像と位置同期の復帰を確認した。歩行では両役職とも8コマが選択され、取り付け位置Yは0.5750で一定だった。表示確認には認識成功のテスト信号を使用し、今回の衣装確認を実マイク認識の再検証とは扱わない。他クライアントからの見え方は未確認。

衣装はNoSの `CosmeticsLayer.SetHat/SetSkin/SetVisor` で一時的に適用する。元の体と手の描画フラグはUnityインスタンスIDごとに保存・復元し、保存済み衣装は変更しない。適用に失敗した場合は変身状態を解除してログを残す。
