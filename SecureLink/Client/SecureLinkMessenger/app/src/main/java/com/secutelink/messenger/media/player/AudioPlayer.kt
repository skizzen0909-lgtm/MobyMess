package com.secutelink.messenger.media.player

import android.content.Context
import android.media.MediaPlayer
import android.net.Uri
import android.util.Log
import androidx.media3.common.MediaItem
import androidx.media3.common.Player
import androidx.media3.exoplayer.ExoPlayer
import java.io.File

/**
 * Менеджер для воспроизведения аудио (голосовых сообщений)
 */
class AudioPlayer(private val context: Context) {
    
    companion object {
        private const val TAG = "AudioPlayer"
    }
    
    private var exoPlayer: ExoPlayer? = null
    private var isPlaying = false
    private var currentFile: File? = null
    private var onCompletionListener: (() -> Unit)? = null
    private var onProgressListener: ((Int, Int) -> Unit)? = null // duration, position
    
    /**
     * Воспроизводит аудиофайл
     */
    fun play(file: File, onCompletion: (() -> Unit)? = null, onProgress: ((Int, Int) -> Unit)? = null) {
        if (!file.exists()) {
            Log.e(TAG, "File does not exist: ${file.absolutePath}")
            return
        }
        
        try {
            stop()
            
            onCompletionListener = onCompletion
            onProgressListener = onProgress
            currentFile = file
            
            exoPlayer = ExoPlayer.Builder(context).build().apply {
                setMediaItem(MediaItem.fromUri(Uri.fromFile(file)))
                prepare()
                playWhenReady = true
                
                addListener(object : Player.Listener {
                    override fun onPlaybackStateChanged(playbackState: Int) {
                        when (playbackState) {
                            Player.STATE_ENDED -> {
                                isPlaying = false
                                onCompletionListener?.invoke()
                            }
                            Player.STATE_READY -> {
                                isPlaying = true
                            }
                            Player.STATE_IDLE -> {
                                isPlaying = false
                            }
                        }
                    }
                    
                    override fun onIsPlayingChanged(isPlaying: Boolean) {
                        this@AudioPlayer.isPlaying = isPlaying
                    }
                })
            }
            
            Log.d(TAG, "Playing audio: ${file.absolutePath}")
            
        } catch (e: Exception) {
            Log.e(TAG, "Error playing audio", e)
        }
    }
    
    /**
     * Воспроизводит аудио из Uri
     */
    fun play(uri: Uri, onCompletion: (() -> Unit)? = null, onProgress: ((Int, Int) -> Unit)? = null) {
        try {
            stop()
            
            onCompletionListener = onCompletion
            onProgressListener = onProgress
            
            exoPlayer = ExoPlayer.Builder(context).build().apply {
                setMediaItem(MediaItem.fromUri(uri))
                prepare()
                playWhenReady = true
                
                addListener(object : Player.Listener {
                    override fun onPlaybackStateChanged(playbackState: Int) {
                        when (playbackState) {
                            Player.STATE_ENDED -> {
                                isPlaying = false
                                onCompletionListener?.invoke()
                            }
                            Player.STATE_READY -> {
                                isPlaying = true
                            }
                        }
                    }
                    
                    override fun onIsPlayingChanged(isPlaying: Boolean) {
                        this@AudioPlayer.isPlaying = isPlaying
                    }
                })
            }
            
            Log.d(TAG, "Playing audio from URI")
            
        } catch (e: Exception) {
            Log.e(TAG, "Error playing audio from URI", e)
        }
    }
    
    /**
     * Ставит на паузу
     */
    fun pause() {
        exoPlayer?.pause()
        isPlaying = false
        Log.d(TAG, "Playback paused")
    }
    
    /**
     * Возобновляет воспроизведение
     */
    fun resume() {
        exoPlayer?.play()
        isPlaying = true
        Log.d(TAG, "Playback resumed")
    }
    
    /**
     * Останавливает воспроизведение и освобождает ресурсы
     */
    fun stop() {
        exoPlayer?.apply {
            stop()
            release()
        }
        exoPlayer = null
        isPlaying = false
        currentFile = null
        Log.d(TAG, "Playback stopped")
    }
    
    /**
     * Проверяет, идет ли воспроизведение
     */
    fun isPlaying(): Boolean = isPlaying
    
    /**
     * Получает длительность аудио в миллисекундах
     */
    fun getDuration(): Int = exoPlayer?.duration?.toInt() ?: 0
    
    /**
     * Получает текущую позицию воспроизведения в миллисекундах
     */
    fun getCurrentPosition(): Int = exoPlayer?.currentPosition?.toInt() ?: 0
    
    /**
     * Перематывает на указанную позицию
     */
    fun seekTo(positionMs: Int) {
        exoPlayer?.seekTo(positionMs.toLong())
    }
    
    /**
     * Форматирует время в читаемый вид (мм:сс)
     */
    fun formatTime(ms: Int): String {
        val seconds = (ms / 1000) % 60
        val minutes = (ms / (1000 * 60)) % 60
        return String.format("%02d:%02d", minutes, seconds)
    }
}
