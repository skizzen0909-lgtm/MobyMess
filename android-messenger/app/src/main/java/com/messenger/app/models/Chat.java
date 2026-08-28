package com.messenger.app.models;

import androidx.room.Entity;
import androidx.room.PrimaryKey;
import java.util.List;

@Entity(tableName = "chats")
public class Chat {
    @PrimaryKey
    private String id;
    private String name;
    private String lastMessage;
    private long lastMessageTime;
    private int unreadCount;
    private boolean isGroup;
    private List<String> participants;
    private String avatarPath;

    public Chat() {
        this.lastMessageTime = System.currentTimeMillis();
        this.unreadCount = 0;
    }

    public Chat(String id, String name, boolean isGroup) {
        this.id = id;
        this.name = name;
        this.isGroup = isGroup;
        this.lastMessageTime = System.currentTimeMillis();
        this.unreadCount = 0;
    }

    // Getters and Setters
    public String getId() { return id; }
    public void setId(String id) { this.id = id; }
    
    public String getName() { return name; }
    public void setName(String name) { this.name = name; }
    
    public String getLastMessage() { return lastMessage; }
    public void setLastMessage(String lastMessage) { this.lastMessage = lastMessage; }
    
    public long getLastMessageTime() { return lastMessageTime; }
    public void setLastMessageTime(long lastMessageTime) { this.lastMessageTime = lastMessageTime; }
    
    public int getUnreadCount() { return unreadCount; }
    public void setUnreadCount(int unreadCount) { this.unreadCount = unreadCount; }
    
    public boolean isGroup() { return isGroup; }
    public void setGroup(boolean group) { isGroup = group; }
    
    public List<String> getParticipants() { return participants; }
    public void setParticipants(List<String> participants) { this.participants = participants; }
    
    public String getAvatarPath() { return avatarPath; }
    public void setAvatarPath(String avatarPath) { this.avatarPath = avatarPath; }
}
