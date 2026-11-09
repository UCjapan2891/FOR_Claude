# HMD で確認すること（後回しにした項目）

HMD がない間は、PC（エディタとキーボード・マウス）で確認できる範囲だけ進める。
HMD が使えるようになったら、この一覧を上から確かめる。確かめたら `[x]` にする。

## 準備（Quest Link）
- [ ] Quest Link（ケーブルか Air Link）で PC につなぐ
- [ ] Project Settings → XR Plug-in Management → PC（Windows）タブで OpenXR にチェック
- [ ] OpenXR → Interaction Profiles に Oculus Touch / Meta Quest Touch Plus Controller Profile がある

## 段階3：部品操作と授業の進行
- [ ] コントローラーのグリップで部品を掴める
- [ ] 説明音声の間は新しく掴めない（持っている部品はそのまま持っていられる）
- [ ] 正しい部品・向き（15° 以内）・位置（5 cm 以内）でだけソケットに吸着する
- [ ] events.csv の grab / release に `input=controller` と出る
- [ ] 実験台の高さ（テスト用シーンでは 0.8 m）が立ったときに不自然でない
- [ ] 「HMD を外してください」の日本語が HMD の中で読める
- [ ] XR Origin のスティック移動・回転・テレポートを残すか外すか決める
- [ ] 部品を床に落とすと、すぐ机の上の初期位置に戻る
- [ ] 正しく置いた部品はコントローラーで掴めない（固定）。カバーを先に置いたときは外せる
