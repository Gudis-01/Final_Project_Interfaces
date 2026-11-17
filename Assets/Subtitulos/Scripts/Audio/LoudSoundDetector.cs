using System;
using Subtitulos.Speech;
using UnityEngine;

namespace Subtitulos.Audio
{
    /// <summary>
    /// Detecta sonidos fuertes que NO son voz (timbre, alarma, golpe en la puerta, claxon...).
    ///
    /// Cómo funciona:
    ///   1. Aprende poco a poco el "ruido de fondo" del lugar.
    ///   2. Si el volumen sube mucho por encima de ese fondo durante un momento, es un evento.
    ///   3. Si Vosk acaba de detectar voz, se ignora (para no alertar cuando alguien habla fuerte).
    ///
    /// No sabe QUÉ sonido es; solo que algo fuerte pasó y si fue corto (golpe) o sostenido (alarma).
    /// Clasificar el tipo de sonido es una mejora posible para después.
    /// </summary>
    public class LoudSoundDetector : MonoBehaviour
    {
        public enum SoundKind { Impact, Sustained }

        [SerializeField] private MicrophoneCapture microphone;
        [Tooltip("Opcional: para ignorar sonidos mientras alguien habla.")]
        [SerializeField] private VoskTranscriber transcriber;

        [Header("Sensibilidad")]
        [Tooltip("Cuántos dB por encima del ruido de fondo cuenta como 'fuerte'.")]
        [SerializeField, Range(6f, 40f)] private float dbAboveBackground = 18f;
        [Tooltip("Volumen mínimo absoluto para alertar (evita alertas en cuartos muy silenciosos).")]
        [SerializeField, Range(-60f, -5f)] private float minimumDb = -30f;
        [Tooltip("Duración (s) a partir de la cual se considera sonido sostenido (alarma, sirena).")]
        [SerializeField] private float sustainedSeconds = 1.0f;
        [Tooltip("Segundos sin alertas después de una alerta.")]
        [SerializeField] private float cooldownSeconds = 3f;
        [Tooltip("Segundos después de detectar voz en los que no se alerta.")]
        [SerializeField] private float ignoreAfterSpeechSeconds = 1.0f;

        /// <summary>Se dispara cuando se detecta un sonido fuerte.</summary>
        public event Action<SoundKind, float> SoundDetected;

        /// <summary>Nivel de ruido de fondo aprendido (dBFS).</summary>
        public float BackgroundDb { get; private set; } = -60f;

        private float _loudSince = -1f;
        private bool _alertedThisEvent;
        private float _lastAlertTime = float.NegativeInfinity;

        private void Reset()
        {
            microphone = GetComponent<MicrophoneCapture>();
            transcriber = GetComponent<VoskTranscriber>();
        }

        private void Update()
        {
            if (microphone == null || !microphone.IsRecording) return;

            float level = microphone.LevelDb;
            bool isLoud = level > minimumDb && level > BackgroundDb + dbAboveBackground;

            // El fondo solo se actualiza con sonidos "normales", y despacio.
            if (!isLoud) BackgroundDb = Mathf.Lerp(BackgroundDb, level, Time.deltaTime * 0.5f);

            if (!isLoud)
            {
                // Terminó un sonido fuerte: si fue corto y no se alertó, era un golpe.
                if (_loudSince >= 0f && !_alertedThisEvent) TryAlert(SoundKind.Impact, level);
                _loudSince = -1f;
                _alertedThisEvent = false;
                return;
            }

            if (_loudSince < 0f) _loudSince = Time.time;

            // Sostenido por mucho tiempo: alarma, sirena, timbre largo.
            if (!_alertedThisEvent && Time.time - _loudSince >= sustainedSeconds)
                TryAlert(SoundKind.Sustained, level);
        }

        private void TryAlert(SoundKind kind, float level)
        {
            bool speechRecently = transcriber != null && Time.time - transcriber.LastSpeechTime < ignoreAfterSpeechSeconds;
            bool inCooldown = Time.time - _lastAlertTime < cooldownSeconds;
            if (speechRecently || inCooldown) return;

            _alertedThisEvent = true;
            _lastAlertTime = Time.time;
            SoundDetected?.Invoke(kind, level);
        }
    }
}
