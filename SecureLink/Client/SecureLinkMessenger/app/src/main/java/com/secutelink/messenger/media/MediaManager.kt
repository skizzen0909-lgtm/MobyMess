package com.secutelink.messenger.media

import android.content.Context
import android.net.Uri
import android.util.Log
import androidx.core.content.FileProvider
import java.io.File
import java.io.FileOutputStream
import java.io.InputStream
import java.text.SimpleDateFormat
import java.util.*

/**
 * Менеджер для работы с медиафайлами (фото, видео, аудио)
 */
class MediaManager(private val context: Context) {
    
    companion object {
        private const val TAG = "MediaManager"
        private const val IMAGE_PREFIX = "IMG_"
        private const val VIDEO_PREFIX = "VID_"
        private const val AUDIO_PREFIX = "AUD_"
    }
    
    private val dateFormat = SimpleDateFormat("yyyyMMdd_HHmmss", Locale.getDefault())
    
    /**
     * Сохраняет изображение из Uri в файл и возвращает путь
     */
    fun saveImageFromUri(uri: Uri): File? {
        return try {
            val inputStream: InputStream? = context.contentResolver.openInputStream(uri)
            if (inputStream == null) {
                Log.e(TAG, "Cannot open input stream for URI: $uri")
                return null
            }
            
            val fileName = "${IMAGE_PREFIX}${dateFormat.format(Date())}.jpg"
            val file = File(getMediaDirectory(MediaType.IMAGE), fileName)
            
            val outputStream = FileOutputStream(file)
            inputStream.use { input ->
                outputStream.use { output ->
                    input.copyTo(output)
                }
            }
            
            Log.d(TAG, "Image saved: ${file.absolutePath}")
            file
        } catch (e: Exception) {
            Log.e(TAG, "Error saving image", e)
            null
        }
    }
    
    /**
     * Сохраняет видео из Uri в файл
     */
    fun saveVideoFromUri(uri: Uri): File? {
        return try {
            val inputStream: InputStream? = context.contentResolver.openInputStream(uri)
            if (inputStream == null) {
                Log.e(TAG, "Cannot open input stream for URI: $uri")
                return null
            }
            
            val fileName = "${VIDEO_PREFIX}${dateFormat.format(Date())}.mp4"
            val file = File(getMediaDirectory(MediaType.VIDEO), fileName)
            
            val outputStream = FileOutputStream(file)
            inputStream.use { input ->
                outputStream.use { output ->
                    input.copyTo(output)
                }
            }
            
            Log.d(TAG, "Video saved: ${file.absolutePath}")
            file
        } catch (e: Exception) {
            Log.e(TAG, "Error saving video", e)
            null
        }
    }
    
    /**
     * Сохраняет аудио из Uri в файл
     */
    fun saveAudioFromUri(uri: Uri): File? {
        return try {
            val inputStream: InputStream? = context.contentResolver.openInputStream(uri)
            if (inputStream == null) {
                Log.e(TAG, "Cannot open input stream for URI: $uri")
                return null
            }
            
            val fileName = "${AUDIO_PREFIX}${dateFormat.format(Date())}.aac"
            val file = File(getMediaDirectory(MediaType.AUDIO), fileName)
            
            val outputStream = FileOutputStream(file)
            inputStream.use { input ->
                outputStream.use { output ->
                    input.copyTo(output)
                }
            }
            
            Log.d(TAG, "Audio saved: ${file.absolutePath}")
            file
        } catch (e: Exception) {
            Log.e(TAG, "Error saving audio", e)
            null
        }
    }
    
    /**
     * Сохраняет произвольный файл
     */
    fun saveFileFromUri(uri: Uri, fileName: String): File? {
        return try {
            val inputStream: InputStream? = context.contentResolver.openInputStream(uri)
            if (inputStream == null) {
                Log.e(TAG, "Cannot open input stream for URI: $uri")
                return null
            }
            
            val file = File(getMediaDirectory(MediaType.FILE), fileName)
            
            val outputStream = FileOutputStream(file)
            inputStream.use { input ->
                outputStream.use { output ->
                    input.copyTo(output)
                }
            }
            
            Log.d(TAG, "File saved: ${file.absolutePath}")
            file
        } catch (e: Exception) {
            Log.e(TAG, "Error saving file", e)
            null
        }
    }
    
    /**
     * Получает Uri для файла (для отправки через FileProvider)
     */
    fun getUriForFile(file: File): Uri {
        return FileProvider.getUriForFile(
            context,
            "${context.packageName}.fileprovider",
            file
        )
    }
    
    /**
     * Удаляет файл
     */
    fun deleteFile(file: File): Boolean {
        return if (file.exists()) {
            file.delete()
        } else {
            false
        }
    }
    
    /**
     * Получает размер файла в байтах
     */
    fun getFileSize(file: File): Long {
        return if (file.exists()) {
            file.length()
        } else {
            0L
        }
    }
    
    /**
     * Создает директорию для медиафайлов указанного типа
     */
    private fun getMediaDirectory(type: MediaType): File {
        val dir = File(context.filesDir, "media/${type.folderName}")
        if (!dir.exists()) {
            dir.mkdirs()
        }
        return dir
    }
    
    /**
     * Очищает все медиафайлы
     */
    fun clearAllMedia() {
        MediaType.values().forEach { type ->
            val dir = getMediaDirectory(type)
            dir.listFiles()?.forEach { file ->
                file.delete()
            }
        }
    }
}

enum class MediaType(val folderName: String) {
    IMAGE("images"),
    VIDEO("videos"),
    AUDIO("audio"),
    FILE("files")
}
