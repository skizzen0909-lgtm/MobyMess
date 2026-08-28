# SecureLink Messenger

Мессенджер с端到端 шифрованием для Android и Windows.

## Структура проекта

```
SecureLink/
├── Server/                     # Серверная часть (Windows)
│   ├── SecureLink.Server.Wpf/  # WPF интерфейс сервера
│   └── SecureLink.Server.Core/ # Ядро сервера
└── Client/                     # Клиентская часть (Android)
    └── SecureLinkMessenger/    # Android приложение
```

## Возможности

### Клиент (Android)
- ✅ Обмен текстовыми сообщениями
- ✅ Отправка и предпросмотр фото
- ✅ Отправка видео
- ✅ Голосовые сообщения
- ✅ Передача файлов
- ✅ Создание групп
- ✅ Контакты из телефонной книги
- ✅ Настройки подключения (IP, порт)

### Сервер (Windows)
- ✅ WebSocket сервер
- ✅ Хранение сообщений (SQLite)
- ✅ Хранение файлов
- ✅ Интерфейс управления
- ✅ Настройки (порт, пути к БД и файлам)

## Установка сервера

1. Установите .NET 8 SDK для Windows
2. Откройте `SecureLink/Server/SecureLink.Server.Wpf/SecureLink.Server.Wpf.csproj` в Visual Studio
3. Соберите проект
4. Запустите `SecureLinkServer.exe`
5. Настройте IP адрес и порт в интерфейсе
6. Нажмите "Запустить"

## Сборка клиента

1. Откройте проект в Android Studio
2. Синхронизируйте Gradle
3. Соберите APK: `Build -> Build Bundle(s) / APK(s) -> Build APK(s)`

## Протокол обмена

### Авторизация
```json
{
  "action": "auth",
  "phoneNumber": "+79990000000"
}
```

### Отправка сообщения
```json
{
  "action": "send_message",
  "senderId": "user-id",
  "type": "TEXT",
  "content": "Привет!",
  "recipientId": "recipient-id",
  "timestamp": 1234567890
}
```

### Типы сообщений
- `TEXT` - текст
- `IMAGE` - изображение
- `VIDEO` - видео
- `AUDIO` - голосовое сообщение
- `FILE` - файл
- `SYSTEM` - системное сообщение

## Требования

### Сервер
- Windows 10/11
- .NET 8 Runtime
- Статический IP адрес

### Клиент
- Android 8.0+ (API 26)
- Разрешения: контакты, камера, микрофон, хранилище

## Лицензия

MIT License
