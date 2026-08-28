package com.secutelink.messenger.data.repository

import android.content.Context
import com.secutelink.messenger.data.database.AppDatabase
import com.secutelink.messenger.data.model.*
import kotlinx.coroutines.flow.Flow

class AppRepository(context: Context) {
    private val database = AppDatabase.builder(
        context.applicationContext,
        AppDatabase.DATABASE_NAME
    ).build()

    private val messageDao = database.messageDao()
    private val userDao = database.userDao()
    private val contactDao = database.contactDao()
    private val chatGroupDao = database.chatGroupDao()

    // Messages
    fun getPersonalMessages(userId: String): Flow<List<Message>> = 
        messageDao.getPersonalMessages(userId)
    
    fun getGroupMessages(groupId: String): Flow<List<Message>> = 
        messageDao.getGroupMessages(groupId)
    
    suspend fun insertMessage(message: Message) = 
        messageDao.insertMessage(message)
    
    suspend fun markAllAsRead(userId: String) = 
        messageDao.markAllAsRead(userId)

    // Users
    fun getUserById(userId: String): Flow<User?> = 
        userDao.getUserById(userId)
    
    fun getAllUsers(): Flow<List<User>> = 
        userDao.getAllUsers()
    
    suspend fun insertUser(user: User) = 
        userDao.insertUser(user)
    
    suspend fun insertUsers(users: List<User>) = 
        userDao.insertUsers(users)

    // Contacts
    fun getAllContacts(): Flow<List<Contact>> = 
        contactDao.getAllContacts()
    
    fun getRegisteredContacts(): Flow<List<Contact>> = 
        contactDao.getRegisteredContacts()
    
    suspend fun insertContact(contact: Contact) = 
        contactDao.insertContact(contact)
    
    suspend fun insertContacts(contacts: List<Contact>) = 
        contactDao.insertContacts(contacts)
    
    suspend fun deleteAllContacts() = 
        contactDao.deleteAllContacts()

    // Groups
    fun getAllGroups(): Flow<List<ChatGroup>> = 
        chatGroupDao.getAllGroups()
    
    fun getGroupById(groupId: String): Flow<ChatGroup?> = 
        chatGroupDao.getGroupById(groupId)
    
    suspend fun insertGroup(group: ChatGroup) = 
        chatGroupDao.insertGroup(group)
    
    suspend fun deleteGroup(group: ChatGroup) = 
        chatGroupDao.deleteGroup(group)
}
