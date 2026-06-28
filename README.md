# MoreCheckmarks RU (fork)

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Release](https://img.shields.io/badge/release-v2.1.0.7-blue)](https://github.com/kabzon93region/MoreCheckmarksRU/releases/tag/v2.1.0.7)
[![Download zip](https://img.shields.io/badge/download-zip-brightgreen)](https://github.com/kabzon93region/MoreCheckmarksRU/releases/tag/v2.1.0.7)
[![EFT](https://img.shields.io/badge/EFT-16%2E9-orange)](https://www.escapefromtarkov.com/)
[![SPT](https://img.shields.io/badge/SPT-4.0.13-blue)](https://sp-tarkov.com/)
[![Fika](https://img.shields.io/badge/Fika-2%2E3%2Ex-purple)](https://github.com/project-fika/Fika-Plugin)
[![BepInEx](https://img.shields.io/badge/BepInEx-5%2E4%2Ex-yellow)](https://github.com/BepInEx/BepInEx)
![Deployment](https://img.shields.io/badge/deployment-server_client-lightgrey)

Русский форк MoreCheckmarks (TommySoucy) для SPT 4 + Fika 2.3. Улучшенные hideout-подсказки, перевод UI/тултипов, исправления совместимости.

| | |
|---|---|
| **Разработчик** | [kabzon93region](https://github.com/kabzon93region) |
| **Версия** | 2.1.0.7 |
| **GitHub** | [MoreCheckmarksRU](https://github.com/kabzon93region/MoreCheckmarksRU) |
| **Deployment** | `(server_client)` |
| **Тип** | combo (client + server) |

## О моде

Русский форк [MoreCheckmarks](https://github.com/TommySoucy/MoreCheckmarks) (TommySoucy) для SPT 4 + Fika 2.3: улучшенные hideout-подсказки, перевод UI/тултипов, исправления совместимости.

## Требования

- Удалить оригинальный **MoreCheckmarks** (GUID `VIP.TommySoucy.MoreCheckmarks`) перед установкой
- **SPT** 4.0.x, **BepInEx** 5.4.x
- Серверная часть — upstream MoreCheckmarksBackend (в архиве)

## Установка

1. Распаковать zip в корень игры (`EscapeFromTarkov.exe`, `BepInEx/`, `SPT/`).
2. Клиент: `BepInEx/plugins/MoreCheckmarks/MoreCheckmarks.dll`
3. Сервер: `SPT/user/mods/MoreCheckmarksBackend/`
| Компонент | Путь |
|-----------|------|
| Клиент | `BepInEx/plugins/MoreCheckmarks/` |
| Сервер | `SPT/user/mods/MoreCheckmarksBackend/` |

## Известные проблемы

- Серверная часть без изменений (upstream backend)
- Только один MoreCheckmarks GUID в BepInEx — конфликт с оригиналом

## Поддержать проект

Разовый донат картой РФ, СБП, ЮMoney, VK Pay:
**[DonationAlerts → kabzon93region](https://www.donationalerts.com/r/kabzon93region)**
