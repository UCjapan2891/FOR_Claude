# 引き継ぎ書（HANDOFF）

最終更新：2026-11-10
新しい会話を始めたら、`CLAUDE.md` → `docs/SPEC.md` → この文書 の順に読む。
研究の設計で決めたことは `docs/SPEC.md` の「9. 決定事項の記録」に日付つきで残してある。

---

## 1. プロジェクトの概要

- 卒業研究「VR授業においてテロップに空間的強調を加えることが、3D構造の理解と認知負荷に与える影響」の実験システム。
- 条件1：テロップのみ（強調と同じ時間 Ttotal だけ止める）／条件2：テロップ＋空間的強調（引き寄せ → 拡大 → 回転 → 復帰）。
- 被験者内デザイン、18〜20名。授業は ①HDD ②HMD の内部構造。Meta Quest 3＋コントローラー。
- 研究者（ユーザー）：青木 勇士。返答は日本語で簡潔に。

## 2. 環境と作業の進め方

| 項目 | 内容 |
|---|---|
| リポジトリ | `UCjapan2891/FOR_Claude`（**公開リポジトリ**） |
| 作業ブランチ | `claude/new-session-b2lbou`（ここにだけコミット・プッシュする。会話ごとに変わるので、指示されたブランチを使う） |
| Unity | 6.3 LTS（6000.3.28f1）、URP、XRI 3.5.1、OpenXR 1.17.1、Unity OpenXR: Meta 2.5.1、Input System 1.20.1 |
| ユーザーの PC | Windows。プロジェクトは `C:\Users\hayat\OneDrive\デスクトップ\VR_Class`（OneDrive の外への移動を勧めたが未実施）。GitHub Desktop も使う |
| HMD | **手元にない**。HMD が必要な確認は `docs/HMD_CHECKLIST.md` に後回し |
| 本実験の動かし方 | **Quest Link**（PC でアプリを動かす。実験者のキー操作が PC のキーボードでできる） |

**Claude（クラウド）側の制約と工夫**
- Unity が動かせないので、コンパイルとテストは**ユーザーが PC で確かめる**。エラーは Console の文を貼ってもらう。
- Unity に依存しない部分（`Assets/Scripts/Core/` と Logging の一部）は、.NET 8 で確かめている。
  スクラッチパッドに `nettest.csproj`（net8.0、LangVersion 9、NUnit 3.14）と、Unity の `Vector3`・`Quaternion` を最小限まねた `UnityStub.cs` を置いて `dotnet test` する（`ConfigDefaultsTests.cs` は Unity 専用なので除外）。
  新しい会話ではスクラッチパッドが消えるので、必要なら作り直す（dotnet は `apt-get install dotnet-sdk-8.0` で入った）。
- XRI・OpenXR の API はソースで確かめる：`git clone --depth 1 --branch 3.5.1 https://github.com/needle-mirror/com.unity.xr.interaction.toolkit`（OpenXR は `com.unity.xr.openxr` の 1.17.1）。Unity のパッケージサーバーや turbosquid.com・blenderkit.com にはここから接続できない。
- **注意**：XRI 3.5.1 では `XRInteractionManager.SelectExit(XRBaseInteractor, XRBaseInteractable)` が使用禁止（コンパイルエラー）。必ずインターフェース（`IXRSelectInteractor`／`IXRSelectInteractable`）で呼ぶか、`PartSocket.Release` を使う。
- ユーザーの PC で Unity が作った `.meta` やシーンは、ユーザーにコミット・プッシュしてもらう。

**ユーザーとの進め方（CLAUDE.md より）**
- 研究の設計（条件・手順・数式・値）を変えるときは、理由を書いて先に確認する。決まったら SPEC の本文と「9. 決定事項の記録」を直す。
- 大きい変更は、作るファイルと確認方法の計画を出して了承を得てから実装する。
- 値は `ExperimentConfig` に集める。条件1・2の違いは `EmphasisController` の中だけに閉じ込める。
- 最後に「エディタ上で確認すること」と「HMD で確認すること」を分けて伝える。

## 3. 進み具合（PROMPTS.md の段階）

| 段階 | 内容 | プログラム | PC での確認 |
|---|---|---|---|
| 1・2 | 設定・割り当て・ログ・数式 | 済み | 済み |
| 3 | 部品操作・授業の進行 | 済み | 済み |
| 4 | 学習者パネル・実験者の操作 | 済み（XRI の使用禁止 API によるコンパイルエラーを修正済み） | **結果の報告待ち** |
| 5 | 注視判定・再配置 | 済み | **結果の報告待ち** |
| 6 | 空間的強調・テロップ | 済み（EmphasisController・TelopView・解説音声・emphasis_* の記録） | **未**（コンパイルとテスト用シーンでの確認をお願いする） |
| 7 | 教室・部品の取り込み | **一部**：教室アセットを取り込み済み（シーンへの配置はまだ）。HDD のモデル未決定 | — |
| 8 | 2本の授業を通す・練習・実機の準備 | 未 | — |
| 9 | 分析スクリプト | 未 | — |

- Unity の外での自動テスト：184 件すべて成功（Unity の Test Runner では、設定の初期値のテスト2件を加えて 186 件の見込み）。
- **ユーザーの PC 側の強調デモのシーン（`Assets/Scenes/EmphasisDemo.unity`）と `.meta`、段階6で増えた .cs の `.meta` はまだプッシュされていない**。

**段階6で決めた実装の細部（研究の設計は変えていない）**
- 何をするかは `Core/EmphasisPlan.ChooseMode`：条件2で注視できたときだけ空間的強調、ほかはテロップのみで Ttotal 停止。条件の分岐は EmphasisController とこの関数だけ。
- 段階は `EmphasisTimeline` で「強調の開始からの経過時間1本」で決める（段階ごとに時間を数え直さない）。条件1と2が同じフレーム数で終わる。
- 再配置した部品の強調は、今の位置から引き寄せ、④復帰で**再配置する前の位置**へ戻す（`SpatialEmphasis` の `homePosition`）。
- ログの detail：emphasis_start `mode=spatial|telop;gaze=ok|fail;total=10.500`、emphasis_phase `phase=pull|scale|rotate|observe|return`（空間的強調のときだけ）、emphasis_end `elapsed=…`（一時停止・中断なら `;cancelled`）。解説音声は audio_start `commentary;clip=…`、audio_end `commentary`（Ttotal で切れたら `commentary;cut`、一時停止なら `commentary;cancelled`）。
- テロップの位置：部品の上端から `telopGapAbovePart`（0.03 m、表示の調整用で SPEC にない値）上。
- テスト用シーンの条件は参加者番号の割り当てで決まる：HDD は 9001 → 条件1、9003 → 条件2（LessonTestBootstrap の participantId を変える）。

## 4. これまでに決めたこと（要点。詳細は SPEC の 9 章）

- 授業は**組み立て中心**。部品は分解した状態で机に並べて始める。
- 正しく置いた部品は**固定**。下の部品（先に置くべきソケット）が固定済みのときだけ固定し、上の部品を先に置いても外せる（詰み回避）。
- 床に落ちた部品は机の上の初期位置へ戻し、reset（reason=floor）を記録。
- 「触れる」＝コントローラーのボタンで掴むこと。説明音声の間は**新しく掴めない**だけで、持っている部品は離さない。要点の間（注視判定・強調）は手から離させる。
- 正面は**水平**、拡大率は s ≥ 1、引き寄せ距離は max(D, dmin)、注視失敗時はテロップのみを Ttotal 表示、条件1でも注視判定・再配置を行う。
- 入力：コントローラーのみ／両方／キーマウのみを `inputMode` で切り替え（実行中は F1）。**本実験はコントローラーのみ**。
- ログ：events.csv と head.csv は1人1ファイル、lesson 列で区別、UTF-8（BOM 付き）。participants.csv は persistentDataPath。テストのログは参加者番号 9001。
- 教室：Asset Store「学校科学教室」（Japanese School Science Classroom、NIMIKKO、v1.0、標準 Unity Asset Store EULA）。**公開リポジトリには置かない**：`Assets/Nimikko_JapaneseSchool_ScienceRoom/` と `Assets/ThirdParty/` は `.gitignore` で除外。
- テスト・質問紙はアプリの外（PC）で回答する。

## 5. 中間報告（3rd stage）で変えたこと（2026-11-10 に SPEC 1章・7章・9章へ反映済み）

SSQ は実験前、休憩なしは中間報告の時間配分に合わせた（ユーザーに要確認。違えば SPEC を直す）。
- 1人あたり**約30分**（説明・同意 2、参加者情報・SSQ 1、練習 2、授業 約5.5 ×2、直後 6.5 ×2、終了後 1）。
- **事前テストなし**（伸び G を使わず、直後の正答率で比べる）。参加者情報で「HDD・HMD の中を学んだことがあるか」を聞く。
- 理解度テストは授業1本につき**6問**（記憶2・空間理解3・応用1）。空間理解は要点 A・B・C に1問ずつ。
- NASA-TLX は **WWL 法**のまま、各授業の直後に行う（約2.5分）。
- 酔いやすさ（MSSQ）と没入感（IPQ）は**省く**。強調の印象は5段階の独自の質問。
- 授業は 7段階・要点3つのまま、説明音声を1段階 約20秒にして**約5.5分**。
- 音声は VOICEVOX（ずんだもん）。クレジット「VOICEVOX:ずんだもん」を表記する。

## 6. ファイルの地図

| 場所 | 中身 |
|---|---|
| `Assets/Scripts/Core/` | 純粋なロジック（割り当て、CSV、時計、数式、状態遷移 LessonFlow、向き、固定の規則、自動タイマー、HMD 検出、注視判定） |
| `Assets/Scripts/Experiment/ExperimentConfig.cs` | すべての値（SPEC の [ ]、切り替え、開発用の値） |
| `Assets/Scripts/Lesson/` | LessonController（進行・パネル・S/P/A・自動タイマー）、LessonData/StageData、テスト用の起動、デバッグ表示、HMD 検出 |
| `Assets/Scripts/Interaction/` | 部品（PartInteraction）、ソケット（PartSocket）、固定（AssemblyLock）、床（FloorZone）、パネル、ヒント、マウス操作、元の状態の保存（PartPose）、黒板（PartInfoBoard / PartDescription） |
| `Assets/Scripts/Gaze/` | 注視判定（GazeJudge）、再配置（Relocator）、要点の処理（GazeKeyPointHandler：今は判定後に元に戻すだけ）、デバッグ表示 |
| `Assets/Scripts/Emphasis/` | 強調の動き（SpatialEmphasis）、発表用デモ（EmphasisDemo） |
| `Assets/Scripts/Logging/` | events.csv / head.csv の書き込み、SessionLog |
| `Assets/Scripts/Editor/` | メニュー：Create Config、Open Data Folder、participants.csv のひな形、Setup Japanese Font、Build Lesson Test Scene、Build Emphasis Demo Scene |
| `Assets/Tests/EditMode/` | EditMode テスト |
| `docs/SPEC.md` | 仕様書と決定事項の記録 |
| `docs/HMD_CHECKLIST.md` | HMD で確認する項目（20 項目、すべて未） |
| `docs/test_questions_draft.txt` | 理解度テスト（HDD・HMD 各6問）・印象の質問・自由記述・参加者情報の案 |
| `docs/PROMPTS.md` | 段階ごとのプロンプト集 |

**Unity のメニュー（Tools/Experiment/）**
- `Build Lesson Test Scene`：仮の部品3つで HDD の授業を通すシーン（`LessonTest.unity`）と `HDD_Test.asset`（5段階、要点は段階3・4）。
- `Build Emphasis Demo Scene`：発表用の強調デモ（`EmphasisDemo.unity`）。操作：左ドラッグで掴む、P 強調、R 中止、Tab 対象、K 説明の表示、Backspace やり直し。吸着はデモ用の 12 cm・45°（実験の値 5 cm・15° とは別）。
- テスト用シーンのキー：F1 入力切り替え、1 リセット、2 ヒント、S スキップ、P 一時停止・再開、A×2 中断、F2 HMD を外したまね。

## 7. 未決定・保留

- HDD のモデル：TurboSquid（`Hard_Drive_tb.c4d`、Cinema 4D 11.5 形式で Claude 側では変換不可、Royalty Free）か BlenderKit（「HDD 3.5 inches Western Digital WD5000AAVS」、.blend なら Claude 側で FBX に変換できる見込み、ライセンスの種類は未確認）。
- 教室のどの実験台を使うか。教室をシーンに組み込む Editor メニュー（Place Selected Prefab ／ Align To Selected Table ／ ClassroomLayout.asset）の計画は**保留中**（未実装）。
- XR Origin のスティック移動・回転・テレポートを残すか（HMD で確かめて決める）。
- 黒板の説明文・テストの問題は**仮の文**。授業の原稿（説明音声）ができたら合わせて直す。

## 8. 次にやること（おすすめの順）

1. ユーザーの PC で、強調デモのシーンと `.meta`（段階6の新しい .cs の分も）をコミット・プッシュしてもらう。段階4・5・6の PC での確認結果を聞く（段階6は Build Lesson Test Scene を作り直してから）。
2. 段階7：教室の組み込み（保留中の計画）と HDD のモデルの取り込み。
3. 段階8：ExperimentManager（番号入力 → 割り当て → 練習 → 1本目 → 2本目 → 終了）、本番の HDD.asset / HMD.asset。
4. 段階9：分析スクリプト（`analysis/`、架空データ 20 人分で動作確認。SSQ・WWL の表は `analysis/tables/` にユーザーが用意する）。

## 9. 作ったもの（リポジトリの外）

- 進捗報告のスライド（Artifact）：https://claude.ai/artifact/1rsarUgRd6gexiu3xEXcEt
- 中間報告（3rd stage）の pptx：30分版・動画入り（16枚）をユーザーに渡した。元のファイルはユーザーの手元にある。
