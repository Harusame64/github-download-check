# Changelog

## [1.1.4] - 2026-04-22

### Changed
- インストール先をユーザーフォルダ (`LocalAppData`) に変更し、管理者権限なしでインストール可能に改善
- MSI のインストールスコープを Per-User に変更

## [1.1.3] - 2026-04-22

### Changed
- MSI インストーラーにメタデータを追加し、信頼性を向上
- README に Windows SmartScreen 警告への対処法を追記

## [1.1.2] - 2026-04-22

### Changed
- トレンド履歴テーブル（取得日）の表示を最新順（降順）に変更
- 「取得日時」の表記を「取得日」に変更し、時間表示を省略（日単位の集計・増加分表示を強調）

## [1.1.1] - 2026-04-22

### Fixed
- 同一日に複数回収計した場合に、チャートに収集回数毎のデータが表示される問題を修正（1日の最終収集データに集約）
- 詳細画面でスクロールバーを一気に下げた際に、チャートが描画されない問題を修正（スクロール時の強制再描画を追加）

## [1.1.0] - 2026-04-11

### Added
- Internationalization (i18n) support: Japanese (default) and English
- Language auto-detection from OS locale at startup
- `tools/translate-resx.ps1` — PowerShell script for automated translation via Claude API

---

## [1.0.4] - 2026-04-11

### Fixed
- Draft releases are now excluded from all download counts and charts
- Asset summary card now correctly reflects the most downloaded non-draft asset

## [1.0.3] - 2026-04-11

### Changed
- Release and asset charts changed from vertical bar to horizontal bar (RowSeries) for better readability
- Asset chart now shows assets in descending order (highest downloads on top)
- Chart tooltips hidden; download counts now displayed as data labels at bar end
- Mouse wheel scrolling now works correctly over charts (`IsHitTestVisible="False"`)

## [1.0.2] - 2026-04-11

### Added
- Initial public release
- GitHub release download count viewer
- Repository management (add/remove, PAT support)
- Summary cards: total downloads, release count, most popular release
- Charts: downloads by release, downloads by asset (top 10)
- Period download chart (daily/weekly/monthly) from snapshot diff
- Trend chart and snapshot history table
- Dark/Light theme support
- MSI installer + standalone ZIP distribution
