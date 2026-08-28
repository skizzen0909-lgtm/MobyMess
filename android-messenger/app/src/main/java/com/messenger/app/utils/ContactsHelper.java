package com.messenger.app.utils;

import android.Manifest;
import android.app.Activity;
import android.content.Context;
import android.content.pm.PackageManager;
import android.database.Cursor;
import android.net.Uri;
import android.provider.ContactsContract;
import android.widget.Toast;

import androidx.core.app.ActivityCompat;
import androidx.core.content.ContextCompat;

import com.messenger.app.models.Contact;

import java.util.ArrayList;
import java.util.List;

public class ContactsHelper {
    private static final int PERMISSIONS_REQUEST_READ_CONTACTS = 100;
    
    public static boolean checkPermission(Activity activity) {
        return ContextCompat.checkSelfPermission(activity, Manifest.permission.READ_CONTACTS) 
                == PackageManager.PERMISSION_GRANTED;
    }
    
    public static void requestPermission(Activity activity) {
        ActivityCompat.requestPermissions(activity,
                new String[]{Manifest.permission.READ_CONTACTS},
                PERMISSIONS_REQUEST_READ_CONTACTS);
    }
    
    public static List<Contact> getContactsFromPhone(Context context) {
        List<Contact> contacts = new ArrayList<>();
        
        if (!checkPermission((Activity) context)) {
            return contacts;
        }
        
        Uri uri = ContactsContract.CommonDataKinds.Phone.CONTENT_URI;
        String[] projection = new String[] {
            ContactsContract.CommonDataKinds.Phone.NUMBER,
            ContactsContract.CommonDataKinds.Phone.DISPLAY_NAME,
            ContactsContract.CommonDataKinds.Phone.PHOTO_URI
        };
        
        Cursor cursor = context.getContentResolver().query(uri, projection, null, null, null);
        
        if (cursor != null) {
            int nameIndex = cursor.getColumnIndex(ContactsContract.CommonDataKinds.Phone.DISPLAY_NAME);
            int numberIndex = cursor.getColumnIndex(ContactsContract.CommonDataKinds.Phone.NUMBER);
            int photoIndex = cursor.getColumnIndex(ContactsContract.CommonDataKinds.Phone.PHOTO_URI);
            
            while (cursor.moveToNext()) {
                String name = cursor.getString(nameIndex);
                String number = cursor.getString(numberIndex);
                String photoUri = cursor.getString(photoIndex);
                
                // Очистка номера телефона
                number = normalizePhoneNumber(number);
                
                Contact contact = new Contact(name, number);
                contact.setAvatarPath(photoUri);
                contacts.add(contact);
            }
            
            cursor.close();
        }
        
        return contacts;
    }
    
    private static String normalizePhoneNumber(String phoneNumber) {
        if (phoneNumber == null) return "";
        
        // Удаляем все нецифровые символы кроме +
        String normalized = phoneNumber.replaceAll("[^\\d+]", "");
        
        // Если номер начинается с 8, заменяем на +7 (для России)
        if (normalized.startsWith("8") && normalized.length() == 11) {
            normalized = "+7" + normalized.substring(1);
        }
        
        // Если номер не начинается с +, добавляем код страны по умолчанию
        if (!normalized.startsWith("+") && normalized.length() == 10) {
            normalized = "+7" + normalized; // Для России
        }
        
        return normalized;
    }
}
