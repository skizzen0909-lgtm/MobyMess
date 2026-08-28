package com.secutelink.messenger

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.ui.Modifier
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.navigation.NavType
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import androidx.navigation.navArgument
import com.secutelink.messenger.data.repository.AppRepository
import com.secutelink.messenger.network.WebSocketClient
import com.secutelink.messenger.network.UserManager
import com.secutelink.messenger.ui.screens.*

class MainActivity : ComponentActivity() {
    private lateinit var repository: AppRepository
    private lateinit var wsClient: WebSocketClient
    
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        
        repository = AppRepository(this)
        wsClient = WebSocketClient(this)
        
        // Инициализация пользователя (регистрация по номеру телефона)
        val phoneNumber = getPhoneNumber() ?: registerUser()
        
        setContent {
            MaterialTheme {
                Surface(modifier = Modifier.fillMaxSize()) {
                    val navController = rememberNavController()
                    
                    NavHost(navController = navController, startDestination = "home") {
                        composable("home") {
                            val viewModel: HomeViewModel = viewModel(
                                factory = object : androidx.lifecycle.ViewModelProvider.Factory {
                                    override fun <T : androidx.lifecycle.ViewModel> create(modelClass: Class<T>): T {
                                        @Suppress("UNCHECKED_CAST")
                                        return HomeViewModel(repository) as T
                                    }
                                }
                            )
                            HomeScreen(navController = navController, viewModel = viewModel)
                        }
                        
                        composable(
                            route = "chat/{recipientId}",
                            arguments = listOf(navArgument("recipientId") { type = NavType.StringType })
                        ) { backStackEntry ->
                            val recipientId = backStackEntry.arguments?.getString("recipientId") ?: return@composable
                            val viewModel: ChatViewModel = viewModel(
                                factory = object : androidx.lifecycle.ViewModelProvider.Factory {
                                    override fun <T : androidx.lifecycle.ViewModel> create(modelClass: Class<T>): T {
                                        @Suppress("UNCHECKED_CAST")
                                        return ChatViewModel(this@MainActivity, wsClient) as T
                                    }
                                }
                            )
                            ChatScreen(
                                navController = navController,
                                recipientId = recipientId,
                                viewModel = viewModel
                            )
                        }
                        
                        composable(
                            route = "chat_group/{groupId}",
                            arguments = listOf(navArgument("groupId") { type = NavType.StringType })
                        ) { backStackEntry ->
                            val groupId = backStackEntry.arguments?.getString("groupId") ?: return@composable
                            // Аналогично чату, но для группы
                            ChatScreen(
                                navController = navController,
                                recipientId = groupId,
                                viewModel = viewModel(
                                    factory = object : androidx.lifecycle.ViewModelProvider.Factory {
                                        override fun <T : androidx.lifecycle.ViewModel> create(modelClass: Class<T>): T {
                                            @Suppress("UNCHECKED_CAST")
                                            return ChatViewModel(this@MainActivity, wsClient) as T
                                        }
                                    }
                                )
                            )
                        }
                        
                        composable("settings") {
                            SettingsScreen(navController = navController)
                        }
                        
                        composable("create_group") {
                            val viewModel: HomeViewModel = viewModel(
                                factory = object : androidx.lifecycle.ViewModelProvider.Factory {
                                    override fun <T : androidx.lifecycle.ViewModel> create(modelClass: Class<T>): T {
                                        @Suppress("UNCHECKED_CAST")
                                        return HomeViewModel(repository) as T
                                    }
                                }
                            )
                            CreateGroupScreen(navController = navController, viewModel = viewModel)
                        }
                    }
                }
            }
        }
    }
    
    private fun getPhoneNumber(): String? {
        return UserManager.getInstance(this).getPhoneNumber()
    }
    
    private fun registerUser(): String {
        // Простая регистрация - в реальном приложении нужно запросить у пользователя
        val phoneNumber = "+79990000000" // Заглушка
        val user = com.secutelink.messenger.data.model.User(
            id = java.util.UUID.randomUUID().toString(),
            phoneNumber = phoneNumber,
            displayName = "Пользователь $phoneNumber",
            avatarPath = null,
            lastSeen = System.currentTimeMillis(),
            isOnline = true
        )
        UserManager.getInstance(this).saveUser(user)
        
        // Сохраняем в БД
        androidx.lifecycle.lifecycleScope.launch {
            repository.insertUser(user)
        }
        
        return phoneNumber
    }
    
    override fun onDestroy() {
        super.onDestroy()
        wsClient.disconnect()
    }
}
