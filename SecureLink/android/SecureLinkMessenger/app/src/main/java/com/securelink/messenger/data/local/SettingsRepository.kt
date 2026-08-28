package com.securelink.messenger.data.local

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.*
import androidx.datastore.preferences.preferencesDataStore
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map

/**
 * Хранилище настроек приложения через DataStore
 */
val Context.settingsDataStore: DataStore<Preferences> by preferencesDataStore(name = "settings")

class SettingsRepository(private val context: Context) {
    
    companion object {
        val SERVER_IP = stringPreferencesKey("server_ip")
        val SERVER_PORT = intPreferencesKey("server_port")
        val USER_ID = stringPreferencesKey("user_id")
        val USER_TOKEN = stringPreferencesKey("user_token")
        val USER_PHONE = stringPreferencesKey("user_phone")
        val NOTIFICATIONS_ENABLED = booleanPreferencesKey("notifications_enabled")
        val USE_SSL = booleanPreferencesKey("use_ssl")
    }

    val serverIpFlow: Flow<String> = context.settingsDataStore.data.map { prefs ->
        prefs[SERVER_IP] ?: ""
    }

    val serverPortFlow: Flow<Int> = context.settingsDataStore.data.map { prefs ->
        prefs[SERVER_PORT] ?: 8080
    }

    val userIdFlow: Flow<String> = context.settingsDataStore.data.map { prefs ->
        prefs[USER_ID] ?: ""
    }

    val userTokenFlow: Flow<String> = context.settingsDataStore.data.map { prefs ->
        prefs[USER_TOKEN] ?: ""
    }

    suspend fun saveServerSettings(ip: String, port: Int) {
        context.settingsDataStore.edit { prefs ->
            prefs[SERVER_IP] = ip
            prefs[SERVER_PORT] = port
        }
    }

    suspend fun saveUserCredentials(userId: String, token: String, phone: String) {
        context.settingsDataStore.edit { prefs ->
            prefs[USER_ID] = userId
            prefs[USER_TOKEN] = token
            prefs[USER_PHONE] = phone
        }
    }

    suspend fun clearUserCredentials() {
        context.settingsDataStore.edit { prefs ->
            prefs.remove(USER_ID)
            prefs.remove(USER_TOKEN)
            prefs.remove(USER_PHONE)
        }
    }

    suspend fun setNotificationsEnabled(enabled: Boolean) {
        context.settingsDataStore.edit { prefs ->
            prefs[NOTIFICATIONS_ENABLED] = enabled
        }
    }

    suspend fun setUseSsl(useSsl: Boolean) {
        context.settingsDataStore.edit { prefs ->
            prefs[USE_SSL] = useSsl
        }
    }

    suspend fun getServerAddress(): Pair<String, Int> {
        return try {
            context.settingsDataStore.data.map { prefs ->
                val ip = prefs[SERVER_IP] ?: "192.168.1.100" // Default local IP
                val port = prefs[SERVER_PORT] ?: 8080
                Pair(ip, port)
            }.first()
        } catch (e: Exception) {
            Pair("192.168.1.100", 8080)
        }
    }
}
