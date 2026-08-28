package com.secutelink.messenger.ui.screens

import android.content.ContentValues
import android.content.Context
import android.provider.ContactsContract
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.secutelink.messenger.data.model.*
import com.secutelink.messenger.data.repository.AppRepository
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.launch

class HomeViewModel(private val repository: AppRepository) : ViewModel() {
    
    private val _chats = MutableStateFlow<List<ChatWithLastMessage>>(emptyList())
    val chats: StateFlow<List<ChatWithLastMessage>> = _chats.asStateFlow()
    
    private val _contacts = MutableStateFlow<List<Contact>>(emptyList())
    val contacts: StateFlow<List<Contact>> = _contacts.asStateFlow()
    
    private val _groups = MutableStateFlow<List<ChatGroup>>(emptyList())
    val groups: StateFlow<List<ChatGroup>> = _groups.asStateFlow()
    
    init {
        loadChats()
        loadGroups()
    }
    
    private fun loadChats() {
        viewModelScope.launch {
            // Здесь должна быть логика загрузки чатов с последними сообщениями
            // Пока заглушка
        }
    }
    
    private fun loadGroups() {
        viewModelScope.launch {
            repository.getAllGroups().collect { groups ->
                _groups.value = groups
            }
        }
    }
    
    fun loadContactsFromPhone(context: Context) {
        viewModelScope.launch {
            val phoneContacts = getPhoneContacts(context)
            
            // Получаем зарегистрированных пользователей
            val registeredUsers = repository.getAllUsers().first()
            val registeredPhones = registeredUsers.map { it.phoneNumber }.toSet()
            
            val contacts = phoneContacts.map { (phone, name) ->
                Contact(
                    phoneNumber = phone,
                    displayName = name,
                    isRegistered = registeredPhones.contains(phone),
                    userId = registeredUsers.find { it.phoneNumber == phone }?.id
                )
            }
            
            repository.deleteAllContacts()
            repository.insertContacts(contacts)
            
            _contacts.value = contacts
        }
    }
    
    private fun getPhoneContacts(context: Context): List<Pair<String, String>> {
        val contacts = mutableListOf<Pair<String, String>>()
        val cursor = context.contentResolver.query(
            ContactsContract.CommonDataKinds.Phone.CONTENT_URI,
            arrayOf(
                ContactsContract.CommonDataKinds.Phone.NUMBER,
                ContactsContract.CommonDataKinds.Phone.DISPLAY_NAME
            ),
            null,
            null,
            ContactsContract.CommonDataKinds.Phone.DISPLAY_NAME + " ASC"
        )
        
        cursor?.use {
            val phoneIndex = it.getColumnIndex(ContactsContract.CommonDataKinds.Phone.NUMBER)
            val nameIndex = it.getColumnIndex(ContactsContract.CommonDataKinds.Phone.DISPLAY_NAME)
            
            while (it.moveToNext()) {
                val phone = it.getString(phoneIndex)?.replace("\\s".toRegex(), "") ?: continue
                val name = it.getString(nameIndex) ?: "Неизвестно"
                contacts.add(phone to name)
            }
        }
        
        return contacts
    }
    
    fun createGroup(name: String, creatorId: String, memberIds: List<String>) {
        viewModelScope.launch {
            val group = ChatGroup(
                id = java.util.UUID.randomUUID().toString(),
                name = name,
                creatorId = creatorId,
                memberIds = memberIds.joinToString(","),
                createdAt = System.currentTimeMillis()
            )
            repository.insertGroup(group)
        }
    }
}

// Placeholder classes для UI компонентов
data class ChatItem(val id: String, val name: String, val lastMessage: String?, val time: Long, val isGroup: Boolean, val avatarPath: String? = null)

@androidx.compose.runtime.Composable
fun ChatListItem(chat: ChatItem, onClick: () -> Unit) {
    androidx.compose.material3.ListItem(
        headlineContent = { androidx.compose.material3.Text(chat.name) },
        supportingContent = { chat.lastMessage?.let { androidx.compose.material3.Text(it) } },
        trailingContent = { 
            androidx.compose.material3.Text(
                android.text.format.DateUtils.getRelativeTimeSpanString(chat.time).toString()
            ) 
        },
        modifier = androidx.compose.ui.Modifier.clickable(onClick = onClick)
    )
}

@androidx.compose.runtime.Composable
fun ContactListItem(contact: Contact, onClick: () -> Unit) {
    androidx.compose.material3.ListItem(
        headlineContent = { androidx.compose.material3.Text(contact.displayName) },
        supportingContent = { androidx.compose.material3.Text(contact.phoneNumber) },
        trailingContent = {
            if (contact.isRegistered) {
                androidx.compose.material3.Icon(
                    androidx.compose.material.icons.Icons.Default.CheckCircle,
                    contentDescription = "Зарегистрирован",
                    tint = androidx.compose.material3.MaterialTheme.colorScheme.primary
                )
            }
        },
        modifier = androidx.compose.ui.Modifier.clickable(onClick = onClick)
    )
}

@androidx.compose.runtime.Composable
fun GroupListItem(group: ChatGroup, onClick: () -> Unit) {
    androidx.compose.material3.ListItem(
        headlineContent = { androidx.compose.material3.Text(group.name) },
        supportingContent = { 
            val count = group.memberIds.split(",").size
            androidx.compose.material3.Text("$count участников") 
        },
        modifier = androidx.compose.ui.Modifier.clickable(onClick = onClick)
    )
}
