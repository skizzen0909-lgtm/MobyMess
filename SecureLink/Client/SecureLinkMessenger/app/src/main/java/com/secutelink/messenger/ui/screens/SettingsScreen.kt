package com.secutelink.messenger.ui.screens

import android.content.Context
import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.Save
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.navigation.NavController
import com.secutelink.messenger.network.UserManager
import com.secutelink.messenger.network.WebSocketClient

@Composable
fun SettingsScreen(navController: NavController) {
    val context = LocalContext.current
    val prefs = context.getSharedPreferences("server_settings", Context.MODE_PRIVATE)
    
    var serverAddress by remember { 
        mutableStateOf(prefs.getString("server_address", "192.168.1.100") ?: "192.168.1.100") 
    }
    var serverPort by remember { 
        mutableStateOf(prefs.getString("server_port", "8080") ?: "8080") 
    }
    
    val user = UserManager.getInstance(context).getCurrentUser()
    
    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Настройки") },
                navigationIcon = {
                    IconButton(onClick = { navController.popBackStack() }) {
                        Icon(Icons.Default.ArrowBack, contentDescription = "Назад")
                    }
                }
            )
        }
    ) { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .padding(16.dp)
        ) {
            // Информация о пользователе
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors(
                    containerColor = MaterialTheme.colorScheme.primaryContainer
                )
            ) {
                Column(modifier = Modifier.padding(16.dp)) {
                    Text("Пользователь", style = MaterialTheme.typography.titleMedium)
                    Spacer(modifier = Modifier.height(8.dp))
                    user?.let {
                        Text("Телефон: ${it.phoneNumber}", style = MaterialTheme.typography.bodyMedium)
                        Text("ID: ${it.id}", style = MaterialTheme.typography.bodySmall)
                    } ?: run {
                        Text("Не авторизован", style = MaterialTheme.typography.bodyMedium)
                    }
                }
            }
            
            Spacer(modifier = Modifier.height(24.dp))
            
            // Настройки сервера
            Text("Настройки подключения", style = MaterialTheme.typography.titleLarge)
            Spacer(modifier = Modifier.height(16.dp))
            
            OutlinedTextField(
                value = serverAddress,
                onValueChange = { serverAddress = it },
                label = { Text("Адрес сервера") },
                placeholder = { Text("Например: 192.168.1.100") },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true
            )
            
            Spacer(modifier = Modifier.height(16.dp))
            
            OutlinedTextField(
                value = serverPort,
                onValueChange = { serverPort = it },
                label = { Text("Порт") },
                placeholder = { Text("8080") },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true
            )
            
            Spacer(modifier = Modifier.height(24.dp))
            
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(16.dp)
            ) {
                Button(
                    onClick = {
                        prefs.edit().apply {
                            putString("server_address", serverAddress)
                            putString("server_port", serverPort)
                            apply()
                        }
                    },
                    modifier = Modifier.weight(1f)
                ) {
                    Icon(Icons.Default.Save, contentDescription = null, modifier = Modifier.size(18.dp))
                    Spacer(modifier = Modifier.width(8.dp))
                    Text("Сохранить")
                }
            }
            
            Spacer(modifier = Modifier.height(16.dp))
            
            // Дополнительная информация
            Card(
                modifier = Modifier.fillMaxWidth()
            ) {
                Column(modifier = Modifier.padding(16.dp)) {
                    Text("О приложении", style = MaterialTheme.typography.titleMedium)
                    Spacer(modifier = Modifier.height(8.dp))
                    Text("SecureLink Messenger v1.0", style = MaterialTheme.typography.bodyMedium)
                    Text("Мессенджер с端到端 шифрованием", style = MaterialTheme.typography.bodySmall)
                    Text("Поддержка: текст, фото, видео, файлы, голосовые сообщения", style = MaterialTheme.typography.bodySmall)
                }
            }
        }
    }
}

@Composable
fun CreateGroupScreen(navController: NavController, viewModel: HomeViewModel) {
    val context = LocalContext.current
    var groupName by remember { mutableStateOf("") }
    val user = UserManager.getInstance(context).getCurrentUser()
    val contacts by viewModel.contacts.collectAsState(initial = emptyList())
    val selectedContacts = remember { mutableStateListOf<String>() }
    
    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Создать группу") },
                navigationIcon = {
                    IconButton(onClick = { navController.popBackStack() }) {
                        Icon(Icons.Default.ArrowBack, contentDescription = "Назад")
                    }
                },
                actions = {
                    IconButton(
                        enabled = groupName.isNotBlank() && selectedContacts.isNotEmpty() && user != null,
                        onClick = {
                            if (user != null) {
                                viewModel.createGroup(
                                    name = groupName,
                                    creatorId = user.id,
                                    memberIds = selectedContacts.toList()
                                )
                                navController.popBackStack()
                            }
                        }
                    ) {
                        Icon(Icons.Default.Save, contentDescription = "Создать")
                    }
                }
            )
        }
    ) { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .padding(16.dp)
        ) {
            OutlinedTextField(
                value = groupName,
                onValueChange = { groupName = it },
                label = { Text("Название группы") },
                placeholder = { Text("Введите название") },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true
            )
            
            Spacer(modifier = Modifier.height(16.dp))
            
            Text("Выберите контакты:", style = MaterialTheme.typography.titleMedium)
            
            Spacer(modifier = Modifier.height(8.dp))
            
            contacts.filter { it.isRegistered }.forEach { contact ->
                contact.userId?.let { userId ->
                    androidx.compose.material3.CheckboxItem(
                        text = contact.displayName,
                        checked = selectedContacts.contains(userId),
                        onCheckedChange = { checked ->
                            if (checked) {
                                selectedContacts.add(userId)
                            } else {
                                selectedContacts.remove(userId)
                            }
                        }
                    )
                }
            }
        }
    }
}

@androidx.compose.runtime.Composable
fun CheckboxItem(text: String, checked: Boolean, onCheckedChange: (Boolean) -> Unit) {
    Row(
        modifier = androidx.compose.ui.Modifier
            .fillMaxWidth()
            .clickable { onCheckedChange(!checked) },
        verticalAlignment = androidx.compose.ui.Alignment.CenterVertically
    ) {
        Checkbox(checked = checked, onCheckedChange = onCheckedChange)
        Spacer(modifier = Modifier.width(8.dp))
        Text(text)
    }
}
