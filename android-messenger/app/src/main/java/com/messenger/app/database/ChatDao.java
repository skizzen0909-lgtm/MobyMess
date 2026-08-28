package com.messenger.app.database;

import androidx.room.Dao;
import androidx.room.Delete;
import androidx.room.Insert;
import androidx.room.OnConflictStrategy;
import androidx.room.Query;
import androidx.room.Update;

import com.messenger.app.models.Chat;

import java.util.List;

@Dao
public interface ChatDao {
    @Insert(onConflict = OnConflictStrategy.REPLACE)
    long insert(Chat chat);
    
    @Update
    void update(Chat chat);
    
    @Delete
    void delete(Chat chat);
    
    @Query("SELECT * FROM chats ORDER BY lastMessageTime DESC")
    List<Chat> getAllChats();
    
    @Query("SELECT * FROM chats WHERE id = :id")
    Chat getChatById(String id);
    
    @Query("DELETE FROM chats WHERE id = :id")
    void deleteChatById(String id);
    
    @Query("SELECT COUNT(*) FROM chats")
    int getChatCount();
}
