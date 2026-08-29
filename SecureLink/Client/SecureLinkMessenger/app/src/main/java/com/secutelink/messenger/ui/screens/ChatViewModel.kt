package com.secutelink.messenger.ui.screens

import android.content.Context
import android.net.Uri
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.secutelink.messenger.data.model.Message
import com.secutelink.messenger.data.model.MessageType
import com.secutelink.messenger.media.MediaManager
import com.secutelink.messenger.media.MediaType
import com.secutelink.messenger.network.WebSocketClient
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import java.io.File

class ChatViewModel(
    private val context: Context,
    private val wsClient: WebSocketClient
) : ViewModel() {
    
    private val _messages = MutableStateFlow<List<Message>>(emptyList())
    val messages: StateFlow<List<Message>> = _messages.asStateFlow()
    
    private val mediaManager = MediaManager(context)
    private var selectedMediaUri: Uri? = null
    private var selectedMediaType: MediaType? = null
    
    private var currentRecipientId: String? = null
    private var currentSenderId: String? = null
    
    init {
        loadMessages()
    }
    
    fun setChatContext(senderId: String, recipientId: String) {
        currentSenderId = senderId
        currentRecipientId = recipientId
    }
    
    private fun loadMessages() {
        // Загрузка сообщений из БД
        // TODO: Реализовать загрузку из Room Database при наличии репозитория
    }
    
    fun sendMessage(
        content: String,
        type: MessageType,
        recipientId: String? = null,
        groupId: String? = null
    ) {
        val senderId = currentSenderId ?: return
        
        wsClient.sendMessage(
            senderId = senderId,
            content = content,
            type = type,
            recipientId = recipientId,
            groupId = groupId
        )
        
        // Добавляем сообщение локально
        val message = Message(
            id = System.currentTimeMillis().toString(),
            senderId = senderId,
            recipientId = recipientId,
            groupId = groupId,
            type = type,
            content = content,
            fileName = null,
            filePath = null,
            fileSize = 0,
            timestamp = System.currentTimeMillis(),
            isOutgoing = true,
            isRead = false
        )
        _messages.value = _messages.value + message
    }
    
    fun sendVoiceMessage(file: File, recipientId: String) {
        val senderId = currentSenderId ?: return
        val fileSize = mediaManager.getFileSize(file)
        
        wsClient.sendMediaFile(
            senderId = senderId,
            file = file,
            type = MessageType.AUDIO,
            recipientId = recipientId
        )
        
        val message = Message(
            id = System.currentTimeMillis().toString(),
            senderId = senderId,
            recipientId = recipientId,
            groupId = null,
            type = MessageType.AUDIO,
            content = "",
            fileName = file.name,
            filePath = file.absolutePath,
            fileSize = fileSize,
            timestamp = System.currentTimeMillis(),
            isOutgoing = true,
            isRead = false
        )
        _messages.value = _messages.value + message
    }
    
    fun onMediaSelected(uri: Uri, type: MediaType) {
        selectedMediaUri = uri
        selectedMediaType = type
    }
    
    fun clearSelectedMedia() {
        selectedMediaUri = null
        selectedMediaType = null
    }
    
    fun sendSelectedMedia(recipientId: String) {
        val senderId = currentSenderId ?: return
        val uri = selectedMediaUri ?: return
        val type = selectedMediaType ?: return
        
        val messageType = when (type) {
            MediaType.IMAGE -> MessageType.IMAGE
            MediaType.VIDEO -> MessageType.VIDEO
            MediaType.FILE -> MessageType.FILE
            MediaType.AUDIO -> MessageType.AUDIO
        }
        
        viewModelScope.launch {
            try {
                mediaManager.processAndSendFile(uri, messageType, recipientId, wsClient)
                
                // Добавляем сообщение локально после отправки
                val message = Message(
                    id = System.currentTimeMillis().toString(),
                    senderId = senderId,
                    recipientId = recipientId,
                    groupId = null,
                    type = messageType,
                    content = "",
                    fileName = mediaManager.getFileNameFromUri(uri),
                    filePath = null,
                    fileSize = mediaManager.getFileSizeFromUri(uri),
                    timestamp = System.currentTimeMillis(),
                    isOutgoing = true,
                    isRead = false
                )
                _messages.value = _messages.value + message
                
                clearSelectedMedia()
            } catch (e: Exception) {
                e.printStackTrace()
            }
        }
    }
    
    fun addIncomingMessage(message: Message) {
        _messages.value = _messages.value + message
    }
}
