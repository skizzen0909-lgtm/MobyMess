package com.securelink.messenger.data.remote

import android.util.Log
import com.google.gson.Gson
import com.google.gson.JsonParser
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import java.net.InetSocketAddress
import java.nio.ByteBuffer
import java.nio.charset.StandardCharsets
import org.java_websocket.client.WebSocketClient
import org.java_websocket.handshake.ServerHandshake

/**
 * WebSocket клиент для подключения к серверу SecureLink
 */
class SecureLinkWebSocketClient(
    private val serverIp: String,
    private val serverPort: Int,
    private val token: String,
    private val gson: Gson
) : WebSocketClient(java.net.URI("ws://$serverIp:$serverPort/?token=$token")) {

    private val _connectionState = MutableStateFlow<ConnectionState>(ConnectionState.Disconnected)
    val connectionState: StateFlow<ConnectionState> = _connectionState

    private val messageListeners = mutableListOf<(MessagePacket) -> Unit>()
    private val binaryListeners = mutableListOf<(ByteArray, String, MessageType) -> Unit>()

    var userId: String? = null
        private set

    override fun onOpen(handshakedata: ServerHandshake?) {
        Log.d(TAG, "WebSocket connected")
        _connectionState.value = ConnectionState.Connected
    }

    override fun onMessage(data: ByteArray?) {
        if (data == null) return

        try {
            // Проверяем есть ли метаданные (первые 4 байта - длина JSON)
            if (data.size >= 4) {
                val metadataLength = ByteBuffer.wrap(data, 0, 4).int
                if (metadataLength > 0 && metadataLength < data.size) {
                    val metadataJson = String(data, 4, metadataLength, StandardCharsets.UTF_8)
                    val metadata = JsonParser.parseString(metadataJson).asJsonObject
                    
                    val typeValue = metadata.get("type")?.asInt ?: MessageType.FILE_MESSAGE.value
                    val fileName = metadata.get("fileName")?.asString ?: "unknown.bin"
                    val type = MessageType.fromValue(typeValue)
                    
                    val fileData = data.copyOfRange(4 + metadataLength, data.size)
                    
                    binaryListeners.forEach { listener ->
                        listener(fileData, fileName, type)
                    }
                    return
                }
            }
            
            // Если нет метаданных, просто передаем как бинарные данные
            binaryListeners.forEach { listener ->
                listener(data, "unknown.bin", MessageType.FILE_MESSAGE)
            }
        } catch (e: Exception) {
            Log.e(TAG, "Error processing binary message", e)
        }
    }

    override fun onMessage(text: String?) {
        if (text == null) return

        try {
            val packet = gson.fromJson(text, MessagePacket::class.java)
            
            // Обрабатываем ответ аутентификации
            if (packet.type == MessageType.AUTH_RESPONSE.value) {
                packet.payload?.let { payload ->
                    val authResponse = gson.fromJson(payload, AuthResponseData::class.java)
                    if (authResponse.success) {
                        userId = authResponse.userId
                    }
                }
            }
            
            messageListeners.forEach { listener ->
                listener(packet)
            }
        } catch (e: Exception) {
            Log.e(TAG, "Error parsing message", e)
        }
    }

    override fun onClose(code: Int, reason: String?, remote: Boolean) {
        Log.d(TAG, "WebSocket closed: $reason (code: $code, remote: $remote)")
        _connectionState.value = ConnectionState.Disconnected
        userId = null
    }

    override fun onError(ex: Exception?) {
        Log.e(TAG, "WebSocket error", ex)
        _connectionState.value = ConnectionState.Error(ex?.message ?: "Unknown error")
    }

    /**
     * Отправить текстовое сообщение
     */
    fun sendMessage(packet: MessagePacket) {
        if (isOpen) {
            send(gson.toJson(packet))
        } else {
            Log.w(TAG, "Cannot send message - WebSocket is not open")
        }
    }

    /**
     * Отправить бинарные данные (файл)
     */
    fun sendFile(data: ByteArray, fileName: String, type: MessageType) {
        if (!isOpen) {
            Log.w(TAG, "Cannot send file - WebSocket is not open")
            return
        }

        try {
            // Создаем метаданные
            val metadata = FileMetadata(
                type = type.value,
                fileName = fileName,
                size = data.size
            )
            val metadataJson = gson.toJson(metadata)
            val metadataBytes = metadataJson.toByteArray(StandardCharsets.UTF_8)

            // Создаем буфер: 4 байта (длина метаданных) + метаданные + данные файла
            val buffer = ByteBuffer.allocate(4 + metadataBytes.size + data.size)
            buffer.putInt(metadataBytes.size)
            buffer.put(metadataBytes)
            buffer.put(data)

            send(buffer.array())
            Log.d(TAG, "File sent: $fileName (${data.size} bytes)")
        } catch (e: Exception) {
            Log.e(TAG, "Error sending file", e)
        }
    }

    /**
     * Добавить слушатель сообщений
     */
    fun addMessageListener(listener: (MessagePacket) -> Unit) {
        messageListeners.add(listener)
    }

    /**
     * Добавить слушатель бинарных данных
     */
    fun addBinaryListener(listener: (ByteArray, String, MessageType) -> Unit) {
        binaryListeners.add(listener)
    }

    /**
     * Удалить слушатель сообщений
     */
    fun removeMessageListener(listener: (MessagePacket) -> Unit) {
        messageListeners.remove(listener)
    }

    /**
     * Аутентифицироваться на сервере
     */
    fun authenticate(phoneNumber: String, deviceId: String) {
        val authRequest = AuthRequest(
            phoneNumber = phoneNumber,
            deviceId = deviceId
        )
        
        val packet = MessagePacket(
            type = MessageType.AUTH.value,
            payload = gson.toJson(authRequest)
        )
        sendMessage(packet)
    }

    /**
     * Отправить ping
     */
    fun sendPing() {
        val packet = MessagePacket(type = MessageType.PING.value)
        sendMessage(packet)
    }

    /**
     * Отправить текстовое сообщение в чат
     */
    fun sendTextMessage(chatId: String, recipientId: String?, text: String) {
        val messageData = TextMessageData(
            chatId = chatId,
            recipientId = recipientId,
            text = text
        )
        
        val packet = MessagePacket(
            type = MessageType.TEXT_MESSAGE.value,
            senderId = userId,
            payload = gson.toJson(messageData)
        )
        sendMessage(packet)
    }

    /**
     * Синхронизировать контакты
     */
    fun syncContacts(contacts: List<ContactData>) {
        val syncData = ContactsSyncData(contacts = contacts)
        
        val packet = MessagePacket(
            type = MessageType.SYNC_CONTACTS.value,
            payload = gson.toJson(syncData)
        )
        sendMessage(packet)
    }

    /**
     * Создать группу
     */
    fun createGroup(name: String, memberIds: List<String>?) {
        val groupData = CreateGroupData(
            name = name,
            memberIds = memberIds
        )
        
        val packet = MessagePacket(
            type = MessageType.CREATE_GROUP.value,
            payload = gson.toJson(groupData)
        )
        sendMessage(packet)
    }

    companion object {
        private const val TAG = "SecureLinkWS"
    }
}

/**
 * Состояние подключения
 */
sealed class ConnectionState {
    object Connected : ConnectionState()
    object Disconnected : ConnectionState()
    data class Error(val message: String) : ConnectionState()
    object Connecting : ConnectionState()
}
