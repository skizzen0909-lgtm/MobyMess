package com.secutelink.messenger.network

import android.content.Context
import android.util.Log
import com.google.gson.Gson
import com.google.gson.reflect.TypeToken
import com.secutelink.messenger.data.model.*
import okhttp3.*
import java.io.IOException
import java.util.concurrent.TimeUnit

class WebSocketClient(private val context: Context) {
    private var webSocket: WebSocket? = null
    private val gson = Gson()
    private var serverAddress: String = ""
    private var isConnected = false
    private var messageListener: ((String) -> Unit)? = null
    private var connectionListener: ((Boolean) -> Unit)? = null

    fun connect(serverIp: String, port: Int, userId: String) {
        disconnect()
        
        serverAddress = "ws://$serverIp:$port/"
        val client = OkHttpClient.Builder()
            .readTimeout(0, TimeUnit.MILLISECONDS)
            .pingInterval(30, TimeUnit.SECONDS)
            .build()

        val request = Request.Builder()
            .url(serverAddress)
            .addHeader("X-User-Id", userId)
            .build()

        webSocket = client.newWebSocket(request, object : WebSocketListener() {
            override fun onOpen(webSocket: WebSocket, response: Response) {
                Log.d("WebSocket", "Connected to $serverAddress")
                isConnected = true
                connectionListener?.invoke(true)
                
                // Отправляем авторизацию
                sendAuth(userId)
            }

            override fun onMessage(webSocket: WebSocket, text: String) {
                Log.d("WebSocket", "Received: $text")
                messageListener?.invoke(text)
            }

            override fun onClosing(webSocket: WebSocket, code: Int, reason: String) {
                Log.d("WebSocket", "Closing: $code / $reason")
                webSocket.close(1000, null)
            }

            override fun onFailure(webSocket: WebSocket, t: Throwable, response: Response?) {
                Log.e("WebSocket", "Error: ${t.message}")
                isConnected = false
                connectionListener?.invoke(false)
            }

            override fun onClosed(webSocket: WebSocket, code: Int, reason: String) {
                Log.d("WebSocket", "Closed: $code / $reason")
                isConnected = false
                connectionListener?.invoke(false)
            }
        })
    }

    fun disconnect() {
        webSocket?.close(1000, "User disconnected")
        webSocket = null
        isConnected = false
    }

    fun setOnMessageListener(listener: (String) -> Unit) {
        messageListener = listener
    }

    fun setOnConnectionListener(listener: (Boolean) -> Unit) {
        connectionListener = listener
    }

    private fun sendAuth(userId: String) {
        val user = UserManager.getInstance(context).getCurrentUser()
        val authRequest = mapOf(
            "action" to "auth",
            "phoneNumber" to (user?.phoneNumber ?: "")
        )
        send(gson.toJson(authRequest))
    }

    fun sendMessage(
        senderId: String,
        content: String,
        type: MessageType = MessageType.TEXT,
        recipientId: String? = null,
        groupId: String? = null,
        fileName: String? = null,
        fileSize: Long = 0
    ) {
        val message = mapOf(
            "action" to "send_message",
            "senderId" to senderId,
            "type" to type.name,
            "content" to content,
            "recipientId" to recipientId,
            "groupId" to groupId,
            "fileName" to fileName,
            "fileSize" to fileSize,
            "timestamp" to System.currentTimeMillis()
        )
        send(gson.toJson(message))
    }

    fun getContacts(userId: String) {
        val request = mapOf(
            "action" to "get_contacts",
            "userId" to userId
        )
        send(gson.toJson(request))
    }

    fun createGroup(name: String, creatorId: String, memberIds: List<String>) {
        val request = mapOf(
            "action" to "create_group",
            "name" to name,
            "creatorId" to creatorId,
            "memberIds" to memberIds
        )
        send(gson.toJson(request))
    }

    /**
     * Отправляет медиафайл (фото, видео, аудио, документ)
     * Сначала отправляется метаданные, затем файл в base64
     */
    fun sendMediaFile(
        senderId: String,
        file: java.io.File,
        type: MessageType,
        recipientId: String? = null,
        groupId: String? = null
    ) {
        if (!file.exists()) {
            Log.e("WebSocket", "File does not exist: ${file.absolutePath}")
            return
        }
        
        try {
            val fileName = file.name
            val fileSize = file.length()
            
            // Читаем файл в байты и кодируем в base64
            val fileBytes = file.readBytes()
            val base64Data = android.util.Base64.encodeToString(fileBytes, android.util.Base64.NO_WRAP)
            
            // Отправляем метаданные файла
            val metadataMessage = mapOf(
                "action" to "send_file_metadata",
                "senderId" to senderId,
                "type" to type.name,
                "fileName" to fileName,
                "fileSize" to fileSize,
                "recipientId" to recipientId,
                "groupId" to groupId,
                "timestamp" to System.currentTimeMillis()
            )
            send(gson.toJson(metadataMessage))
            
            // Отправляем файл частями (chunking для больших файлов)
            val chunkSize = 16384 // 16KB chunks
            var offset = 0
            var chunkIndex = 0
            
            while (offset < fileBytes.size) {
                val end = minOf(offset + chunkSize, fileBytes.size)
                val chunk = fileBytes.sliceArray(offset until end)
                val base64Chunk = android.util.Base64.encodeToString(chunk, android.util.Base64.NO_WRAP)
                
                val chunkMessage = mapOf(
                    "action" to "send_file_chunk",
                    "fileName" to fileName,
                    "chunkIndex" to chunkIndex,
                    "data" to base64Chunk,
                    "isLast" to (end >= fileBytes.size)
                )
                send(gson.toJson(chunkMessage))
                
                offset = end
                chunkIndex++
                
                // Небольшая задержка между чанками чтобы не перегружать сокет
                if (chunkSize < fileBytes.size) {
                    Thread.sleep(10)
                }
            }
            
            Log.d("WebSocket", "File sent: ${file.name} (${fileSize} bytes, $chunkIndex chunks)")
            
        } catch (e: Exception) {
            Log.e("WebSocket", "Error sending file: ${e.message}", e)
        }
    }

    private fun send(json: String) {
        if (isConnected && webSocket != null) {
            webSocket?.send(json)
        } else {
            Log.w("WebSocket", "Not connected, message not sent: $json")
        }
    }

    fun isConnecting(): Boolean = webSocket != null && !isConnected
    fun isConnected(): Boolean = isConnected
}

object UserManager {
    @Volatile private var instance: UserManager? = null
    private lateinit var context: Context
    private var currentUser: User? = null
    private val prefs by lazy { context.getSharedPreferences("user_prefs", Context.MODE_PRIVATE) }

    fun getInstance(context: Context): UserManager {
        Companion.context = context.applicationContext
        return instance ?: synchronized(this) {
            instance ?: UserManager().also { instance = it }
        }
    }

    fun saveUser(user: User) {
        currentUser = user
        prefs.edit().apply {
            putString("user_id", user.id)
            putString("phone_number", user.phoneNumber)
            putString("display_name", user.displayName)
            apply()
        }
    }

    fun getCurrentUser(): User? {
        if (currentUser != null) return currentUser
        
        val userId = prefs.getString("user_id", null) ?: return null
        currentUser = User(
            id = userId,
            phoneNumber = prefs.getString("phone_number", "")!!,
            displayName = prefs.getString("display_name", "")!!,
            avatarPath = null,
            lastSeen = System.currentTimeMillis(),
            isOnline = false
        )
        return currentUser
    }

    fun getUserId(): String? = getCurrentUser()?.id
    fun getPhoneNumber(): String? = getCurrentUser()?.phoneNumber

    fun clearUser() {
        currentUser = null
        prefs.edit().clear().apply()
    }
}
