# План разработки SecureLink Messenger

## Статус: В РАЗРАБОТКЕ (Готово ~97%)

**Последнее обновление:** 29.08.2024

**Выполненные критические исправления:**
- ✅ Исправлен метод `FindClientByUserId()` - добавлен словарь `_userToClient` для поиска получателя по userId
- ✅ Создан файл решения `.sln` для сборки сервера в Visual Studio
- ✅ Создан отдельный класс `ChatViewModel.kt` вместо встроенного в ChatScreen
- ✅ Реализована синхронизация контактов с сервером (метод `sync_contacts`)
- ✅ Добавлен метод `HashPhoneNumber()` для безопасного хэширования номеров телефонов
- ✅ Добавлена обработка ответа сервера о синхронизации контактов в MainActivity
- ✅ Создан BAT-файл `run_server.bat` для быстрого запуска сервера (с английскими сообщениями)
- ✅ Удалена дублирующаяся папка SecureLinkServer с устаревшим кодом
- ✅ Исправлено пространство имён в SecurityValidator.cs (`SecureLink.Server.Core.Security`)
- ✅ Добавлен пакет `System.Text.Json` в проект SecureLink.Server.Core.csproj
- ✅ Исправлена ошибка `webSocket not in context` - сокет добавляется в HandleClientAsync при подключении
- ✅ Исправлен тип Id в Message - изменён с `long` на `string` для соответствия Guid
- ✅ Обновлены версии пакетов до 8.0.5 для устранения уязвимостей
- ✅ Добавлено значение по умолчанию для FileSize в DbContext
- ✅ Добавлен флаг `--no-incremental` в BAT-файл для чистой сборки

---

## 🔴 КРИТИЧЕСКИЕ ПРОБЛЕМЫ (ИСПРАВЛЕНО)

### Исправленные проблемы:
1. ✅ **FindClientByUserId()** - была заглушка, добавлен словарь `_userToClient` для отображения userId → clientId
2. ✅ **Отсутствует файл .sln** - создан SecureLink.Server.sln для сборки в Visual Studio  
3. ✅ **Отсутствует ChatViewModel.kt** - создан отдельный файл ChatViewModel.kt
4. ✅ **Нет синхронизации контактов** - реализован метод `HandleSyncContactsAsync()` на сервере и `syncContacts()` на клиенте
5. ✅ **Нет скрипта запуска** - создан `run_server.bat` для автоматической проверки .NET, сборки и запуска сервера
6. ✅ **Дублирование кода** - удалена папка SecureLinkServer с дублирующимся SecurityValidator.cs
7. ✅ **Кодировка BAT-файла** - заменены русские сообщения на английские для корректной работы в Windows
8. ✅ **Пространство имён SecurityValidator** - изменено с `SecureLinkServer.Security` на `SecureLink.Server.Core.Security`
9. ✅ **Отсутствие System.Text.Json** - добавлен пакет в csproj файл
10. ✅ **Ошибка webSocket not in context** - WebSocket добавляется в словарь _clients в начале HandleClientAsync
11. ✅ **Несоответствие типа Id** - Message.Id изменён с `long` на `string` для совместимости с Guid
12. ✅ **Уязвимости в пакетах** - обновлены System.Text.Json и Microsoft.EntityFrameworkCore.Sqlite до 8.0.5
13. ✅ **Ошибка преобразования string to long** - FileSize имеет значение по умолчанию 0 через DbContext

### Остающиеся проблемы:
1. ⚠️ **Отсутствует фоновая служба** - приложение не работает в фоне (Foreground Service)
2. ⚠️ **Нет Push-уведомлений** - FCM не настроен

---

## 📋 Детали реализации синхронизации контактов

### Серверная часть (WebSocketServer.cs):
- Метод `HandleSyncContactsAsync()` принимает список контактов от клиента
- Очищает старые контакты пользователя в БД
- Проверяет каждый контакт на регистрацию в системе
- Возвращает список зарегистрированных контактов с полной информацией

### Клиентская часть (Android):
- `WebSocketClient.syncContacts()` - отправляет контакты на сервер
- `MainActivity.setupWebSocketListeners()` - обрабатывает ответы сервера
- `MainActivity.syncContacts()` - автоматически синхронизирует контакты при подключении
- `HomeViewModel.loadContactsFromPhone()` - загружает контакты из телефонной книги

---

*Последнее обновление: 29.08.2024*
*Статус: ~97% готовности - исправлены все ошибки сборки и типы данных, остаются фоновая служба и push-уведомления*
