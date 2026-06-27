# Publish to GitHub — MoreCheckmarks RU (fork)

**Статус:** `ready`  
**GitHub:** Release + zip  
**Версия:** `2.1.0.7`  
**Deployment:** `(server_client)`

## 1. Подготовка (уже сделано этим скриптом)

Папка: `github-repos/MoreCheckmarksRU/`

## 2. Создать репозиторий и запушить

```powershell
cd github-repos/MoreCheckmarksRU
git init
git add .
git commit -m "Source backup MoreCheckmarks RU (fork) v2.1.0.7"
git branch -M main
git remote add origin https://github.com/kabzon93region/MoreCheckmarksRU.git
git push -u origin main
```

Или автоматически:

```powershell
python CURSORAIMODING/tools/publish/publish_github_release.py MoreCheckmarksRU --create-repo
```

## 3. GitHub Release

Прикрепить zip (только игровые файлы, без INSTALL.md):

`\\Servant\data\Games\EscapeFromTarkov4\CURSORAIMODING\releases\MoreCheckmarksRU_(server_client)_v2.1.0.7_2026-06-27.zip`

```powershell
gh release create v2.1.0.7 "\\Servant\data\Games\EscapeFromTarkov4\CURSORAIMODING\releases\MoreCheckmarksRU_(server_client)_v2.1.0.7_2026-06-27.zip" ^
  --title "MoreCheckmarks RU (fork) v2.1.0.7" ^
  --notes-file CHANGELOG.md
```

## Описание репозитория (suggested)

Русский форк MoreCheckmarks (TommySoucy) для SPT 4 + Fika 2.3. Улучшенные hideout-подсказки, перевод UI/тултипов, исправления совместимости.

SPT 4.0 + Fika 2.3 headless stack. Deployment: `(server_client)`.
