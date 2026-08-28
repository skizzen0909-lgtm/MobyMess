package com.securelink.messenger.data.remote

import com.google.gson.annotations.SerializedName

/**
 * Типы сообщений протокола SecureLink
 */
enum class MessageType(val value: Int) {
    @SerializedName("auth") AUTH(1),
    @SerializedName("auth_response") AUTH_RESPONSE(2),
    @SerializedName("ping") PING(3),
    @SerializedName("pong") PONG(4),
    @SerializedName("text_message") TEXT_MESSAGE(10),
    @SerializedName("file_message") FILE_MESSAGE(11),
    @SerializedName("image_message") IMAGE_MESSAGE(12),
    @SerializedName("voice_message") VOICE_MESSAGE(13),
    @SerializedName("video_message") VIDEO_MESSAGE(14),
    @SerializedName("create_group") CREATE_GROUP(20),
    @SerializedName("add_to_group") ADD_TO_GROUP(21),
    @SerializedName("group_info") GROUP_INFO(22),
    @SerializedName("sync_contacts") SYNC_CONTACTS(30),
    @SerializedName("contacts_response") CONTACTS_RESPONSE(31),
    @SerializedName("error") ERROR(100);

    companion object {
        fun fromValue(value: Int): MessageType = entries.find { it.value == value } ?: TEXT_MESSAGE
    }
}

/**
 * Базовый пакет сообщения
 */
data class MessagePacket(
    @SerializedName("type") val type: Int,
    @SerializedName("messageId") val messageId: String = java.util.UUID.randomUUID().toString(),
    @SerializedName("senderId") val senderId: String? = null,
    @SerializedName("timestamp") val timestamp: Long = System.currentTimeMillis(),
    @SerializedName("payload") val payload: String? = null
)

/**
 * Запрос аутентификации
 */
data class AuthRequest(
    @SerializedName("phoneNumber") val phoneNumber: String,
    @SerializedName("deviceId") val deviceId: String,
    @SerializedName("verificationCode") val verificationCode: String? = null
)

/**
 * Ответ аутентификации
 */
data class AuthResponseData(
    @SerializedName("success") val success: Boolean,
    @SerializedName("userId") val userId: String?,
    @SerializedName("token") val token: String?,
    @SerializedName("errorMessage") val errorMessage: String?
)

/**
 * Данные текстового сообщения
 */
data class TextMessageData(
    @SerializedName("chatId") val chatId: String,
    @SerializedName("recipientId") val recipientId: String?,
    @SerializedName("text") val text: String
)

/**
 * Данные для создания группы
 */
data class CreateGroupData(
    @SerializedName("name") val name: String,
    @SerializedName("memberIds") val memberIds: List<String>? = null
)

/**
 * Модель контакта
 */
data class ContactData(
    @SerializedName("userId") val userId: String = "",
    @SerializedName("phoneNumber") val phoneNumber: String,
    @SerializedName("displayName") val displayName: String,
    @SerializedName("isRegistered") val isRegistered: Boolean = false
)

/**
 * Данные для синхронизации контактов
 */
data class ContactsSyncData(
    @SerializedName("contacts") val contacts: List<ContactData>
)

/**
 * Данные файла
 */
data class FileMetadata(
    @SerializedName("type") val type: Int,
    @SerializedName("fileName") val fileName: String,
    @SerializedName("size") val size: Int
)

/**
 * Ответ с информацией о файле
 */
data class FileResponseData(
    @SerializedName("filePath") val filePath: String,
    @SerializedName("fileName") val fileName: String,
    @SerializedName("size") val size: Int
)

/**
 * Данные группы
 */
data class GroupData(
    @SerializedName("groupId") val groupId: String,
    @SerializedName("name") val name: String,
    @SerializedName("creatorId") val creatorId: String?,
    @SerializedName("memberIds") val memberIds: List<String>,
    @SerializedName("action") val action: String? = null
)

/**
 * Ошибка сервера
 */
data class ErrorData(
    @SerializedName("error") val error: String
)
