# SwiftlyBhopTimer コマンド表

コマンドはチャットから `!command` 形式で実行します。`st_` 付きのコマンドは SwiftlyBhopTimer 用の推奨エイリアスです。

## プレイヤーコマンド

| コマンド | エイリアス | 説明 |
| --- | --- | --- |
| `!stver` | `!st_ver` | プラグインのバージョンを表示します。 |
| `!help` | `!sthelp`, `!st_help` | 利用可能なコマンドを表示します。 |
| `!timer` | `!st_timer` | 自分のタイマー有効/無効を切り替えます。 |
| `!options` | `!settings`, `!stoptions`, `!st_options` | 個人設定メニューを開き、HUD、表示、FOV、サウンドを変更します。 |
| `!hud` | `!st_hud` | 自分の HUD 表示/非表示を切り替えます。 |
| `!hidelegs` | `!st_hidelegs` | 自分の足表示/非表示を切り替えます。 |
| `!hide` | `!st_hide` | 自分以外のプレイヤー表示/非表示を切り替えます。 |
| `!hidefps` | `!st_hidefps` | 一人称の腕/ビューモデル表示を切り替えます。 |
| `!fov <85-130>` | `!st_fov <85-130>` | 自分の FOV を設定します。 |
| `!sounds` | `!st_sounds` | 自分のタイマーサウンド有効/無効を切り替えます。 |
| `!pause` | `!stpause`, `!st_pause` | 現在のタイマーを一時停止/再開し、停止中は移動も固定します。 |
| `!stop` | `!ststop`, `!st_stop` | 現在のタイマーを記録せず停止してリセットします。 |
| `!cp` | `sbt_cp` | 現在のCPスロットに位置、角度、速度を保存します。保存時にタイマーと録画はリセットされます。 |
| `!tp` | `sbt_tp` | 現在のCPスロットへ戻ります。TP時にタイマーと録画はリセットされます。 |
| `sbt_nextcp` | `sbt_prevcp`, `sbt_clearcp` | bind向けのCPスロット切替/消去コマンドです。 |
| `!ssp` | `sbt_ssp`, `!ssp clear` | 現在位置を個人用スタート位置として保存します。通常時の `!r` はこの位置を優先します。 |
| `!noclip` | `!stnoclip`, `!st_noclip` | プラグイン制御の noclip を切り替えます。有効化時はタイマーを強制停止し、無効化まで新規スタートをブロックします。コンソール noclip を無効化したサーバー向けです。 |
| `!r` | `!st_r` | ボーナス走行中はそのボーナスのリスポーン位置へ、それ以外は通常マップのリスポーン/スタート位置へ戻ります。 |
| `!b <1-99>` | `!bonus <1-99>`, `!st_b <1-99>`, `!st_bonus <1-99>` | ボーナスのリスポーン位置、またはボーナススタート位置へ戻ります。 |
| `!b1` ... `!b99` | `!st_b1` ... `!st_b99` | 各ボーナスへ移動するショートカットです。 |

## 記録コマンド

| コマンド | エイリアス | 説明 |
| --- | --- | --- |
| `!top [map]` | `!mtop [map]`, `!st_top [map]`, `!st_mtop [map]` | 現在マップ、または指定マップの上位記録を表示します。 |
| `!rank [map]` | `!st_rank [map]` | 自分の現在マップ、または指定マップでの順位を表示します。 |
| `!sr [map]` | `!st_sr [map]` | 現在マップ、または指定マップのサーバーレコードを表示します。 |
| `!tier [map]` | `!sttier`, `!st_tier` | 現在マップ、または指定マップの難易度 Tier を表示します。 |
| `!btop <1-99>` | `!topbonus <1-99>`, `!st_btop <1-99>`, `!st_topbonus <1-99>` | 指定したボーナスの上位タイムを表示します。 |
| `!stage` | `!st_stage` | 現在のステージ番号と経過タイムを表示します。 |
| `!replay [b1]` | `!streplay [b1]`, `!st_replay [b1]` | Top 1-5 / PB から再生するリプレイをセンターメニューで選択します。`b1` または `bonus 1` を付けるとボーナスリプレイを開きます。 |
| `!pbreplay [b1]` | `!stpbreplay [b1]`, `!st_pbreplay [b1]` | 通常マップ、または指定ボーナスの自分の PB リプレイ Bot を追加します。 |

## 管理者コマンド

管理者判定は SwiftlyS2 の権限 `swiftlybhoptimer.admin` で行います。SwiftlyS2 のワイルドカード権限として `swiftlybhoptimer.*` を付与したグループでも通ります。

| コマンド | エイリアス | 説明 |
| --- | --- | --- |
| `!admin` | `!stadmin`, `!st_admin` | 管理者メニューを開き、タイム削除、ゾーン設定、cfg適用などを実行します。 |
| `!map [map]` | `!changemap`, `!st_changemap` | 設定済みマップ一覧から変更先を選択します。マップ名を指定した場合は、その設定済みマップへ即時変更します。 |
| `!maptier <map> <0-10>` | `!stmaptier`, `!st_maptier` | 指定マップの難易度 Tier を保存します。`0` は未設定、`1`〜`10` は Tier 1 (Novice) から Tier 10 (Master/TAS) です。`!maptier <0-10>` で現在マップの Tier を設定できます。 |
| `!addmap <map> <workshopId> [0-10]` | `!staddmap`, `!st_addmap` | MapChooser に Workshop マップを追加、または既存マップを更新します。Tier は任意で、省略時は `0` です。 |
| `!st_cfg` | `!stcfg` | SwiftlyBhopTimer 用 cfg を再生成して適用します。 |
| `!st_chat` | `!stchat` | チャット Prefix/色設定を再読み込みします。 |
| `!st_replaybot` | `!streplaybot` | リプレイ Bot の追加を強制します。 |
| `!st_collision [on/off]` | `!stcollision [on/off]` | プレイヤー衝突無効化を切り替えます。 |
| `!st_damage [on/off]` | `!stdamage [on/off]` | ダメージ無効化を切り替えます。 |
| `!st_deltime <rank> [map]` | `!stdeltime`, `!st_delrecord`, `!stdelrecord` | `!top` と同じ順位基準で、指定順位のベストタイムを削除します。 |
| `!st_setstart1` | `!stsetstart1` | 現在位置をスタートゾーンの角 1 として保存します。 |
| `!st_setstart2` | `!stsetstart2` | 現在位置をスタートゾーンの角 2 として保存します。 |
| `!st_setend1` | `!stsetend1` | 現在位置をゴールゾーンの角 1 として保存します。 |
| `!st_setend2` | `!stsetend2` | 現在位置をゴールゾーンの角 2 として保存します。 |
| `!st_setrespawn` | `!stsetrespawn` | 現在位置をリスポーン位置として保存します。 |
| `!st_setbonusstart1 <1-99>` | `!stsetbonusstart1 <1-99>` | 現在位置を指定ボーナスのスタートゾーン角 1 として保存します。 |
| `!st_setbonusstart2 <1-99>` | `!stsetbonusstart2 <1-99>` | 現在位置を指定ボーナスのスタートゾーン角 2 として保存します。 |
| `!st_setbonusend1 <1-99>` | `!stsetbonusend1 <1-99>` | 現在位置を指定ボーナスのゴールゾーン角 1 として保存します。 |
| `!st_setbonusend2 <1-99>` | `!stsetbonusend2 <1-99>` | 現在位置を指定ボーナスのゴールゾーン角 2 として保存します。 |
| `!st_setbonusrespawn <1-99>` | `!stsetbonusrespawn <1-99>` | 現在位置を指定ボーナスのリスポーン位置として保存します。 |

## デバッグ/運用コマンド

| コマンド | エイリアス | 説明 |
| --- | --- | --- |
| `!st_debugtouch` | `!stdebugtouch` | トリガー接触デバッグの表示を切り替えます。 |
| `!st_map [map]` | `!stmap [map]` | 現在のマップ情報を表示します。`map` 指定時はプラグイン側のマップ名を切り替えます。 |
| `!st_where` | `!stwhere` | 現在位置、スタート/ゴールゾーン内判定、タイマー状態を表示します。 |
| `!st_beam` | `!stbeam` | スタート/ゴールゾーンのビーム描画を再予約します。 |

## 使用例

```text
!top
!rank
!sr bhop_eazy
!r
!b1
!btop 1
!replay b1
!pbreplay b1
!pause
!stop
!noclip
!st_fov 110
!st_deltime 1
!st_deltime 3 bhop_eazy
!st_setrespawn
!st_setbonusstart1 1
!st_setbonusend2 1
```
