package com.securelink.messenger.data.local

import androidx.room.Entity
import androidx.room.PrimaryKey

/**
 * Модель пользователя в локальной базе данных
 */
@Entity(tableName = "users")
data class UserEntity(
    @PrimaryKey val id: String,
    val phoneNumber: String,
    val displayName: String?,
    val avatarPath: String?,
    val registeredAt: Long,
    val isActive: Boolean = true
)

/**
 * Модель контакта
 */
@Entity(tableName = "contacts")
data class ContactEntity(
    val userId: String,
    val phoneNumber: String,
    val displayName: String,
    val isRegistered: Boolean = false,
    val syncedAt: Long = System.currentTimeMillis()
) {
    @PrimaryKey(autoGenerate = false)
    fun getCompositeKey(): String = "$userId-$phoneNumber"
}

/**
 * Модель чата
 */
@Entity(tableName = "chats")
data class ChatEntity(
    @PrimaryKey val id: String,
    val user1Id: String,
    val user2Id: String,
    val createdAt: Long = System.currentTimeMillis(),
    val lastMessageAt: Long? = null
)

/**
 * Модель группы
 */
@Entity(tableName = "groups")
data class GroupEntity(
    @PrimaryKey val id: String,
    val name: String,
    val creatorId: String,
    val createdAt: Long = System.currentTimeMillis(),
    val avatarPath: String? = null
)

/**
 * Связь участников группы
 */
@Entity(tableName = "group_members", primaryKeys = ["groupId", "userId"])
data class GroupMemberEntity(
    val groupId: String,
    val userId: String,
    val joinedAt: Long = System.currentTimeMillis()
)

/**
 * Типы сообщений
 */
enum class MessageTypeEntity {
    TEXT,
    IMAGE,
    VIDEO,
    VOICE,
    FILE
}

/**
 * Модель сообщения
 */
@Entity(tableName = "messages")
data class MessageEntity(
    @PrimaryKey val id: String,
    val chatId: String?,
    val groupId: String?,
    val senderId: String,
    val type: MessageTypeEntity,
    val content: String,
    val fileName: String? = null,
    val fileSize: Long? = null,
    val mimeType: String? = null,
    val sentAt: Long = System.currentTimeMillis(),
    val isDelivered: Boolean = false,
    val isRead: Boolean = false
)
