package com.messenger.app.network;

import android.content.Context;
import android.util.Log;

import com.google.gson.Gson;
import com.google.gson.GsonBuilder;
import com.messenger.app.models.Message;
import com.messenger.app.utils.PreferencesManager;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.IOException;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.TimeUnit;

import okhttp3.Call;
import okhttp3.Callback;
import okhttp3.MediaType;
import okhttp3.OkHttpClient;
import okhttp3.Request;
import okhttp3.RequestBody;
import okhttp3.Response;

public class ApiClient {
    private static final String TAG = "ApiClient";
    private static final MediaType JSON = MediaType.parse("application/json; charset=utf-8");
    
    private OkHttpClient client;
    private Gson gson;
    private PreferencesManager prefsManager;
    private Context context;
    
    public ApiClient(Context context) {
        this.context = context;
        this.prefsManager = new PreferencesManager(context);
        
        this.client = new OkHttpClient.Builder()
                .connectTimeout(30, TimeUnit.SECONDS)
                .readTimeout(30, TimeUnit.SECONDS)
                .writeTimeout(30, TimeUnit.SECONDS)
                .build();
        
        this.gson = new GsonBuilder().create();
    }
    
    public interface ApiCallback<T> {
        void onSuccess(T response);
        void onError(String error);
    }
    
    // Регистрация пользователя
    public void registerUser(String phoneNumber, String name, ApiCallback<String> callback) {
        try {
            JSONObject json = new JSONObject();
            json.put("action", "register");
            json.put("phone", phoneNumber);
            json.put("name", name);
            
            sendRequest(json.toString(), new ApiCallback<String>() {
                @Override
                public void onSuccess(String response) {
                    try {
                        JSONObject jsonResponse = new JSONObject(response);
                        if (jsonResponse.getBoolean("success")) {
                            String userId = jsonResponse.getString("userId");
                            prefsManager.setUserId(userId);
                            prefsManager.setUserName(name);
                            callback.onSuccess(userId);
                        } else {
                            callback.onError(jsonResponse.optString("error", "Registration failed"));
                        }
                    } catch (Exception e) {
                        callback.onError(e.getMessage());
                    }
                }
                
                @Override
                public void onError(String error) {
                    callback.onError(error);
                }
            });
        } catch (Exception e) {
            callback.onError(e.getMessage());
        }
    }
    
    // Отправка сообщения
    public void sendMessage(Message message, ApiCallback<Boolean> callback) {
        try {
            JSONObject json = new JSONObject();
            json.put("action", "send_message");
            json.put("userId", prefsManager.getUserId());
            json.put("chatId", message.getChatId());
            json.put("content", message.getContent());
            json.put("messageType", message.getMessageType());
            json.put("timestamp", message.getTimestamp());
            
            if (message.getFilePath() != null) {
                json.put("filePath", message.getFilePath());
            }
            
            sendRequest(json.toString(), new ApiCallback<String>() {
                @Override
                public void onSuccess(String response) {
                    try {
                        JSONObject jsonResponse = new JSONObject(response);
                        callback.onSuccess(jsonResponse.getBoolean("success"));
                    } catch (Exception e) {
                        callback.onError(e.getMessage());
                    }
                }
                
                @Override
                public void onError(String error) {
                    callback.onError(error);
                }
            });
        } catch (Exception e) {
            callback.onError(e.getMessage());
        }
    }
    
    // Получение сообщений
    public void getMessages(String chatId, ApiCallback<List<Message>> callback) {
        try {
            JSONObject json = new JSONObject();
            json.put("action", "get_messages");
            json.put("userId", prefsManager.getUserId());
            json.put("chatId", chatId);
            
            sendRequest(json.toString(), new ApiCallback<String>() {
                @Override
                public void onSuccess(String response) {
                    try {
                        JSONObject jsonResponse = new JSONObject(response);
                        JSONArray messagesArray = jsonResponse.getJSONArray("messages");
                        List<Message> messages = new ArrayList<>();
                        
                        for (int i = 0; i < messagesArray.length(); i++) {
                            JSONObject msgObj = messagesArray.getJSONObject(i);
                            Message msg = new Message();
                            msg.setChatId(msgObj.optString("chatId", ""));
                            msg.setSenderId(msgObj.optString("senderId", ""));
                            msg.setSenderName(msgObj.optString("senderName", ""));
                            msg.setContent(msgObj.optString("content", ""));
                            msg.setMessageType(msgObj.optString("messageType", "text"));
                            msg.setFilePath(msgObj.optString("filePath", null));
                            msg.setTimestamp(msgObj.optLong("timestamp", System.currentTimeMillis()));
                            msg.setIncoming(!msgObj.optString("senderId", "").equals(prefsManager.getUserId()));
                            
                            messages.add(msg);
                        }
                        
                        callback.onSuccess(messages);
                    } catch (Exception e) {
                        callback.onError(e.getMessage());
                    }
                }
                
                @Override
                public void onError(String error) {
                    callback.onError(error);
                }
            });
        } catch (Exception e) {
            callback.onError(e.getMessage());
        }
    }
    
    // Создание группы
    public void createGroup(String groupName, List<String> participants, ApiCallback<String> callback) {
        try {
            JSONObject json = new JSONObject();
            json.put("action", "create_group");
            json.put("userId", prefsManager.getUserId());
            json.put("groupName", groupName);
            
            JSONArray participantsArray = new JSONArray();
            for (String participant : participants) {
                participantsArray.put(participant);
            }
            json.put("participants", participantsArray);
            
            sendRequest(json.toString(), new ApiCallback<String>() {
                @Override
                public void onSuccess(String response) {
                    try {
                        JSONObject jsonResponse = new JSONObject(response);
                        if (jsonResponse.getBoolean("success")) {
                            callback.onSuccess(jsonResponse.getString("groupId"));
                        } else {
                            callback.onError(jsonResponse.optString("error", "Failed to create group"));
                        }
                    } catch (Exception e) {
                        callback.onError(e.getMessage());
                    }
                }
                
                @Override
                public void onError(String error) {
                    callback.onError(error);
                }
            });
        } catch (Exception e) {
            callback.onError(e.getMessage());
        }
    }
    
    // Получение списка чатов
    public void getChats(ApiCallback<List<JSONObject>> callback) {
        try {
            JSONObject json = new JSONObject();
            json.put("action", "get_chats");
            json.put("userId", prefsManager.getUserId());
            
            sendRequest(json.toString(), new ApiCallback<String>() {
                @Override
                public void onSuccess(String response) {
                    try {
                        JSONObject jsonResponse = new JSONObject(response);
                        JSONArray chatsArray = jsonResponse.getJSONArray("chats");
                        List<JSONObject> chats = new ArrayList<>();
                        
                        for (int i = 0; i < chatsArray.length(); i++) {
                            chats.add(chatsArray.getJSONObject(i));
                        }
                        
                        callback.onSuccess(chats);
                    } catch (Exception e) {
                        callback.onError(e.getMessage());
                    }
                }
                
                @Override
                public void onError(String error) {
                    callback.onError(error);
                }
            });
        } catch (Exception e) {
            callback.onError(e.getMessage());
        }
    }
    
    private void sendRequest(String jsonBody, ApiCallback<String> callback) {
        String url = prefsManager.getServerUrl() + "/api";
        
        RequestBody body = RequestBody.create(jsonBody, JSON);
        Request request = new Request.Builder()
                .url(url)
                .post(body)
                .build();
        
        client.newCall(request).enqueue(new Callback() {
            @Override
            public void onFailure(Call call, IOException e) {
                Log.e(TAG, "Request failed: " + e.getMessage());
                callback.onError("Connection error: " + e.getMessage());
            }
            
            @Override
            public void onResponse(Call call, Response response) throws IOException {
                if (response.isSuccessful()) {
                    String responseData = response.body().string();
                    callback.onSuccess(responseData);
                } else {
                    callback.onError("Server error: " + response.code());
                }
            }
        });
    }
}
