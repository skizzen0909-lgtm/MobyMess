package com.messenger.app.models;

import androidx.room.Entity;
import androidx.room.PrimaryKey;
import java.util.Date;

@Entity(tableName = "messages")
public class Message {
    @PrimaryKey(autoGenerate = true)
    private long id;
    private String chatId;
    private String senderId;
    private String senderName;
    private String content;
    private String messageType; // text, image, video, audio, file
    private String filePath;
    private long timestamp;
    private boolean isRead;
    private boolean isIncoming;

    public Message() {
        this.timestamp = System.currentTimeMillis();
        this.isRead = false;
    }

    public Message(String chatId, String senderId, String senderName, String content, 
                   String messageType, String filePath, boolean isIncoming) {
        this.chatId = chatId;
        this.senderId = senderId;
        this.senderName = senderName;
        this.content = content;
        this.messageType = messageType;
        this.filePath = filePath;
        this.timestamp = System.currentTimeMillis();
        this.isRead = false;
        this.isIncoming = isIncoming;
    }

    // Getters and Setters
    public long getId() { return id; }
    public void setId(long id) { this.id = id; }
    
    public String getChatId() { return chatId; }
    public void setChatId(String chatId) { this.chatId = chatId; }
    
    public String getSenderId() { return senderId; }
    public void setSenderId(String senderId) { this.senderId = senderId; }
    
    public String getSenderName() { return senderName; }
    public void setSenderName(String senderName) { this.senderName = senderName; }
    
    public String getContent() { return content; }
    public void setContent(String content) { this.content = content; }
    
    public String getMessageType() { return messageType; }
    public void setMessageType(String messageType) { this.messageType = messageType; }
    
    public String getFilePath() { return filePath; }
    public void setFilePath(String filePath) { this.filePath = filePath; }
    
    public long getTimestamp() { return timestamp; }
    public void setTimestamp(long timestamp) { this.timestamp = timestamp; }
    
    public boolean isRead() { return isRead; }
    public void setRead(boolean read) { isRead = read; }
    
    public boolean isIncoming() { return isIncoming; }
    public void setIncoming(boolean incoming) { isIncoming = incoming; }
}
