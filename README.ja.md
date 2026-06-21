# SwiftlyBhopTimer

> [!IMPORTANT]
> このプロジェクトは Vibe Coding で作成されています。各プロジェクトは自由にフォークし、コピー、カスタマイズ、開発していただいて構いません。

SwiftlyS2 向けの bhop 特化タイマープラグインです。

- [English README](README.md)
- [コマンド表](COMMANDS.ja.md)

このプロジェクトは、従来の CounterStrikeSharp 版とは分離されています。
`CounterStrikeSharp.API` は参照せず、SwiftlyS2 の C# API を対象にしています。

本プラグインは [girlglock/SharpTimer](https://github.com/girlglock/SharpTimer) を参考に、SwiftlyS2 上で動作する後継候補としてテスト実装しているものです。SharpTimer はタイマー挙動や機能方針の参考にしつつ、本プロジェクトでは bhop 向けの SwiftlyS2 実装に特化しています。

## 必須環境

- Counter-Strike 2 dedicated server
- SwiftlyS2 `1.3.5` 以上
- SwiftlyS2 の C# / managed plugin 実行環境
- 本体プラグインは `net10.0` ターゲットです。
- MetaMod:Source と、サーバーOSに対応した `SwiftlyBhopTimer MetaMod Helper` が必須です。

## Bhop モード

- `Standard`: CS2 の挙動をベースにした標準モードです。タイマー向けの互換設定や保護処理は行いますが、プレイフィールを大きく変える補正は最小限にしています。
- `Classic`: 実験的なモードです。CSGO / CS:S などの Bhop スタイル、特に 100 tick bhop サーバーに近いストレイフ感と速度維持を再現することを目指しています。単純に速度を上げるための高速モードではなく、空中ストレイフ時の減速を抑え、CSGO / CS:S 系の入力感に近づけるための補助モードです。

Classic の速度差は固定の割合ではありません。実際の加速量は現在速度、視点角度、入力方向、接地状態、マップギミックの影響を受けます。現在の実装では Helper 側で CSS 100 tick 相当の空中加速計算を参考にしつつ、CS2 側の現在速度を不自然に壊さない範囲で補正しています。

## MetaMod Helper について

SwiftlyBhopTimer は、SwiftlyS2 の C# プラグイン本体と `SwiftlyBhopTimer MetaMod Helper` の 2 層構成です。

Helper は MetaMod:Source 上で動作する必須の native 補助プラグインです。SwiftlyS2 本体はタイマー、DB、HUD、チャットコマンド、MapData、記録保存などのゲームロジックを担当し、Helper は CS2 エンジン寄りの処理を担当します。

切り分けている理由は、SwiftlyS2/C# からは安定して触りにくい、またはサーバーモードや workshop cfg の影響を受けやすい処理があるためです。具体的には replay bot の生成/削除/上限管理、round flow と map time の補助、HTML HUD flashing 対策、`!r` の native teleport、map_start 系 spawn fallback、noclip の MoveType 制御、hidefps/viewmodel 制御、trigger_push/telehop/bhop block/RNGFix 互換処理、Classic モードの movement 補助などを Helper 側へ寄せています。

この分離により、タイマー本体は SwiftlyS2 の開発しやすい C# API を使い続けつつ、CS2 の低レベルな挙動に近い部分だけを MetaMod 側で扱えます。将来的に SwiftlyS2 以外の実装へ移行する場合も、Helper の `sbt_*` サーバーコマンドを呼び出す形で native 側の一部機能を再利用しやすくなります。

## 現在の状態

SwiftlyS2 向けに独立して実装した bhop タイマーです。

現在含まれている主な機能:

- SwiftlyS2 `net10.0` プロジェクト
- SwiftlyS2 本体と MetaMod Helper の分離構成
- プラグインライフサイクルのエントリーポイント
- タイマー系チャットコマンド
- マップロード、プレイヤー接続/切断、tick、エンティティ接触イベント
- start/end トリガーによるタイマー開始/停止
- ボーナス start/end/respawn による別枠タイマーとボーナス記録
- `MapStartC1/MapStartC2` と `MapEndC1/MapEndC2` による座標 fallback
- `cfg/SwiftlyBhopTimer/MapData` 用 MapData JSON DTO
- SwiftlyS2 Database を使った設定、記録、リプレイ保存
- ラウンド進行、チーム、Bot、保護、マップ投票向けの cfg / ConVar 適用
- MetaMod Helper を使った replay bot、noclip、restart teleport、movement/gimmick 補助

## 実装されている機能一覧

タイマー / 記録:

- スタート/ゴールゾーンによるタイマー開始・停止
- トリガー名と座標範囲によるスタート/ゴール判定
- Standard / Classic のモード別タイマー記録
- PB、SR、rank、top コマンド
- ゴール時の順位、PB差分、SR差分表示
- New SR の全体チャット通知
- SwiftlyS2 Database を使った設定、タイム、リプレイ保存

マップ / ゾーン:

- MapData 読み込み、同梱 MapData の展開、マップ設定コマンド
- `MapStartC1/MapStartC2` と `MapEndC1/MapEndC2` による座標 fallback
- スタート/ゴールゾーンの Beam 描画
- 管理者用のスタート、ゴール、リスポーン位置設定
- ボーナスステージのスタート、ゴール、リスポーン設定
- `!b1` 形式のボーナス移動とボーナス上位記録
- マップ Tier、マップ追加、MapChooser、RTV、map extend

プレイヤー操作 / 練習:

- リスポーン位置へ戻る `!r`
- `!pause` と `!stop`
- 練習用 `!cp` / `!tp` と bind 向け `sbt_cp` / `sbt_tp` / `sbt_nextcp`
- 個人用スタート位置 `!ssp` / `sbt_ssp`
- `!noclip` と、noclip 中のタイマー停止/開始ブロック
- スタートゾーン内の速度制限
- `!hidelegs`, `!hide`, `!hidefps`, `!fov`
- HUD、表示、FOV、サウンド設定の保存

HUD / チャット / メニュー:

- CenterHTML HUD
- HUD flashing 対策
- 観戦時のHUD参照
- チャット prefix / 色設定
- `!options` / `!settings` の個人設定メニュー
- 管理者向けメニュー

Replay Bot / Helper:

- Standard / Classic の SR 常設 replay bot
- `!replay` による上位記録とPB replay bot 呼び出し
- `!spec` によるプレイヤー / bot 観戦メニュー
- MetaMod Helper による replay bot 生成、削除、上限管理
- Helper による `!r` native teleport、map_start fallback、noclip、hidefps 補助
- Helper による trigger_push、telehop、bhop block、RNGFix 互換処理
- Helper による Classic mode movement 補助

サーバー保護 / cfg:

- collision 無効化、damage 無効化
- 武器ドロップ抑制とチーム変更後のハンドガン再付与
- タイマー向け round flow cfg の生成
- ジャンプ/重力/空中加速を上書きしない timer 向け cfg / ConVar 適用
- Linux / Windows publish 出力

## ビルド

```powershell
dotnet restore
dotnet build
```

Linux / Windows の両方を publish する場合:

```powershell
.\scripts\publish.ps1
```

手動で個別に publish する場合:

```powershell
dotnet publish .\SwiftlyBhopTimer.csproj -c Release -r linux-x64 --self-contained false
dotnet publish .\SwiftlyBhopTimer.csproj -c Release -r win-x64 --self-contained false
```

出力先:

- `build/SwiftlyBhopTimer_linux`
- `build/SwiftlyBhopTimer_windows`

## 推奨サーバープラグイン

CS2 の movement まわりの問題を減らし、surf/bhop の挙動を近づけるため、以下の導入を推奨します。

- [SharpTimer/STFixes-metamod](https://github.com/SharpTimer/STFixes-metamod) または [Source2ZE/MovementUnlocker](https://github.com/Source2ZE/MovementUnlocker)
- [Interesting-exe/CS2Fixes-RampbugFix](https://github.com/Interesting-exe/CS2Fixes-RampbugFix/)

`STFixes-metamod` には surf/bhop 向けの movement 修正と movement unlocker 相当の機能が含まれるため、複数プラグインで同種の movement unlock を同時に有効化しないよう注意してください。

## コマンド

- [Command reference (English)](COMMANDS.en.md)
- [コマンド表 (日本語)](COMMANDS.ja.md)

## MapChooser

マップ投票は `cfg/SwiftlyBhopTimer/SwiftlyBhopTimer.MapChooser.json` で設定します。
`VoteStartBeforeEndMinutes` を変更すると、マップ終了何分前に自動投票を開始するかを指定できます。
投票候補には設定済みマップに加えて、`MaxExtends` の上限に達していない場合はマップ延長も入ります。

MapChooser のチャット出力だけは `ChatColors` で個別に色変更できます。
通常のタイマー、順位、ゴール時チャットの色には影響しません。

```json
{
  "VoteStartBeforeEndMinutes": 5.0,
  "ExtendMinutes": 15.0,
  "MaxExtends": 2,
  "ChatColors": {
    "Label": "{lightblue}",
    "Value": "{green}",
    "Accent": "{gold}",
    "Extend": "{gold}",
    "Muted": "{gray}",
    "Error": "{red}"
  }
}
```

## 開発構成

- `src/SwiftlyBhopTimerPlugin.cs`: プラグインライフサイクル、共有状態、サービス初期化、tick loop
- `src/Features/SwiftlyBhopTimer.Commands.cs`: コマンド登録とチャットコマンド処理
- `src/Features/SwiftlyBhopTimer.Events.cs`: SwiftlyS2 / game event hook
- `src/Features/SwiftlyBhopTimer.MapData.cs`: マップ読み込み、復旧、ゾーン描画、設定中プレビュー
- `src/Features/SwiftlyBhopTimer.Movement.cs`: リスポーン、FOV、スタートゾーン速度制限、pause中の移動固定
- `src/Features/SwiftlyBhopTimer.Hud.cs`: CenterHTML HUD と HUD flashing 対策
- `src/Features/SwiftlyBhopTimer.ReplayBot.cs`: 実験中のリプレイ bot 作成と再生ループ
- `src/Features/SwiftlyBhopTimer.TimerFlow.cs`: start/end/stage/bonus タイマー処理とゴール時チャット
- `src/Features/SwiftlyBhopTimer.Infrastructure.cs`: チャット整形、DB診断、設定保存補助
- `src/Services`: ストレージ、マップ、描画、表示、保護、リプレイ、フォーマットなどの再利用サービス

新しいゲームプレイ機能は、まず対応する `src/Features` ファイルへ追加する方針です。DB、IO、パース、エンジン補助などの再利用コードは `src/Services` に分けます。

## メモ

一部コマンドは任意のマップ名を受け取れます。

```text
!top bhop_beginnerfriendly
!rank bhop_beginnerfriendly
!sr bhop_beginnerfriendly
```

マップテスト時は `!st_debugtouch` で解決されたトリガー名を確認できます。HotReload などでマップロードイベントを受け取れなかった場合は、`!st_map <mapname>` でプラグイン側のマップ名を指定できます。

## 参考プロジェクト / Credits

SwiftlyBhopTimer は独立した実装ですが、設計や挙動検証にあたり以下のプラグイン、プロジェクト、ドキュメントを参考にしています。各プロジェクトの権利とライセンスは、それぞれのリポジトリに従います。

- [swiftly-solution/swiftlys2](https://github.com/swiftly-solution/swiftlys2): SwiftlyS2 本体と C# plugin API
- [girlglock/SharpTimer](https://github.com/girlglock/SharpTimer): timer 挙動、MapData、チャット出力、replay、Bhop Timer 全体仕様の参考
- [KZGlobalteam/cs2kz-metamod](https://github.com/KZGlobalteam/cs2kz-metamod): MetaMod 側の bot 生成、native helper 実装方針の参考
- [girlglock/CS2FlashingHtmlHudFix](https://github.com/girlglock/CS2FlashingHtmlHudFix): CS2 HTML HUD flashing 対策の参考
- [jason-e/rngfix](https://github.com/jason-e/rngfix): slope、edge、telehop、triggerjump などの RNGFix 系挙動の参考
- [shavitush/bhoptimer](https://github.com/shavitush/bhoptimer): CSGO / CS:S 系 Bhop Timer と strafe / physics 方向性の参考
- [SharpTimer/STFixes-metamod](https://github.com/SharpTimer/STFixes-metamod): surf/bhop 向け movement 修正の参考、および推奨サーバープラグイン
- [Source2ZE/MovementUnlocker](https://github.com/Source2ZE/MovementUnlocker): movement unlock 系の参考、および推奨サーバープラグイン
- [Interesting-exe/CS2Fixes-RampbugFix](https://github.com/Interesting-exe/CS2Fixes-RampbugFix/): rampbug 対策の参考、および推奨サーバープラグイン
