package com.secutelink.messenger.data.database

import androidx.room.*
import com.secutelink.messenger.data.model.*
import kotlinx.coroutines.flow.Flow

@Dao
interface MessageDao {
    @Query("SELECT * FROM messages WHERE (recipientId = :userId OR senderId = :userId) AND (groupId IS NULL) ORDER BY timestamp DESC")
    fun getPersonalMessages(userId: String): Flow<List<Message>>

    @Query("SELECT * FROM messages WHERE groupId = :groupId ORDER BY timestamp DESC")
    fun getGroupMessages(groupId: String): Flow<List<Message>>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertMessage(message: Message)

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertMessages(messages: List<Message>)

    @Query("UPDATE messages SET isRead = 1 WHERE recipientId = :userId AND isRead = 0")
    suspend fun markAllAsRead(userId: String)

    @Query("DELETE FROM messages WHERE id = :messageId")
    suspend fun deleteMessage(messageId: Long)
}

@Dao
interface UserDao {
    @Query("SELECT * FROM users WHERE id = :userId")
    fun getUserById(userId: String): Flow<User?>

    @Query("SELECT * FROM users ORDER BY displayName")
    fun getAllUsers(): Flow<List<User>>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertUser(user: User)

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertUsers(users: List<User>)

    @Query("UPDATE users SET isOnline = :isOnline, lastSeen = :lastSeen WHERE id = :userId")
    suspend fun updateUserStatus(userId: String, isOnline: Boolean, lastSeen: Long)
}

@Dao
interface ContactDao {
    @Query("SELECT * FROM contacts ORDER BY displayName")
    fun getAllContacts(): Flow<List<Contact>>

    @Query("SELECT * FROM contacts WHERE isRegistered = 1 ORDER BY displayName")
    fun getRegisteredContacts(): Flow<List<Contact>>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertContact(contact: Contact)

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertContacts(contacts: List<Contact>)

    @Query("DELETE FROM contacts")
    suspend fun deleteAllContacts()
}

@Dao
interface ChatGroupDao {
    @Query("SELECT * FROM chat_groups ORDER BY createdAt DESC")
    fun getAllGroups(): Flow<List<ChatGroup>>

    @Query("SELECT * FROM chat_groups WHERE id = :groupId")
    fun getGroupById(groupId: String): Flow<ChatGroup?>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertGroup(group: ChatGroup)

    @Delete
    suspend fun deleteGroup(group: ChatGroup)
}

@Database(
    entities = [Message::class, User::class, Contact::class, ChatGroup::class],
    version = 1,
    exportSchema = false
)
@TypeConverters(Converters::class)
abstract class AppDatabase : RoomDatabase() {
    abstract fun messageDao(): MessageDao
    abstract fun userDao(): UserDao
    abstract fun contactDao(): ContactDao
    abstract fun chatGroupDao(): ChatGroupDao

    companion object {
        const val DATABASE_NAME = "securelink_db"
    }
}

class Converters {
    @TypeConverter
    fun fromMessageType(value: MessageType): String = value.name

    @TypeConverter
    fun toMessageType(value: String): MessageType = MessageType.valueOf(value)
}
