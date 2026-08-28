package com.secutelink.messenger.ui.screens

import android.Manifest
import android.content.Context
import android.content.pm.PackageManager
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.core.content.ContextCompat
import androidx.navigation.NavController
import com.google.accompanist.permissions.ExperimentalPermissionsApi
import com.google.accompanist.permissions.rememberMultipleStatesWithStatus
import com.secutelink.messenger.data.model.ChatGroup
import com.secutelink.messenger.data.model.Contact
import com.secutelink.messenger.network.WebSocketClient
import com.secutelink.messenger.network.UserManager

@OptIn(ExperimentalPermissionsApi::class)
@Composable
fun HomeScreen(
    navController: NavController,
    viewModel: HomeViewModel
) {
    val context = LocalContext.current
    var selectedTab by remember { mutableStateOf(0) }
    val tabs = listOf("Чаты", "Контакты", "Группы")
    
    val permissions = rememberMultipleStatesWithStatus(
        permissions = listOf(
            Manifest.permission.READ_CONTACTS,
            Manifest.permission.CAMERA,
            Manifest.permission.RECORD_AUDIO,
            Manifest.permission.READ_EXTERNAL_STORAGE,
            Manifest.permission.READ_MEDIA_IMAGES,
            Manifest.permission.POST_NOTIFICATIONS
        )
    )
    
    LaunchedEffect(Unit) {
        permissions.forEach { permissionState ->
            if (!permissionState.status.isGranted) {
                permissionState.launchPermissionRequest()
            }
        }
        
        // Загружаем контакты после получения разрешений
        if (permissions.all { it.status.isGranted }) {
            viewModel.loadContactsFromPhone(context)
        }
    }
    
    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("SecureLink") },
                actions = {
                    IconButton(onClick = { navController.navigate("settings") }) {
                        Icon(Icons.Default.Settings, contentDescription = "Настройки")
                    }
                }
            )
        },
        bottomBar = {
            NavigationBar {
                tabs.forEachIndexed { index, title ->
                    NavigationBarItem(
                        icon = when (index) {
                            0 -> Icons.Default.Chat
                            1 -> Icons.Default.Contacts
                            else -> Icons.Default.Groups
                        },
                        label = { Text(title) },
                        selected = selectedTab == index,
                        onClick = { selectedTab = index }
                    )
                }
            }
        }
    ) { paddingValues ->
        Box(modifier = Modifier.padding(paddingValues)) {
            when (selectedTab) {
                0 -> ChatsListScreen(navController, viewModel)
                1 -> ContactsScreen(navController, viewModel)
                2 -> GroupsScreen(navController, viewModel)
            }
        }
    }
}

@Composable
fun ChatsListScreen(navController: NavController, viewModel: HomeViewModel) {
    val chats by viewModel.chats.collectAsState(initial = emptyList())
    
    if (chats.isEmpty()) {
        Box(
            modifier = Modifier.fillMaxSize(),
            contentAlignment = Alignment.Center
        ) {
            Column(horizontalAlignment = Alignment.CenterHorizontally) {
                Icon(Icons.Default.ChatBubbleOutline, contentDescription = null, modifier = Modifier.size(64.dp), tint = MaterialTheme.colorScheme.outline)
                Spacer(modifier = Modifier.height(16.dp))
                Text("Нет чатов", style = MaterialTheme.typography.bodyLarge, color = MaterialTheme.colorScheme.outline)
            }
        }
    } else {
        LazyColumn {
            items(chats, key = { it.id }) { chat ->
                ChatListItem(
                    chat = chat,
                    onClick = { 
                        if (chat.isGroup) {
                            navController.navigate("chat_group/${chat.id}")
                        } else {
                            navController.navigate("chat/${chat.id}")
                        }
                    }
                )
            }
        }
    }
}

@Composable
fun ContactsScreen(navController: NavController, viewModel: HomeViewModel) {
    val context = LocalContext.current
    val contacts by viewModel.contacts.collectAsState(initial = emptyList())
    val user = UserManager.getInstance(context).getCurrentUser()
    
    if (contacts.isEmpty()) {
        Box(
            modifier = Modifier.fillMaxSize(),
            contentAlignment = Alignment.Center
        ) {
            Column(horizontalAlignment = Alignment.CenterHorizontally) {
                Icon(Icons.Default.PersonAdd, contentDescription = null, modifier = Modifier.size(64.dp), tint = MaterialTheme.colorScheme.outline)
                Spacer(modifier = Modifier.height(16.dp))
                Text("Нет контактов", style = MaterialTheme.typography.bodyLarge, color = MaterialTheme.colorScheme.outline)
            }
        }
    } else {
        LazyColumn {
            items(contacts, key = { it.phoneNumber }) { contact ->
                ContactListItem(
                    contact = contact,
                    onClick = {
                        if (contact.isRegistered && contact.userId != null) {
                            navController.navigate("chat/${contact.userId}")
                        }
                    }
                )
            }
            
            item {
                Spacer(modifier = Modifier.height(80.dp))
            }
        }
        
        FloatingActionButton(
            onClick = { /* Синхронизировать контакты */ },
            modifier = Modifier
                .align(Alignment.BottomEnd)
                .padding(16.dp)
        ) {
            Icon(Icons.Default.Refresh, contentDescription = "Обновить")
        }
    }
}

@Composable
fun GroupsScreen(navController: NavController, viewModel: HomeViewModel) {
    val context = LocalContext.current
    val groups by viewModel.groups.collectAsState(initial = emptyList())
    val user = UserManager.getInstance(context).getCurrentUser()
    
    if (groups.isEmpty()) {
        Box(
            modifier = Modifier.fillMaxSize(),
            contentAlignment = Alignment.Center
        ) {
            Column(horizontalAlignment = Alignment.CenterHorizontally) {
                Icon(Icons.Default.Groups, contentDescription = null, modifier = Modifier.size(64.dp), tint = MaterialTheme.colorScheme.outline)
                Spacer(modifier = Modifier.height(16.dp))
                Text("Нет групп", style = MaterialTheme.typography.bodyLarge, color = MaterialTheme.colorScheme.outline)
                Spacer(modifier = Modifier.height(8.dp))
                Button(onClick = { navController.navigate("create_group") }) {
                    Text("Создать группу")
                }
            }
        }
    } else {
        LazyColumn {
            items(groups, key = { it.id }) { group ->
                GroupListItem(
                    group = group,
                    onClick = { navController.navigate("chat_group/${group.id}") }
                )
            }
            
            item {
                Spacer(modifier = Modifier.height(80.dp))
            }
        }
        
        FloatingActionButton(
            onClick = { navController.navigate("create_group") },
            modifier = Modifier
                .align(Alignment.BottomEnd)
                .padding(16.dp)
        ) {
            Icon(Icons.Default.Add, contentDescription = "Создать группу")
        }
    }
}
