"""
Сервер мессенджера для Windows
Поддерживает:
- Текстовые сообщения
- Групповые чаты
- Фото, видео, аудио и файлы
- Хранение базы данных и медиафайлов
"""

import json
import os
import sqlite3
import uuid
import shutil
from datetime import datetime
from http.server import HTTPServer, BaseHTTPRequestHandler
from urllib.parse import parse_qs
import threading
import logging

# Конфигурация
CONFIG_FILE = "config.json"
DEFAULT_CONFIG = {
    "host": "0.0.0.0",
    "port": 8080,
    "database_path": "./data/database.db",
    "media_path": "./data/media",
    "max_file_size": 104857600,  # 100 MB
    "log_level": "INFO"
}

class Config:
    def __init__(self):
        self.config = self.load_config()
    
    def load_config(self):
        if os.path.exists(CONFIG_FILE):
            with open(CONFIG_FILE, 'r', encoding='utf-8') as f:
                return json.load(f)
        else:
            self.save_config(DEFAULT_CONFIG)
            return DEFAULT_CONFIG.copy()
    
    def save_config(self, config):
        with open(CONFIG_FILE, 'w', encoding='utf-8') as f:
            json.dump(config, f, indent=2, ensure_ascii=False)
    
    def get(self, key, default=None):
        return self.config.get(key, default)
    
    def set(self, key, value):
        self.config[key] = value
        self.save_config(self.config)

config = Config()

# Настройка логирования
logging.basicConfig(
    level=getattr(logging, config.get('log_level', 'INFO')),
    format='%(asctime)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

# Создание директорий
def setup_directories():
    db_dir = os.path.dirname(config.get('database_path', './data/database.db'))
    media_path = config.get('media_path', './data/media')
    
    os.makedirs(db_dir, exist_ok=True)
    os.makedirs(media_path, exist_ok=True)
    os.makedirs(os.path.join(media_path, 'images'), exist_ok=True)
    os.makedirs(os.path.join(media_path, 'videos'), exist_ok=True)
    os.makedirs(os.path.join(media_path, 'audio'), exist_ok=True)
    os.makedirs(os.path.join(media_path, 'files'), exist_ok=True)

# База данных
class Database:
    def __init__(self):
        self.db_path = config.get('database_path', './data/database.db')
        self.init_db()
    
    def init_db(self):
        conn = sqlite3.connect(self.db_path)
        cursor = conn.cursor()
        
        # Таблица пользователей
        cursor.execute('''
            CREATE TABLE IF NOT EXISTS users (
                id TEXT PRIMARY KEY,
                phone TEXT UNIQUE,
                name TEXT,
                avatar_path TEXT,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                last_seen TIMESTAMP
            )
        ''')
        
        # Таблица чатов
        cursor.execute('''
            CREATE TABLE IF NOT EXISTS chats (
                id TEXT PRIMARY KEY,
                name TEXT,
                is_group BOOLEAN DEFAULT FALSE,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                created_by TEXT
            )
        ''')
        
        # Таблица участников чата
        cursor.execute('''
            CREATE TABLE IF NOT EXISTS chat_participants (
                chat_id TEXT,
                user_id TEXT,
                joined_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (chat_id, user_id),
                FOREIGN KEY (chat_id) REFERENCES chats(id),
                FOREIGN KEY (user_id) REFERENCES users(id)
            )
        ''')
        
        # Таблица сообщений
        cursor.execute('''
            CREATE TABLE IF NOT EXISTS messages (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                chat_id TEXT,
                sender_id TEXT,
                content TEXT,
                message_type TEXT DEFAULT 'text',
                file_path TEXT,
                timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                is_read BOOLEAN DEFAULT FALSE,
                FOREIGN KEY (chat_id) REFERENCES chats(id),
                FOREIGN KEY (sender_id) REFERENCES users(id)
            )
        ''')
        
        # Таблица файлов
        cursor.execute('''
            CREATE TABLE IF NOT EXISTS files (
                id TEXT PRIMARY KEY,
                original_name TEXT,
                stored_path TEXT,
                mime_type TEXT,
                file_size INTEGER,
                uploaded_by TEXT,
                uploaded_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (uploaded_by) REFERENCES users(id)
            )
        ''')
        
        conn.commit()
        conn.close()
        logger.info("База данных инициализирована")
    
    def execute(self, query, params=(), fetch=False):
        conn = sqlite3.connect(self.db_path)
        conn.row_factory = sqlite3.Row
        cursor = conn.cursor()
        cursor.execute(query, params)
        
        if fetch:
            result = cursor.fetchall()
            conn.close()
            return [dict(row) for row in result]
        else:
            conn.commit()
            last_id = cursor.lastrowid
            conn.close()
            return last_id

db = Database()

# Обработчик запросов
class MessengerHandler(BaseHTTPRequestHandler):
    def log_message(self, format, *args):
        logger.info("%s - %s" % (self.address_string(), format % args))
    
    def send_json_response(self, data, status=200):
        self.send_response(status)
        self.send_header('Content-Type', 'application/json; charset=utf-8')
        self.send_header('Access-Control-Allow-Origin', '*')
        self.end_headers()
        self.wfile.write(json.dumps(data, ensure_ascii=False).encode('utf-8'))
    
    def do_OPTIONS(self):
        self.send_response(200)
        self.send_header('Access-Control-Allow-Origin', '*')
        self.send_header('Access-Control-Allow-Methods', 'GET, POST, OPTIONS')
        self.send_header('Access-Control-Allow-Headers', 'Content-Type')
        self.end_headers()
    
    def do_POST(self):
        content_length = int(self.headers.get('Content-Length', 0))
        post_data = self.rfile.read(content_length).decode('utf-8')
        
        try:
            data = json.loads(post_data)
            action = data.get('action')
            
            if action == 'register':
                self.handle_register(data)
            elif action == 'send_message':
                self.handle_send_message(data)
            elif action == 'get_messages':
                self.handle_get_messages(data)
            elif action == 'create_group':
                self.handle_create_group(data)
            elif action == 'get_chats':
                self.handle_get_chats(data)
            elif action == 'upload_file':
                self.handle_upload_file(data)
            elif action == 'update_settings':
                self.handle_update_settings(data)
            else:
                self.send_json_response({'success': False, 'error': 'Unknown action'})
        except json.JSONDecodeError as e:
            self.send_json_response({'success': False, 'error': f'Invalid JSON: {str(e)}'})
        except Exception as e:
            logger.error(f"Error handling request: {e}")
            self.send_json_response({'success': False, 'error': str(e)})
    
    def do_GET(self):
        if self.path.startswith('/api'):
            query_params = parse_qs(self.path.split('?')[0])
            action = query_params.get('action', [''])[0]
            
            if action == 'get_file':
                self.handle_get_file()
            elif action == 'health':
                self.send_json_response({'status': 'ok', 'timestamp': datetime.now().isoformat()})
            else:
                self.send_json_response({'success': False, 'error': 'Unknown action'})
        else:
            self.send_json_response({'error': 'Not found'}, 404)
    
    def handle_register(self, data):
        phone = data.get('phone', '')
        name = data.get('name', '')
        
        if not phone or not name:
            self.send_json_response({'success': False, 'error': 'Phone and name are required'})
            return
        
        # Проверка существующего пользователя
        existing = db.execute(
            'SELECT id FROM users WHERE phone = ?', 
            (phone,), 
            fetch=True
        )
        
        if existing:
            user_id = existing[0]['id']
            # Обновление имени
            db.execute('UPDATE users SET name = ? WHERE id = ?', (name, user_id))
        else:
            user_id = str(uuid.uuid4())
            db.execute(
                'INSERT INTO users (id, phone, name) VALUES (?, ?, ?)',
                (user_id, phone, name)
            )
        
        logger.info(f"User registered: {name} ({phone}) -> {user_id}")
        self.send_json_response({
            'success': True,
            'userId': user_id,
            'name': name
        })
    
    def handle_send_message(self, data):
        user_id = data.get('userId')
        chat_id = data.get('chatId')
        content = data.get('content', '')
        message_type = data.get('messageType', 'text')
        file_path = data.get('filePath')
        timestamp = data.get('timestamp', datetime.now().isoformat())
        
        if not user_id or not chat_id:
            self.send_json_response({'success': False, 'error': 'userId and chatId are required'})
            return
        
        # Проверка существования чата
        chat = db.execute('SELECT id FROM chats WHERE id = ?', (chat_id,), fetch=True)
        if not chat:
            # Создаем чат если не существует
            db.execute('INSERT INTO chats (id, name, is_group) VALUES (?, ?, ?)',
                      (chat_id, 'Chat', False))
        
        # Сохранение сообщения
        db.execute(
            '''INSERT INTO messages (chat_id, sender_id, content, message_type, file_path, timestamp)
               VALUES (?, ?, ?, ?, ?, ?)''',
            (chat_id, user_id, content, message_type, file_path, timestamp)
        )
        
        logger.info(f"Message sent in chat {chat_id} by {user_id}")
        
        # В реальном приложении здесь была бы отправка через WebSocket
        self.send_json_response({'success': True, 'messageId': 'ok'})
    
    def handle_get_messages(self, data):
        user_id = data.get('userId')
        chat_id = data.get('chatId')
        
        if not chat_id:
            self.send_json_response({'success': False, 'error': 'chatId is required'})
            return
        
        messages = db.execute(
            '''SELECT m.id, m.chat_id, m.sender_id, u.name as sender_name, 
                      m.content, m.message_type, m.file_path, m.timestamp, m.is_read
               FROM messages m
               LEFT JOIN users u ON m.sender_id = u.id
               WHERE m.chat_id = ?
               ORDER BY m.timestamp ASC''',
            (chat_id,),
            fetch=True
        )
        
        self.send_json_response({
            'success': True,
            'messages': messages
        })
    
    def handle_create_group(self, data):
        user_id = data.get('userId')
        group_name = data.get('groupName', 'Group')
        participants = data.get('participants', [])
        
        if not user_id:
            self.send_json_response({'success': False, 'error': 'userId is required'})
            return
        
        group_id = str(uuid.uuid4())
        
        # Создание группы
        db.execute(
            'INSERT INTO chats (id, name, is_group, created_by) VALUES (?, ?, ?, ?)',
            (group_id, group_name, True, user_id)
        )
        
        # Добавление создателя
        db.execute(
            'INSERT INTO chat_participants (chat_id, user_id) VALUES (?, ?)',
            (group_id, user_id)
        )
        
        # Добавление участников
        for participant_id in participants:
            if participant_id != user_id:
                db.execute(
                    'INSERT INTO chat_participants (chat_id, user_id) VALUES (?, ?)',
                    (group_id, participant_id)
                )
        
        logger.info(f"Group created: {group_name} ({group_id}) by {user_id}")
        
        self.send_json_response({
            'success': True,
            'groupId': group_id
        })
    
    def handle_get_chats(self, data):
        user_id = data.get('userId')
        
        if not user_id:
            self.send_json_response({'success': False, 'error': 'userId is required'})
            return
        
        # Получение чатов пользователя
        chats = db.execute(
            '''SELECT c.id, c.name, c.is_group, c.created_at,
                      (SELECT COUNT(*) FROM messages WHERE chat_id = c.id) as message_count,
                      (SELECT content FROM messages WHERE chat_id = c.id ORDER BY timestamp DESC LIMIT 1) as last_message,
                      (SELECT timestamp FROM messages WHERE chat_id = c.id ORDER BY timestamp DESC LIMIT 1) as last_message_time
               FROM chats c
               JOIN chat_participants cp ON c.id = cp.chat_id
               WHERE cp.user_id = ?
               ORDER BY last_message_time DESC''',
            (user_id,),
            fetch=True
        )
        
        self.send_json_response({
            'success': True,
            'chats': chats
        })
    
    def handle_upload_file(self, data):
        # В реальной реализации здесь будет обработка multipart/form-data
        user_id = data.get('userId')
        file_data = data.get('file')
        
        if not user_id or not file_data:
            self.send_json_response({'success': False, 'error': 'userId and file data required'})
            return
        
        file_id = str(uuid.uuid4())
        media_path = config.get('media_path', './data/media')
        file_path = os.path.join(media_path, 'files', f"{file_id}")
        
        # Сохранение файла (упрощенно)
        try:
            with open(file_path, 'wb') as f:
                f.write(file_data.encode('utf-8'))  # В реальности будут бинарные данные
            
            db.execute(
                'INSERT INTO files (id, original_name, stored_path, uploaded_by) VALUES (?, ?, ?, ?)',
                (file_id, 'file.dat', file_path, user_id)
            )
            
            self.send_json_response({
                'success': True,
                'fileId': file_id,
                'filePath': file_path
            })
        except Exception as e:
            self.send_json_response({'success': False, 'error': str(e)})
    
    def handle_update_settings(self, data):
        # Обновление настроек сервера
        new_settings = data.get('settings', {})
        
        for key, value in new_settings.items():
            config.set(key, value)
        
        logger.info("Settings updated")
        self.send_json_response({'success': True})
    
    def handle_get_file(self):
        # Отдача файла клиенту
        file_id = parse_qs(self.path).get('fileId', [''])[0]
        
        if not file_id:
            self.send_json_response({'success': False, 'error': 'fileId required'}, 400)
            return
        
        file_info = db.execute(
            'SELECT stored_path, mime_type, original_name FROM files WHERE id = ?',
            (file_id,),
            fetch=True
        )
        
        if not file_info:
            self.send_json_response({'success': False, 'error': 'File not found'}, 404)
            return
        
        file_path = file_info[0]['stored_path']
        
        if os.path.exists(file_path):
            self.send_response(200)
            self.send_header('Content-Type', file_info[0]['mime_type'] or 'application/octet-stream')
            self.send_header('Content-Disposition', f'attachment; filename="{file_info[0]["original_name"]}"')
            self.end_headers()
            
            with open(file_path, 'rb') as f:
                self.wfile.write(f.read())
        else:
            self.send_json_response({'success': False, 'error': 'File not found on disk'}, 404)

# Интерфейс управления сервером
class ServerGUI:
    def __init__(self):
        self.server = None
        self.server_thread = None
    
    def start_server(self):
        host = config.get('host', '0.0.0.0')
        port = config.get('port', 8080)
        
        try:
            self.server = HTTPServer((host, port), MessengerHandler)
            logger.info(f"Server started on {host}:{port}")
            print(f"\n{'='*50}")
            print(f"Сервер запущен на {host}:{port}")
            print(f"База данных: {config.get('database_path')}")
            print(f"Медиафайлы: {config.get('media_path')}")
            print(f"{'='*50}\n")
            print("Нажмите Ctrl+C для остановки сервера\n")
            self.server.serve_forever()
        except OSError as e:
            logger.error(f"Failed to start server: {e}")
            print(f"Ошибка запуска сервера: {e}")
    
    def stop_server(self):
        if self.server:
            self.server.shutdown()
            logger.info("Server stopped")

def main():
    print("\n" + "="*50)
    print(" Мессенджер - Серверная часть")
    print("="*50 + "\n")
    
    # Инициализация
    setup_directories()
    
    # Отображение текущей конфигурации
    print("Текущая конфигурация:")
    print(f"  Хост: {config.get('host', '0.0.0.0')}")
    print(f"  Порт: {config.get('port', 8080)}")
    print(f"  База данных: {config.get('database_path')}")
    print(f"  Медиафайлы: {config.get('media_path')}")
    print(f"  Макс. размер файла: {config.get('max_file_size', 104857600) / 1024 / 1024} MB")
    print()
    
    # Запуск сервера
    gui = ServerGUI()
    
    try:
        gui.start_server()
    except KeyboardInterrupt:
        print("\n\nОстановка сервера...")
        gui.stop_server()
        print("Сервер остановлен")

if __name__ == '__main__':
    main()
