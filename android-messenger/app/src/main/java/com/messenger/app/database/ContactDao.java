package com.messenger.app.database;

import androidx.room.Dao;
import androidx.room.Delete;
import androidx.room.Insert;
import androidx.room.OnConflictStrategy;
import androidx.room.Query;
import androidx.room.Update;

import com.messenger.app.models.Contact;

import java.util.List;

@Dao
public interface ContactDao {
    @Insert(onConflict = OnConflictStrategy.REPLACE)
    long insert(Contact contact);
    
    @Update
    void update(Contact contact);
    
    @Delete
    void delete(Contact contact);
    
    @Query("SELECT * FROM contacts ORDER BY name ASC")
    List<Contact> getAllContacts();
    
    @Query("SELECT * FROM contacts WHERE phoneNumber = :phoneNumber")
    Contact getContactByPhoneNumber(String phoneNumber);
    
    @Query("SELECT * FROM contacts WHERE isRegistered = 1 ORDER BY name ASC")
    List<Contact> getRegisteredContacts();
    
    @Query("SELECT COUNT(*) FROM contacts")
    int getContactCount();
    
    @Query("DELETE FROM contacts")
    void deleteAllContacts();
}
