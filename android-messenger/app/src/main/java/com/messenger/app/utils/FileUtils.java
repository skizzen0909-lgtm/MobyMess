package com.messenger.app.utils;

import android.content.Context;
import android.webkit.MimeTypeMap;

import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;

public class FileUtils {
    
    public static String getMimeType(String url) {
        String type = null;
        String extension = MimeTypeMap.getFileExtensionFromUrl(url);
        if (extension != null) {
            type = MimeTypeMap.getSingleton().getMimeTypeFromExtension(extension);
        }
        if (type == null) {
            type = "*/*";
        }
        return type;
    }
    
    public static String getExtensionFromMimeType(String mimeType) {
        if (mimeType == null) return "";
        
        if (mimeType.startsWith("image/")) {
            if (mimeType.contains("png")) return ".png";
            if (mimeType.contains("gif")) return ".gif";
            if (mimeType.contains("webp")) return ".webp";
            return ".jpg";
        } else if (mimeType.startsWith("video/")) {
            if (mimeType.contains("mp4")) return ".mp4";
            if (mimeType.contains("3gpp")) return ".3gp";
            return ".avi";
        } else if (mimeType.startsWith("audio/")) {
            if (mimeType.contains("mpeg")) return ".mp3";
            if (mimeType.contains("wav")) return ".wav";
            return ".aac";
        } else if (mimeType.contains("pdf")) {
            return ".pdf";
        } else if (mimeType.contains("word") || mimeType.contains("document")) {
            return ".docx";
        }
        
        return "";
    }
    
    public static String getMessageTypeFromMimeType(String mimeType) {
        if (mimeType == null) return "file";
        
        if (mimeType.startsWith("image/")) return "image";
        if (mimeType.startsWith("video/")) return "video";
        if (mimeType.startsWith("audio/")) return "audio";
        
        return "file";
    }
    
    public static File saveFileToAppDirectory(Context context, InputStream inputStream, 
                                               String fileName, String subDir) throws IOException {
        File appDir = new File(context.getExternalFilesDir(null), subDir);
        if (!appDir.exists()) {
            appDir.mkdirs();
        }
        
        File file = new File(appDir, fileName);
        try (OutputStream outputStream = new FileOutputStream(file)) {
            byte[] buffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = inputStream.read(buffer)) != -1) {
                outputStream.write(buffer, 0, bytesRead);
            }
        }
        
        return file;
    }
    
    public static long getFileSize(File file) {
        if (file != null && file.exists()) {
            return file.length();
        }
        return 0;
    }
    
    public static boolean deleteFile(File file) {
        return file != null && file.exists() && file.delete();
    }
}
