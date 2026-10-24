# ヒーロー変身アイコン

変身後のMoreCosmic衣装を参考にした、変身能力ボタン専用の透過PNG。スーパーヒーローは青・赤・金と光の演出、イーヴィルヒーローは黒紫・マゼンタと闇の演出で区別する。

組み込みimage_genで各役職を個別生成。生成指示は `prompts.json`、生成原本は `source/`、ゲーム用素材は `addon/Resources/Heroes/` に保存する。通常のキルボタンと役職説明用画像は変更しない。

- [SuperTransform.png](../../../addon/Resources/Heroes/SuperTransform.png)
- [EvilTransform.png](../../../addon/Resources/Heroes/EvilTransform.png)

`export.ps1` で原本の縦横比とアルファを保ったまま224px内へ縮小し、256×256pxのキャンバス中央へ配置する。上下左右に16px以上の余白を設ける。ゲームでは従来の能力アイコンと同じ180PPUで読み込み、役職ごとにキャッシュする。

NoS v3.5.3.8のエアシップ・ローカルフリープレイで両役職へ切り替え、新しい画像のカラー表示、クールタイム中の減色と秒数表示、ラベルと隣のボタンの配置を確認した。開発版ZIPはAddonsへ配置済み。NoSコンパイル62ファイルでエラー・警告なし、ネスト監査68ファイルで最大3・違反なし。
