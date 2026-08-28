package com.messenger.app.database;

import androidx.room.Dao;
import androidx.room.Delete;
import androidx.room.Insert;
import androidx.room.OnConflictStrategy;
import androidx.room.Query;
import androidx.room.Update;

import com.messenger.app.models.Message;
import com.messenger.app.models.Chat;
import com.messenger.app.models.Contact;

import java.util.List;

@Dao
public interface MessageDao {
    @Insert(onConflict = OnConflictStrategy.REPLACE)
    long insert(Message message);
    
    @Update
    void update(Message message);
    
    @Delete
    void delete(Message message);
    
    @Query("SELECT * FROM messages WHERE chatId = :chatId ORDER BY timestamp ASC")
    List<Message> getMessagesByChatId(String chatId);
    
    @Query("SELECT * FROM messages WHERE id = :id")
    Message getMessageById(long id);
    
    @Query("DELETE FROM messages WHERE chatId = :chatId")
    void deleteMessagesByChatId(String chatId);
    
    @Query("SELECT * FROM messages ORDER BY timestamp DESC LIMIT :limit")
    List<Message> getRecentMessages(int limit);
}
