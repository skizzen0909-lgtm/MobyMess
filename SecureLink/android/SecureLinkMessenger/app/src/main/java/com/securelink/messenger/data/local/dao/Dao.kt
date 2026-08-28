package com.securelink.messenger.data.local.dao

import androidx.room.*
import com.securelink.messenger.data.local.*
import kotlinx.coroutines.flow.Flow

/**
 * DAO для работы с пользователями
 */
@Dao
interface UserDao {
    @Query("SELECT * FROM users WHERE id = :userId")
    suspend fun getUserById(userId: String): UserEntity?

    @Query("SELECT * FROM users WHERE phoneNumber = :phoneNumber")
    suspend fun getUserByPhone(phoneNumber: String): UserEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertUser(user: UserEntity)

    @Update
    suspend fun updateUser(user: UserEntity)

    @Query("SELECT * FROM users WHERE isActive = 1")
    fun getAllUsersFlow(): Flow<List<UserEntity>>
}

/**
 * DAO для работы с контактами
 */
@Dao
interface ContactDao {
    @Query("SELECT * FROM contacts WHERE userId = :userId ORDER BY displayName")
    fun getContactsByUserId(userId: String): Flow<List<ContactEntity>>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertContact(contact: ContactEntity)

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertContacts(contacts: List<ContactEntity>)

    @Query("DELETE FROM contacts WHERE userId = :userId")
    suspend fun deleteAllContacts(userId: String)

    @Query("SELECT * FROM contacts WHERE userId = :userId AND isRegistered = 1")
    suspend fun getRegisteredContacts(userId: String): List<ContactEntity>
}

/**
 * DAO для работы с чатами
 */
@Dao
interface ChatDao {
    @Query("SELECT * FROM chats WHERE user1Id = :user1Id AND user2Id = :user2Id OR user1Id = :user2Id AND user2Id = :user1Id")
    suspend fun getChatBetweenUsers(user1Id: String, user2Id: String): ChatEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertChat(chat: ChatEntity)

    @Query("SELECT * FROM chats WHERE user1Id = :userId OR user2Id = :userId ORDER BY lastMessageAt DESC")
    fun getUserChatsFlow(userId: String): Flow<List<ChatEntity>>

    @Update
    suspend fun updateChat(chat: ChatEntity)
}

/**
 * DAO для работы с группами
 */
@Dao
interface GroupDao {
    @Query("SELECT * FROM groups WHERE id = :groupId")
    suspend fun getGroupById(groupId: String): GroupEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertGroup(group: GroupEntity)

    @Query("SELECT g.* FROM groups g INNER JOIN group_members gm ON g.id = gm.groupId WHERE gm.userId = :userId")
    fun getUserGroupsFlow(userId: String): Flow<List<GroupEntity>>

    @Insert(onConflict = OnConflictStrategy.IGNORE)
    suspend fun insertGroupMember(member: GroupMemberEntity)

    @Query("DELETE FROM group_members WHERE groupId = :groupId AND userId = :userId")
    suspend fun removeGroupMember(groupId: String, userId: String)

    @Query("SELECT * FROM group_members WHERE groupId = :groupId")
    suspend fun getGroupMembers(groupId: String): List<GroupMemberEntity>
}

/**
 * DAO для работы с сообщениями
 */
@Dao
interface MessageDao {
    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertMessage(message: MessageEntity)

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertMessages(messages: List<MessageEntity>)

    @Query("SELECT * FROM messages WHERE chatId = :chatId ORDER BY sentAt DESC LIMIT :count")
    fun getChatMessagesFlow(chatId: String, count: Int = 50): Flow<List<MessageEntity>>

    @Query("SELECT * FROM messages WHERE groupId = :groupId ORDER BY sentAt DESC LIMIT :count")
    fun getGroupMessagesFlow(groupId: String, count: Int = 50): Flow<List<MessageEntity>>

    @Query("UPDATE messages SET isDelivered = 1 WHERE id = :messageId")
    suspend fun markAsDelivered(messageId: String)

    @Query("UPDATE messages SET isRead = 1 WHERE id = :messageId")
    suspend fun markAsRead(messageId: String)

    @Query("DELETE FROM messages WHERE chatId = :chatId")
    suspend fun deleteChatMessages(chatId: String)
}

/**
 * Объединенная база данных
 */
@Database(
    entities = [
        UserEntity::class,
        ContactEntity::class,
        ChatEntity::class,
        GroupEntity::class,
        GroupMemberEntity::class,
        MessageEntity::class
    ],
    version = 1,
    exportSchema = false
)
@TypeConverters(Converters::class)
abstract class AppDatabase : RoomDatabase() {
    abstract fun userDao(): UserDao
    abstract fun contactDao(): ContactDao
    abstract fun chatDao(): ChatDao
    abstract fun groupDao(): GroupDao
    abstract fun messageDao(): MessageDao

    companion object {
        const val DATABASE_NAME = "securelink_db"
    }
}

/**
 * Конвертеры типов для Room
 */
class Converters {
    @TypeConverter
    fun fromMessageType(value: MessageTypeEntity): String = value.name

    @TypeConverter
    fun toMessageType(value: String): MessageTypeEntity = MessageTypeEntity.valueOf(value)
}
