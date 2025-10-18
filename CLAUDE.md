# CLAUDE.md

このファイルは、Claude Code (claude.ai/code) がこのリポジトリでコードを扱う際のガイダンスを提供します。


## Claude Codeの作業ルール

- 必ず日本語で応答してください。
- ドキュメントやコメントは日本語で記載してください。
- 作業内容は「./worklog/yyyymmdd_hhss.md」の形でレポートを作成してください。レポートの内容は、「変更した内容の概要を3行程度で」「なぜそのように変更しようと考えたかを3行程度で」記載してください。
- 新しく作業を開始したときは、worklogの直近5件の内容を把握してください。
- コード作成時は、ドキュメントコメントを整備してください。

- UniTaskを導入しています。状況に応じて積極的に利用してください。
- R3を導入していますが、こちらは明示的に指示しない限り利用しないでください。一部、ユーザデータ変動に応じてUIやゲーム処理を交通整理する目的で検討中です。
- C#のリフレクションは極力使わないでください。必要な際は確認してください。

## Repository Status

This is currently an empty Git repository. When code is added, update this file with:

1. **Build Commands**: How to build, lint, and run tests
2. **Architecture**: High-level code structure and key patterns
3. **Development Workflow**: Commands for running single tests, development servers, etc.

## Next Steps

When adding code to this repository, consider updating this file with project-specific information to help future Claude Code instances work more effectively.