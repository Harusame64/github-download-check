# リリース手順書

本書は、GitHub Download Check の新しいバージョンをリリースする際の手順をまとめたものです。

## 1. 事前準備
- すべての変更が `main` ブランチにマージされ、テストが完了していることを確認してください。
- [GitHub CLI (gh)](https://cli.github.com/) がインストールされ、適切にログインされていることを確認してください。

## 2. バージョン情報の更新
1. `CHANGELOG.md` に新しいバージョンの修正・追加内容を追記します。
   - 日付は `YYYY-MM-DD` 形式で記載します。

## 3. ディストリビューションのビルド
プロジェクトのルートディレクトリで、PowerShell を使用してビルドスクリプトを実行します。
```powershell
./build-dist.ps1 -Version "1.1.1"
```
実行後、`dist/` フォルダ内に以下のファイルが生成されていることを確認してください。
- `GitHubDownloadCheck-1.1.1.msi`
- `GitHubDownloadCheck-1.1.1-win-x64.zip`

## 4. GitHub Release の作成（配備）
`gh` コマンドを使用して、GitHub 上にリリースを作成し、バイナリをアップロードします。
**注意：バイナリファイルを Git リポジトリ自体にコミットしないでください。**

```powershell
# リリースノートの内容を変数に格納
$notes = "### Fixed`n- 修正内容1`n- 修正内容2"

# リリースの作成とファイルの添付
gh release create v1.1.1 dist/GitHubDownloadCheck-1.1.1* --title "v1.1.1" --notes $notes
```

## 5. 後片付け
- 必要に応じて、ローカルの `dist/` フォルダ内のファイルを削除してクリーンアップしてください（`.gitignore` で除外されているため、そのままにしても Git には影響しません）。
