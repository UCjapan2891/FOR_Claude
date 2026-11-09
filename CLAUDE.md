# CLAUDE.md — VR授業 空間的強調 実験システム

このファイルはプロジェクトのルートに置き、Claude Code が毎回最初に読む指示書として使う。
研究の仕様の詳細は `docs/SPEC.md` にある。実装に入る前に必ず読むこと。

## 1. このプロジェクトは何か

卒業研究「VR授業においてテロップに空間的強調を加えることが、3D構造の理解と認知負荷に与える影響」の実験システム。
演出の違いによる**理解度（学習効果）**を比較する。テストは各授業の直後のみで、遅延テスト（定着）は行わない。

- 条件1：テロップのみ
- 条件2：テロップ＋空間的強調（引き寄せ → 拡大 → 回転 → 復帰）
- 被験者内デザイン、18〜20名。授業は ①HDD の内部構造、②HMD の内部構造 の2本
- 舞台は理科実験室（実在の部屋をスキャン。参考：`docs/room_reference.png`）。受講者は実験台の前で授業を受ける
- 受講者は Meta Quest 3 を装着し、HDD・HMD の3D部品を自分で動かして分解・組み立てをしながら、説明・解説の音声を聞いて学ぶ。要点では演出（テロップ／空間的強調）を受け取る
- 作るもの：Unity の実験アプリ（`Assets/`）と、ログ・アンケートの分析スクリプト（`analysis/`）

## 2. 環境

- Unity：Unity 6.3 LTS（6000.3.28f1）
- エディタのパス：C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe（Unity Hub の既定の場所。違う場所に入れた場合は直す）
- OS：Windows
- XR：OpenXR 1.17.1 ＋ XR Interaction Toolkit 3.5.1 ＋ Unity OpenXR: Meta 2.5.1。入力は Quest 3 のコントローラー
- その他：Input System 1.20.1、URP 17.3.0、Test Framework 1.6.0、TextMesh Pro（uGUI 2.0.0 に同梱、Essentials 取り込み済み）
- 分析：Python 3（pandas, numpy, scipy, matplotlib）

パッケージのバージョンは推測しない。まず `Packages/manifest.json` を読み、入っているバージョンの API に合わせて書く。
特に XRI 3.x では、名前空間が `UnityEngine.XR.Interaction.Toolkit.Interactables` / `.Interactors` に分かれている。

## 3. 作業のルール

### 研究の設計を勝手に変えない
- 条件、手順、数式、パラメータの値は研究の中身そのもの。変更が必要だと思ったら、実装する前に理由を書いて私に確認する
- パラメータ（角度・距離・秒数など）は、コードに直接書かず `ExperimentConfig`（ScriptableObject）に集める。値は `docs/SPEC.md` の表のとおりにする
- **条件1と条件2の違いは `EmphasisController` の中だけに閉じ込める。** ほかのクラスで条件による分岐を書かない
- 研究で使う値・尺度の項目・採点表（SSQの項目対応など）を推測で作らない。必要なら「この表を用意してください」と私に頼む

### コードの書き方
- 数式やアルゴリズム（注視角、再配置位置、イージング、拡大率、グループ分け）は、MonoBehaviour から切り離した純粋な C# の static クラス（`Assets/Scripts/Core/`）に書き、EditMode テストを付ける
- 時間は必ず `Time.deltaTime` の積算か、コルーチン／async で秒として扱う。フレーム数で数えない
- ログに残すべき出来事（`docs/SPEC.md` 6章）は、起きた時点でファイルに書き込んでフラッシュする。アプリが落ちても残るようにするため
- コメントと UI の文言は日本語でよい。クラス名・変数名は英語

### Unity エディタの操作
- Claude Code はエディタの画面を操作できない。シーンの組み立てや参照の割り当ては、できる限り Editor スクリプト（メニュー項目 `Tools/Experiment/...`）で自動化する
- 自動化できない作業は、「どのウィンドウで、何を、どこに設定するか」を番号付きの手順で私に渡す
- `Library/`、`Temp/`、`.meta` ファイルを手で編集しない。`ProjectSettings/` は変更前に確認する

### 確認の手順
1. 変更したら、エディタを閉じた状態でコンパイルを確認する
   `"<エディタのパス>" -batchmode -nographics -projectPath . -quit -logFile -`
2. ロジックを変えたら EditMode テストを流す
   `"<エディタのパス>" -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults TestResults.xml -logFile -`
3. 最後に「エディタ上で私が確認すること」と「HMD を被って確認すること」を分けて箇条書きで伝える
- エディタを開いているとバッチモードは失敗する。そのときは私に閉じるよう頼む

### 進め方
- 1回の依頼で1つの機能だけ作る。大きい変更は、まず計画（作るファイル・クラス・確認方法）を出して私の了承を得る
- 区切りごとに git で小さくコミットする。メッセージは日本語でよい
- 返答は日本語で、簡潔に

## 4. フォルダ構成（目標）

```
Assets/
  Scripts/
    Core/          数式・判定などの純粋ロジック（テスト対象）
    Experiment/    ExperimentManager, ExperimentConfig, ParticipantAssignment
    Lesson/        LessonController, LessonData（ScriptableObject）, StageData
    Gaze/          GazeJudge, Relocator
    Emphasis/      EmphasisController, TelopView
    Interaction/   PartInteraction, PartSocket, LearnerPanel
    Logging/       EventLogger, HeadLogger
    Editor/        シーン組み立て用の Editor スクリプト
  Tests/EditMode/
  Lessons/         HDD.asset, HMD.asset（授業データ）
  Models/          部品の3Dモデル、スキャンした部屋
analysis/          Python の分析スクリプト
docs/SPEC.md       仕様書
```
