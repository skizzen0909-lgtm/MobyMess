package com.messenger.app.utils;

import android.content.Context;
import android.content.SharedPreferences;
import androidx.preference.PreferenceManager;

public class PreferencesManager {
    private static final String KEY_SERVER_IP = "server_ip";
    private static final String KEY_SERVER_PORT = "server_port";
    private static final String KEY_USER_ID = "user_id";
    private static final String KEY_USER_NAME = "user_name";
    private static final String KEY_DB_PATH = "db_path";
    private static final String KEY_MEDIA_PATH = "media_path";
    
    private SharedPreferences prefs;
    
    public PreferencesManager(Context context) {
        prefs = PreferenceManager.getDefaultSharedPreferences(context);
    }
    
    // Server IP
    public String getServerIp() {
        return prefs.getString(KEY_SERVER_IP, "192.168.1.100");
    }
    
    public void setServerIp(String ip) {
        prefs.edit().putString(KEY_SERVER_IP, ip).apply();
    }
    
    // Server Port
    public int getServerPort() {
        return prefs.getInt(KEY_SERVER_PORT, 8080);
    }
    
    public void setServerPort(int port) {
        prefs.edit().putInt(KEY_SERVER_PORT, port).apply();
    }
    
    // User ID
    public String getUserId() {
        return prefs.getString(KEY_USER_ID, "");
    }
    
    public void setUserId(String userId) {
        prefs.edit().putString(KEY_USER_ID, userId).apply();
    }
    
    // User Name
    public String getUserName() {
        return prefs.getString(KEY_USER_NAME, "");
    }
    
    public void setUserName(String userName) {
        prefs.edit().putString(KEY_USER_NAME, userName).apply();
    }
    
    // Database Path (for server settings sync)
    public String getDbPath() {
        return prefs.getString(KEY_DB_PATH, "");
    }
    
    public void setDbPath(String path) {
        prefs.edit().putString(KEY_DB_PATH, path).apply();
    }
    
    // Media Path (for server settings sync)
    public String getMediaPath() {
        return prefs.getString(KEY_MEDIA_PATH, "");
    }
    
    public void setMediaPath(String path) {
        prefs.edit().putString(KEY_MEDIA_PATH, path).apply();
    }
    
    // Get full server URL
    public String getServerUrl() {
        return "http://" + getServerIp() + ":" + getServerPort();
    }
}
