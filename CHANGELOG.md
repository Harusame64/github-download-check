# Changelog

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
