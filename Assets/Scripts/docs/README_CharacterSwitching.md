# キャラクター切り替えシステム - 使い方ガイド

## 概要
GameManagerを使って、複数のキャラクターを簡単に切り替えられるシステムです。
このシステムを使うと、以下が自動的に処理されます:
- カメラの追跡対象の切り替え
- 入力システムとカメラの連携
- アクティブキャラクター以外の入力無効化

## セットアップ方法

### 1. GameManagerの設定

1. シーン内に空のGameObjectを作成し、名前を「GameManager」にする
2. `GameManager`コンポーネントをアタッチ
3. Inspectorで以下を設定:
   - **Active Character**: 初期操作キャラクター（GameInputManagerを持つオブジェクト）
   - **Camera Tracker**: Main CameraのCharacterTracker（通常は自動検出されます）
   - **Switch Character Key**: キャラクター切り替えキー（デフォルト: Tab）
   - **Available Characters**: 切り替え可能なキャラクターのリスト

### 2. キャラクターの準備

各キャラクターには以下のコンポーネントが必要です:
- `GameInputManager`: プレイヤー入力と移動を管理
- `CharacterController`: Unity標準の移動コンポーネント
- `Animator` (オプション): キャラクターアニメーション

### 3. カメラの設定

Main Cameraには`CharacterTracker`コンポーネントをアタッチします。
- GameManagerが自動的に検出して設定します
- 手動で設定する場合は、GameManagerの「Camera Tracker」フィールドに設定

## 使用例

### 基本的な使い方

```csharp
// 現在の操作キャラクターを取得
GameInputManager currentCharacter = gameManager.GetActiveCharacter();

// 特定のキャラクターに切り替え（インデックス指定）
gameManager.SwitchToCharacter(0); // 最初のキャラクター
gameManager.SwitchToCharacter(1); // 2番目のキャラクター

// 次のキャラクターに切り替え（順番に切り替え）
gameManager.SwitchToNextCharacter();

// 直接キャラクターを設定
GameInputManager myCharacter = ...;
gameManager.SetActiveCharacter(myCharacter);
```

### ゲーム中の切り替え

デフォルトでは`Tab`キーで次のキャラクターに切り替えることができます。
キーは`GameManager`の「Switch Character Key」フィールドで変更可能です。

## 旧システムからの移行

### 以前の設定方法（非推奨）
- Main CameraのCharacterTrackerで「Target Transform」を手動設定
- 各キャラクターのGameInputManagerで「Camera Tracker」を手動設定

### 新しい設定方法（推奨）
- GameManagerの「Active Character」または「Available Characters」に設定するだけ
- カメラとキャラクターの連携は自動的に処理されます

## トラブルシューティング

### キャラクターが動かない
- GameManagerの「Available Characters」リストにキャラクターが登録されているか確認
- キャラクターに`GameInputManager`コンポーネントがアタッチされているか確認
- GameInputManagerの「Managed By Game Manager」がtrueになっているか確認

### カメラが追従しない
- Main Cameraに`CharacterTracker`がアタッチされているか確認
- GameManagerの「Camera Tracker」フィールドが設定されているか確認（通常は自動）

### 複数のキャラクターが同時に動く
- GameManagerで管理されていないキャラクターがある可能性があります
- すべてのキャラクターの`GameInputManager`で「Managed By Game Manager」をtrueに設定

## 注意事項

1. **GameManagerは1つのシーンに1つだけ配置してください**
2. **操作対象キャラクター以外は自動的に無効化されます**
3. **カメラはGameManagerによって自動的に設定されます**

## システム構成

```
GameManager (シーンに1つ)
  ├─ CharacterTracker (Main Camera)
  └─ GameInputManager[] (複数のキャラクター)
       ├─ Character 1
       ├─ Character 2
       └─ Character 3
```

各キャラクターは独立しており、GameManagerが統一的に管理します。
