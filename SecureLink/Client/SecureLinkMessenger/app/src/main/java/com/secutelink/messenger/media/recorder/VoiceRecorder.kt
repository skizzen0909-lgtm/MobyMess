package com.secutelink.messenger.media.recorder

import android.content.Context
import android.media.MediaRecorder
import android.os.Build
import android.util.Log
import java.io.File
import java.io.IOException
import java.text.SimpleDateFormat
import java.util.*

/**
 * Менеджер для записи голосовых сообщений
 */
class VoiceRecorder(private val context: Context) {
    
    companion object {
        private const val TAG = "VoiceRecorder"
        private const val AUDIO_PREFIX = "VOICE_"
    }
    
    private var mediaRecorder: MediaRecorder? = null
    private var currentOutputFile: File? = null
    private var isRecording = false
    private var startTime: Long = 0
    
    private val dateFormat = SimpleDateFormat("yyyyMMdd_HHmmss", Locale.getDefault())
    
    /**
     * Начинает запись голосового сообщения
     */
    fun startRecording(): Boolean {
        if (isRecording) {
            Log.w(TAG, "Already recording")
            return false
        }
        
        try {
            val fileName = "${AUDIO_PREFIX}${dateFormat.format(Date())}.aac"
            val audioDir = File(context.filesDir, "media/audio")
            if (!audioDir.exists()) {
                audioDir.mkdirs()
            }
            
            currentOutputFile = File(audioDir, fileName)
            
            mediaRecorder = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
                MediaRecorder(context)
            } else {
                @Suppress("DEPRECATION")
                MediaRecorder()
            }.apply {
                setAudioSource(MediaRecorder.AudioSource.MIC)
                setOutputFormat(MediaRecorder.OutputFormat.AAC_ADTS)
                setAudioEncoder(MediaRecorder.AudioEncoder.AAC)
                setAudioEncodingBitRate(128000)
                setAudioSamplingRate(44100)
                setOutputFile(currentOutputFile?.absolutePath)
                
                prepare()
                start()
            }
            
            isRecording = true
            startTime = System.currentTimeMillis()
            
            Log.d(TAG, "Recording started: ${currentOutputFile?.absolutePath}")
            return true
            
        } catch (e: IOException) {
            Log.e(TAG, "Failed to start recording", e)
            releaseRecorder()
            return false
        } catch (e: IllegalStateException) {
            Log.e(TAG, "Recorder in illegal state", e)
            releaseRecorder()
            return false
        }
    }
    
    /**
     * Останавливает запись и возвращает файл с записью
     */
    fun stopRecording(): File? {
        if (!isRecording) {
            Log.w(TAG, "Not recording")
            return null
        }
        
        try {
            mediaRecorder?.apply {
                stop()
                reset()
            }
            
            val file = currentOutputFile
            releaseRecorder()
            
            isRecording = false
            currentOutputFile = null
            
            Log.d(TAG, "Recording stopped, duration: ${System.currentTimeMillis() - startTime}ms")
            return file
            
        } catch (e: RuntimeException) {
            Log.e(TAG, "Error stopping recording", e)
            releaseRecorder()
            isRecording = false
            currentOutputFile = null
            return null
        }
    }
    
    /**
     * Отменяет запись (удаляет файл)
     */
    fun cancelRecording(): Boolean {
        stopRecording()
        
        return currentOutputFile?.let { file ->
            if (file.exists()) {
                file.delete()
            }
            true
        } ?: false
    }
    
    /**
     * Проверяет, идет ли запись
     */
    fun isRecording(): Boolean = isRecording
    
    /**
     * Получает длительность текущей записи в миллисекундах
     */
    fun getRecordingDuration(): Long {
        return if (isRecording) {
            System.currentTimeMillis() - startTime
        } else {
            0L
        }
    }
    
    /**
     * Получает размер текущего файла записи
     */
    fun getCurrentFileSize(): Long {
        return currentOutputFile?.length() ?: 0L
    }
    
    /**
     * Освобождает ресурсы рекордера
     */
    private fun releaseRecorder() {
        try {
            mediaRecorder?.apply {
                reset()
                release()
            }
            mediaRecorder = null
        } catch (e: Exception) {
            Log.e(TAG, "Error releasing recorder", e)
        }
    }
    
    /**
     * Форматирует длительность в читаемый вид (мм:сс)
     */
    fun formatDuration(durationMs: Long): String {
        val seconds = (durationMs / 1000) % 60
        val minutes = (durationMs / (1000 * 60)) % 60
        return String.format("%02d:%02d", minutes, seconds)
    }
}
