# Platformer Microgame — лабораторные работы по CI/CD

**Студентка:** Карелина Мария Дмитриевна

**Группа:** 23-СТ

**Вариант:** 16

## Лабораторные работы

| № | Название | Отчёт |
|---|---|---|
| 1 | Автоматизация сборки 2D игры через CLI | [LR1.md](LR1.md) |
| 2 | Настройка GitHub Actions и автоматическая проверка проекта (Sanity Check) | [LR2.md](LR2.md) |
| 3 | Контейнеризация веб-сервера в Docker и раздача сжатых файлов со стороны Nginx | [LR3.md](LR3.md) |
| 4 | Автоматизация тестирования (Unity Test Runner) в CI/CD | [LR4.md](LR4.md) |
| 5 | Автоматическая компиляция WebGL-игры в облаке и деплой на GitHub Pages | [LR5.md](LR5.md) |

Игра онлайн: https://medrss.github.io/platformer-microgame-labs/

## Пайплайн CI/CD

```
sanity_check → run_tests → build_game → build_docker_image (ghcr.io/medrss/platformer-game)
                         │            → deploy_to_pages   (GitHub Pages)
                         → mirror_repo
```

Тест по варианту 16 (`PlayerEnteredVictoryZone`): [`Assets/Tests/PlayMode/VictoryZoneTests.cs`](Assets/Tests/PlayMode/VictoryZoneTests.cs)
