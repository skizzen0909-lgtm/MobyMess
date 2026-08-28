package com.secutelink.messenger.data.model

import androidx.room.Entity
import androidx.room.PrimaryKey

enum class MessageType {
    TEXT, IMAGE, VIDEO, AUDIO, FILE, SYSTEM
}

@Entity(tableName = "messages")
data class Message(
    @PrimaryKey(autoGenerate = true) val id: Long = 0,
    val senderId: String,
    val recipientId: String?,
    val groupId: String?,
    val type: MessageType,
    val content: String,
    val fileName: String?,
    val fileSize: Long,
    val timestamp: Long,
    val isRead: Boolean = false,
    val isOutgoing: Boolean = false
)

@Entity(tableName = "users")
data class User(
    @PrimaryKey val id: String,
    val phoneNumber: String,
    val displayName: String,
    val avatarPath: String?,
    val lastSeen: Long,
    val isOnline: Boolean = false
)

@Entity(tableName = "contacts")
data class Contact(
    @PrimaryKey(autoGenerate = true) val id: Int = 0,
    val phoneNumber: String,
    val displayName: String,
    val isRegistered: Boolean = false,
    val userId: String? = null
)

@Entity(tableName = "chat_groups")
data class ChatGroup(
    @PrimaryKey val id: String,
    val name: String,
    val creatorId: String,
    val memberIds: String, // JSON массив
    val createdAt: Long
)

data class ChatWithLastMessage(
    val id: String,
    val name: String,
    val avatarPath: String?,
    val lastMessage: String?,
    val lastMessageTime: Long,
    val unreadCount: Int,
    val isGroup: Boolean
)
