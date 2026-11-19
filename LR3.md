# Лабораторная работа №3: Контейнеризация веб-сервера в Docker и раздача сжатых файлов со стороны Nginx

**Студентка:** Карелина Мария Дмитриевна

**Группа:** 23-СТ

## Цель работы

Упаковать WebGL-сборку игры вместе с веб-сервером Nginx в Docker-образ, настроить корректную раздачу сжатых Gzip-файлов Unity (заголовок `Content-Encoding: gzip`) и автоматизировать сборку и публикацию образа в GitHub Container Registry (GHCR) через GitHub Actions.

## 1. Включение сжатия Gzip в Unity 6

В `File → Build Profiles → Player Settings → Player → WebGL → Publishing Settings`:

* **Compression Format** изменён с `Disabled` на `Gzip`;
* галочка **Decompression Fallback** снята — распаковку выполняет браузер по заголовку от сервера, а не JS-распаковщик.

В `ProjectSettings/ProjectSettings.asset` это соответствует строкам `webGLCompressionFormat: 1` и `webGLDecompressionFallback: 0`.

После этого проект пересобран командой из ЛР №1:

```
"D:\Programs\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe" -batchmode -nographics -executeMethod BuildManager.BuildWebGL -quit -logFile build_webgl.log
```

В папке `Builds/WebGL/Build/` файлы получили расширения `.framework.js.gz`, `.wasm.gz` и `.data.gz`.

<!-- Скриншот: папка Builds/WebGL/Build с .gz файлами -->

## 2. Конфигурация веб-сервера `nginx.conf`

В корне проекта создан файл [`nginx.conf`](nginx.conf). Для каждого типа архивов Unity задан правильный MIME-тип и добавлен заголовок `Content-Encoding: gzip`:

| Файл | Content-Type | Content-Encoding |
|---|---|---|
| `*.js.gz` | `application/javascript` | `gzip` |
| `*.wasm.gz` | `application/wasm` | `gzip` |
| `*.data.gz` | `application/octet-stream` | `gzip` |

## 3. Dockerfile

В корне проекта создан [`Dockerfile`](Dockerfile):

1. базовый образ `nginx:alpine`;
2. удаление стандартного `default.conf`;
3. копирование своего `nginx.conf` в `/etc/nginx/conf.d/`;
4. очистка `/usr/share/nginx/html/` и копирование туда сборки `Builds/WebGL/`;
5. `EXPOSE 80`;
6. запуск `nginx -g "daemon off;"`.

## 4. Локальное тестирование контейнера

```
docker build -t platformer-compressed:v1 .
docker run -d -p 8080:80 --name unity_compressed_container platformer-compressed:v1
```

Игра открыта по адресу `http://localhost:8080`. В DevTools (F12 → Network, перезагрузка Ctrl + F5) у файла `.wasm.gz` в Headers присутствует `Content-Encoding: gzip`.

Проверка заголовков через `curl -I`:

```
--- game.framework.js.gz
HTTP/1.1 200 OK
Content-Type: application/javascript
Content-Encoding: gzip
--- game.wasm.gz
HTTP/1.1 200 OK
Content-Type: application/wasm
Content-Encoding: gzip
--- game.data.gz
HTTP/1.1 200 OK
Content-Type: application/octet-stream
Content-Encoding: gzip
```

<!-- Скриншот: DevTools → Network → .wasm.gz → Headers с Content-Encoding: gzip -->

Остановка контейнера:

```
docker stop unity_compressed_container && docker rm unity_compressed_container
```

## 5. Сборка Docker-образа в GitHub Actions

В `.github/workflows/main.yml` добавлена третья задача `build_docker_image`:

* запускается после `sanity_check` (`needs: sanity_check`);
* имеет права `packages: write` для публикации в GitHub Packages;
* создаёт временные файлы-заглушки `game.framework.js.gz`, `game.wasm.gz`, `game.data.gz` (папка `Builds/` исключена `.gitignore`, поэтому на облачном сервере реального билда нет);
* авторизуется в `ghcr.io` через системный `GITHUB_TOKEN`;
* собирает и публикует образ с помощью `docker/build-push-action` под тегами:
  * `ghcr.io/medrss/platformer-game:latest`
  * `ghcr.io/medrss/platformer-game:${{ github.sha }}`

## 6. Отправка изменений на GitHub

Изменения (`nginx.conf`, `Dockerfile`, `main.yml`, настройки сжатия) выполнены в ветке `LR3`, отправлены на GitHub и объединены с `main` через Pull Request.

## 7. Проверка результатов

* Во вкладке **Actions** все три задачи (`sanity_check`, `mirror_repo`, `build_docker_image`) завершились успешно.
* Во вкладке **Packages** профиля появился пакет `platformer-game` с тегами `latest` и `sha` коммита.

<!-- Скриншот: Actions — три зелёные задачи -->

<!-- Скриншот: Packages — platformer-game -->

## Ответы на контрольные вопросы

**1. Какую проблему решает строка `add_header Content-Encoding gzip;`?**
Без неё Nginx отдаёт `.gz`-файл как обычный бинарный файл, и браузер получает сжатые байты, не зная, что их нужно распаковать, — загрузчик Unity падает с ошибкой «Unable to parse…». Заголовок сообщает браузеру, что содержимое сжато Gzip, и браузер сам прозрачно распаковывает его перед передачей в JS/WebAssembly.

**2. Зачем в пайплайне создаются временные файлы через `touch Builds/WebGL/Build/game.wasm.gz`?**
Папка `Builds/` исключена в `.gitignore`, а облачной сборки Unity пока нет (она появится в следующих ЛР). Без заглушек команда `COPY Builds/WebGL/ ...` в Dockerfile упала бы из-за отсутствия папки. Заглушки повторяют структуру настоящей сжатой сборки и позволяют проверить весь путь сборки и публикации образа.

**3. В какую директорию контейнера Nginx копируются файлы WebGL-игры?**
В `/usr/share/nginx/html/` — это корневая веб-папка (`root`) официального образа Nginx.

**4. Зачем публиковать образ под двумя тегами (`latest` и `github.sha`)?**
`latest` всегда указывает на самую свежую версию — удобно для запуска «последней сборки». Тег `github.sha` неизменяемо привязывает образ к конкретному коммиту: можно точно знать, какой код внутри, и в любой момент откатиться на нужную версию.

## Вывод

В ходе работы включено Gzip-сжатие WebGL-сборки, написана конфигурация Nginx, отдающая архивы Unity с заголовком `Content-Encoding: gzip`, и создан Docker-образ «Nginx + игра». Сборка и публикация образа в GitHub Container Registry автоматизирована в GitHub Actions.
