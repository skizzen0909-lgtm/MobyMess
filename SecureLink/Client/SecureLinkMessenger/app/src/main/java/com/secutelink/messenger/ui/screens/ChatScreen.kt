package com.secutelink.messenger.ui.screens

import android.Manifest
import android.content.pm.PackageManager
import android.net.Uri
import android.widget.Toast
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.core.content.ContextCompat
import androidx.navigation.NavController
import coil.compose.AsyncImage
import com.secutelink.messenger.data.model.Message
import com.secutelink.messenger.data.model.MessageType
import com.secutelink.messenger.media.MediaManager
import com.secutelink.messenger.media.MediaType
import com.secutelink.messenger.media.player.AudioPlayer
import com.secutelink.messenger.media.recorder.VoiceRecorder
import com.secutelink.messenger.network.UserManager
import com.secutelink.messenger.network.WebSocketClient
import java.io.File

@Composable
fun ChatScreen(
    navController: NavController,
    recipientId: String,
    viewModel: ChatViewModel
) {
    val context = LocalContext.current
    val messages by viewModel.messages.collectAsState(initial = emptyList())
    var messageText by remember { mutableStateOf("") }
    val user = UserManager.getInstance(context).getCurrentUser()
    
    // Медиа компоненты
    val mediaManager = remember { MediaManager(context) }
    val voiceRecorder = remember { VoiceRecorder(context) }
    val audioPlayer = remember { AudioPlayer(context) }
    
    var isRecording by remember { mutableStateOf(false) }
    var recordingDuration by remember { mutableStateOf(0L) }
    var selectedMediaUri by remember { mutableStateOf<Uri?>(null) }
    var selectedMediaType by remember { mutableStateOf<MediaType?>(null) }
    
    // Таймер для обновления длительности записи
    LaunchedEffect(isRecording) {
        if (isRecording) {
            while (isRecording) {
                recordingDuration = voiceRecorder.getRecordingDuration()
                kotlinx.coroutines.delay(1000)
            }
        }
    }
    
    // Лаунчер для выбора изображений
    val imagePickerLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.GetContent()
    ) { uri: Uri? ->
        uri?.let {
            selectedMediaUri = it
            selectedMediaType = MediaType.IMAGE
            viewModel.onMediaSelected(it, MediaType.IMAGE)
        }
    }

    // Лаунчер для выбора видео
    val videoPickerLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.GetContent()
    ) { uri: Uri? ->
        uri?.let {
            selectedMediaUri = it
            selectedMediaType = MediaType.VIDEO
            viewModel.onMediaSelected(it, MediaType.VIDEO)
        }
    }

    // Лаунчер для выбора файлов
    val filePickerLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.GetContent()
    ) { uri: Uri? ->
        uri?.let {
            selectedMediaUri = it
            selectedMediaType = MediaType.FILE
            viewModel.onMediaSelected(it, MediaType.FILE)
        }
    }

    // Запрос разрешений для записи аудио
    val requestPermissionLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.RequestPermission()
    ) { isGranted ->
        if (isGranted) {
            isRecording = voiceRecorder.startRecording()
        } else {
            Toast.makeText(context, "Разрешение на запись аудио не предоставлено", Toast.LENGTH_SHORT).show()
        }
    }

    fun checkRecordPermissionAndStart() {
        when {
            ContextCompat.checkSelfPermission(context, Manifest.permission.RECORD_AUDIO) == PackageManager.PERMISSION_GRANTED -> {
                isRecording = voiceRecorder.startRecording()
            }
            else -> {
                requestPermissionLauncher.launch(Manifest.permission.RECORD_AUDIO)
            }
        }
    }
    
    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Чат") },
                navigationIcon = {
                    IconButton(onClick = { navController.popBackStack() }) {
                        Icon(Icons.Default.ArrowBack, contentDescription = "Назад")
                    }
                },
                actions = {
                    IconButton(onClick = { /* Открыть настройки чата */ }) {
                        Icon(Icons.Default.MoreVert, contentDescription = "Меню")
                    }
                }
            )
        }
    ) { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
        ) {
            LazyColumn(
                modifier = Modifier.weight(1f),
                reverseLayout = true,
                contentPadding = PaddingValues(16.dp)
            ) {
                items(messages.reversed(), key = { it.id }) { message ->
                    MessageItem(
                        message = message, 
                        isOutgoing = message.isOutgoing,
                        audioPlayer = audioPlayer,
                        context = context,
                        mediaManager = mediaManager
                    )
                }
            }
            
            // Индикатор предпросмотра медиа
            selectedMediaUri?.let { uri ->
                Card(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(16.dp),
                    shape = MaterialTheme.shapes.medium,
                    elevation = CardDefaults.cardElevation(defaultElevation = 4.dp)
                ) {
                    Column(
                        modifier = Modifier
                            .padding(12.dp)
                            .background(MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.3f))
                            .padding(12.dp)
                    ) {
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically
                        ) {
                            Row(verticalAlignment = Alignment.CenterVertically) {
                                Icon(
                                    when (selectedMediaType) {
                                        MediaType.IMAGE -> Icons.Default.Image
                                        MediaType.VIDEO -> Icons.Default.Videocam
                                        MediaType.FILE -> Icons.Default.AttachFile
                                        else -> Icons.Default.Attachment
                                    },
                                    contentDescription = null,
                                    tint = MaterialTheme.colorScheme.primary
                                )
                                Spacer(modifier = Modifier.width(8.dp))
                                Text(
                                    text = selectedMediaType?.name ?: "FILE",
                                    style = MaterialTheme.typography.titleMedium
                                )
                            }
                            IconButton(onClick = {
                                selectedMediaUri = null
                                selectedMediaType = null
                                viewModel.clearSelectedMedia()
                            }) {
                                Icon(Icons.Default.Close, contentDescription = "Удалить")
                            }
                        }
                        
                        // Предпросмотр изображения
                        if (selectedMediaType == MediaType.IMAGE) {
                            Spacer(modifier = Modifier.height(8.dp))
                            AsyncImage(
                                model = uri,
                                contentDescription = "Предпросмотр",
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .height(150.dp)
                                    .clip(MaterialTheme.shapes.small),
                                contentScale = ContentScale.Crop
                            )
                        }
                        
                        Spacer(modifier = Modifier.height(8.dp))
                        Button(
                            onClick = {
                                selectedMediaUri?.let {
                                    selectedMediaType?.let { type ->
                                        viewModel.sendSelectedMedia(it, type, user?.id, recipientId)
                                    }
                                }
                                selectedMediaUri = null
                                selectedMediaType = null
                            },
                            modifier = Modifier.align(Alignment.End)
                        ) {
                            Icon(Icons.Default.Send, contentDescription = "Отправить", modifier = Modifier.size(18.dp))
                            Spacer(modifier = Modifier.width(4.dp))
                            Text("Отправить")
                        }
                    }
                }
            }
            
            // Индикатор записи
            if (isRecording) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .background(MaterialTheme.colorScheme.errorContainer)
                        .padding(12.dp),
                    horizontalArrangement = Arrangement.Center,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Icon(
                        Icons.Default.Mic,
                        contentDescription = "Запись",
                        tint = MaterialTheme.colorScheme.onErrorContainer,
                        modifier = Modifier.size(24.dp)
                    )
                    Spacer(modifier = Modifier.width(8.dp))
                    Text(
                        text = voiceRecorder.formatDuration(recordingDuration),
                        style = MaterialTheme.typography.bodyLarge,
                        color = MaterialTheme.colorScheme.onErrorContainer
                    )
                    Spacer(modifier = Modifier.width(16.dp))
                    Button(
                        onClick = {
                            isRecording = false
                            val file = voiceRecorder.stopRecording()
                            if (file != null && user != null) {
                                viewModel.sendVoiceMessage(file, user.id, recipientId)
                            }
                        },
                        colors = ButtonDefaults.buttonColors(
                            containerColor = MaterialTheme.colorScheme.primary
                        )
                    ) {
                        Icon(Icons.Default.Send, contentDescription = "Отправить", modifier = Modifier.size(18.dp))
                        Spacer(modifier = Modifier.width(4.dp))
                        Text("Отправить")
                    }
                    Spacer(modifier = Modifier.width(8.dp))
                    OutlinedButton(
                        onClick = {
                            isRecording = false
                            voiceRecorder.cancelRecording()
                        }
                    ) {
                        Icon(Icons.Default.Close, contentDescription = "Отмена", modifier = Modifier.size(18.dp))
                    }
                }
            }
            
            // Панель ввода
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(8.dp),
                verticalAlignment = Alignment.Bottom
            ) {
                // Кнопка прикрепления файлов
                IconButton(onClick = { 
                    imagePickerLauncher.launch("image/*")
                }) {
                    Icon(Icons.Default.Image, contentDescription = "Фото", tint = MaterialTheme.colorScheme.primary)
                }
                
                // Кнопка видео
                IconButton(onClick = { 
                    videoPickerLauncher.launch("video/*")
                }) {
                    Icon(Icons.Default.Videocam, contentDescription = "Видео", tint = MaterialTheme.colorScheme.primary)
                }
                
                // Кнопка файла
                IconButton(onClick = { 
                    filePickerLauncher.launch("*/*")
                }) {
                    Icon(Icons.Default.AttachFile, contentDescription = "Файл", tint = MaterialTheme.colorScheme.primary)
                }
                
                Spacer(modifier = Modifier.width(4.dp))
                
                OutlinedTextField(
                    value = messageText,
                    onValueChange = { messageText = it },
                    modifier = Modifier
                        .weight(1f)
                        .imePadding(),
                    placeholder = { Text("Введите сообщение") },
                    maxLines = 4
                )
                
                Spacer(modifier = Modifier.width(8.dp))
                
                // Кнопка отправки текста или начала записи
                if (messageText.isNotBlank()) {
                    FloatingActionButton(
                        onClick = {
                            if (user != null) {
                                viewModel.sendMessage(
                                    senderId = user.id,
                                    content = messageText,
                                    type = MessageType.TEXT,
                                    recipientId = recipientId
                                )
                                messageText = ""
                            }
                        },
                        modifier = Modifier.align(Alignment.Bottom)
                    ) {
                        Icon(Icons.Default.Send, contentDescription = "Отправить")
                    }
                } else {
                    FloatingActionButton(
                        onClick = {
                            if (!isRecording) {
                                checkRecordPermissionAndStart()
                            }
                        },
                        modifier = Modifier.align(Alignment.Bottom),
                        containerColor = if (isRecording) MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.primary
                    ) {
                        Icon(if (isRecording) Icons.Default.Stop else Icons.Default.Mic, contentDescription = "Голосовое")
                    }
                }
            }
        }
    }
}

@Composable
fun MessageItem(
    message: Message, 
    isOutgoing: Boolean,
    audioPlayer: AudioPlayer,
    context: Context,
    mediaManager: MediaManager
) {
    var isPlaying by remember { mutableStateOf(false) }
    var currentPosition by remember { mutableStateOf(0) }
    var duration by remember { mutableStateOf(0) }
    
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 4.dp),
        colors = CardDefaults.cardColors(
            containerColor = if (isOutgoing) 
                MaterialTheme.colorScheme.primaryContainer 
            else 
                MaterialTheme.colorScheme.surfaceVariant
        ),
        shape = MaterialTheme.shapes.medium
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(12.dp)
        ) {
            when (message.type) {
                MessageType.TEXT -> {
                    Text(
                        text = message.content,
                        style = MaterialTheme.typography.bodyLarge,
                        color = if (isOutgoing) 
                            MaterialTheme.colorScheme.onPrimaryContainer 
                        else 
                            MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
                MessageType.IMAGE -> {
                    val imageFile = message.filePath?.let { File(it) }
                    if (imageFile != null && imageFile.exists()) {
                        AsyncImage(
                            model = imageFile,
                            contentDescription = "Изображение",
                            modifier = Modifier
                                .fillMaxWidth()
                                .height(200.dp)
                                .clip(MaterialTheme.shapes.medium),
                            contentScale = ContentScale.Crop
                        )
                    } else {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Icon(Icons.Default.Image, contentDescription = null)
                            Spacer(modifier = Modifier.width(8.dp))
                            Text("📷 Изображение", style = MaterialTheme.typography.bodyLarge)
                        }
                    }
                    message.fileName?.let { 
                        Spacer(modifier = Modifier.height(4.dp))
                        Text(it, style = MaterialTheme.typography.bodySmall) 
                    }
                }
                MessageType.VIDEO -> {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(Icons.Default.Videocam, contentDescription = null)
                        Spacer(modifier = Modifier.width(8.dp))
                        Text("🎥 Видео", style = MaterialTheme.typography.bodyLarge)
                    }
                    message.fileName?.let { 
                        Spacer(modifier = Modifier.height(4.dp))
                        Text(it, style = MaterialTheme.typography.bodySmall) 
                    }
                }
                MessageType.AUDIO -> {
                    val audioFile = message.filePath?.let { File(it) }
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            IconButton(
                                onClick = {
                                    if (audioFile != null) {
                                        if (isPlaying) {
                                            audioPlayer.pause()
                                            isPlaying = false
                                        } else {
                                            audioPlayer.play(audioFile, 
                                                onCompletion = { isPlaying = false },
                                                onProgress = { dur, pos ->
                                                    duration = dur
                                                    currentPosition = pos
                                                }
                                            )
                                            isPlaying = true
                                        }
                                    }
                                }
                            ) {
                                Icon(
                                    if (isPlaying) Icons.Default.Pause else Icons.Default.Play,
                                    contentDescription = if (isPlaying) "Пауза" else "Воспроизвести"
                                )
                            }
                            Column {
                                Text("🎤 Голосовое сообщение", style = MaterialTheme.typography.bodyLarge)
                                Text(
                                    text = audioPlayer.formatTime(currentPosition) + " / " + 
                                           audioPlayer.formatTime(duration),
                                    style = MaterialTheme.typography.bodySmall
                                )
                            }
                        }
                    }
                }
                MessageType.FILE -> {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(Icons.Default.AttachFile, contentDescription = null)
                        Spacer(modifier = Modifier.width(8.dp))
                        Text("📁 Файл", style = MaterialTheme.typography.bodyLarge)
                    }
                    message.fileName?.let { 
                        Spacer(modifier = Modifier.height(4.dp))
                        Text(it, style = MaterialTheme.typography.bodySmall) 
                    }
                }
                MessageType.SYSTEM -> {
                    Text(
                        text = message.content,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.outline,
                        modifier = Modifier.fillMaxWidth(),
                        textAlign = TextAlign.Center
                    )
                }
            }
            
            Spacer(modifier = Modifier.height(4.dp))
            
            Text(
                text = android.text.format.DateFormat.format("HH:mm", message.timestamp).toString(),
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.outline,
                modifier = Modifier.align(Alignment.End)
            )
        }
    }
}

class ChatViewModel(private val context: Context, private val wsClient: WebSocketClient) : androidx.lifecycle.ViewModel() {
    private val _messages = MutableStateFlow<List<Message>>(emptyList())
    val messages: StateFlow<List<Message>> = _messages.asStateFlow()
    
    private val mediaManager = MediaManager(context)
    private var selectedMediaUri: Uri? = null
    private var selectedMediaType: MediaType? = null
    
    init {
        loadMessages()
    }
    
    private fun loadMessages() {
        // Загрузка сообщений из БД
        // TODO: Реализовать загрузку из Room Database
    }
    
    fun sendMessage(
        senderId: String,
        content: String,
        type: MessageType,
        recipientId: String? = null,
        groupId: String? = null
    ) {
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
    
    fun sendVoiceMessage(file: java.io.File, senderId: String, recipientId: String) {
        val fileSize = mediaManager.getFileSize(file)
        val uri = mediaManager.getUriForFile(file)
        
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
    
    fun sendSelectedMedia(uri: Uri, type: MediaType, senderId: String?, recipientId: String) {
        if (senderId == null) return
        
        val messageType = when (type) {
            MediaType.IMAGE -> MessageType.IMAGE
            MediaType.VIDEO -> MessageType.VIDEO
            MediaType.FILE -> MessageType.FILE
            MediaType.AUDIO -> MessageType.AUDIO
        }
        
        kotlinx.coroutines.GlobalScope.launch {
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
                
                selectedMediaUri = null
                selectedMediaType = null
            } catch (e: Exception) {
                e.printStackTrace()
            }
        }
    }
}
