using System;
using System.Collections;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace Subtitulos.Audio
{
    /// <summary>
    /// Captura audio del micrófono de forma continua y lo reparte a quien lo necesite
    /// (transcriptor, medidor de volumen, detector de sonidos).
    ///
    /// También calcula el volumen actual en decibeles (dBFS: 0 = máximo, valores negativos = más bajo).
    /// </summary>
    public class MicrophoneCapture : MonoBehaviour
    {
        [Tooltip("Frecuencia que pide Vosk. Si el dispositivo no la soporta se usa la más cercana.")]
        [SerializeField] private int preferredSampleRate = 16000;

        [Tooltip("Dejar vacío para usar el micrófono por defecto.")]
        [SerializeField] private string deviceName = "";

        [Tooltip("Iniciar al arrancar la escena.")]
        [SerializeField] private bool startOnAwake = true;

        [Header("Sensibilidad (para el reconocimiento de voz)")]
        [Tooltip("Amplifica el audio que se manda a Vosk. Los micrófonos del visor suelen captar bajito.")]
        [SerializeField] private bool autoGain = true;
        [Tooltip("Ganancia mínima siempre aplicada (1 = sin cambio).")]
        [SerializeField, Range(1f, 8f)] private float baseGain = 3f;
        [Tooltip("Ganancia máxima que puede subir sola cuando alguien habla bajito.")]
        [SerializeField, Range(1f, 40f)] private float maxAutoGain = 30f;
        [Tooltip("Volumen objetivo (RMS 0..1) al que trata de llegar la voz.")]
        [SerializeField, Range(0.02f, 0.4f)] private float targetRms = 0.12f;
        [Tooltip("Por debajo de este volumen (RMS) se considera silencio y la ganancia no sube (evita amplificar ruido).")]
        [SerializeField, Range(0.0003f, 0.05f)] private float noiseFloorRms = 0.0012f;

        [Header("Solo la voz cercana")]
        [Tooltip("Silencia lo que se manda a Vosk cuando la voz es mucho más baja que la de quien habla más cerca.")]
        [SerializeField] private bool nearVoiceOnly = false;
        [Tooltip("Cuántos dB más baja que la voz más cercana se ignora. Menor = ignora más voces del fondo (6 dB ≈ el doble de distancia).")]
        [SerializeField, Range(4f, 40f)] private float gateMarginDb = 24f;
        [Tooltip("Nunca se transcribe por debajo de este volumen (dBFS), aunque sea la voz más cercana.")]
        [SerializeField, Range(-80f, -30f)] private float gateFloorDb = -58f;
        [Tooltip("Qué tan rápido (dB por segundo) olvida a la voz más fuerte para adaptarse si cambia de lugar o de persona.")]
        [SerializeField, Range(0.2f, 6f)] private float peakDecayDbPerSecond = 1.5f;

        private const int ClipLengthSeconds = 2;
        private float _peakVoiceDb = -80f;
        private float _gateOpenUntil;

        /// <summary>Activa o desactiva el filtro de voces lejanas (lo controlan los ajustes).</summary>
        public bool NearVoiceOnly { get => nearVoiceOnly; set => nearVoiceOnly = value; }
        private float _gain = 6f;
        private float _noiseRms = 0.005f;
        private const float SilenceDb = -80f;

        private AudioClip _clip;
        private int _lastPosition;
        private float[] _readBuffer = Array.Empty<float>();
        private float[] _monoBuffer = Array.Empty<float>();

        /// <summary>Se dispara en el hilo principal con muestras nuevas (mono, -1..1). El arreglo se reutiliza: cópienlo si lo guardan.</summary>
        public event Action<float[], int> SamplesAvailable;

        /// <summary>Frecuencia real con la que se está grabando.</summary>
        public int SampleRate { get; private set; }

        /// <summary>True cuando el micrófono está grabando.</summary>
        public bool IsRecording => _clip != null && Microphone.IsRecording(ActiveDevice);

        /// <summary>Volumen del último bloque en dBFS (entre -80 y 0).</summary>
        public float LevelDb { get; private set; } = SilenceDb;

        /// <summary>Volumen normalizado 0..1 (útil para barras de UI).</summary>
        public float NormalizedLevel => Mathf.InverseLerp(SilenceDb, 0f, LevelDb);

        /// <summary>Mensaje de error si no se pudo iniciar (sin permiso, sin micrófono, etc.).</summary>
        public string Error { get; private set; }

        private string ActiveDevice => string.IsNullOrEmpty(deviceName) ? null : deviceName;

        private IEnumerator Start()
        {
            if (startOnAwake) yield return StartRecording();
        }

        /// <summary>Pide permiso (Android/Quest) y empieza a grabar.</summary>
        public IEnumerator StartRecording()
        {
            Error = null;

#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                Permission.RequestUserPermission(Permission.Microphone);
                // Esperar a que el usuario responda el cuadro de permiso.
                float timeout = 30f;
                while (!Permission.HasUserAuthorizedPermission(Permission.Microphone) && timeout > 0f)
                {
                    timeout -= Time.unscaledDeltaTime;
                    yield return null;
                }
                if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
                {
                    Error = "Sin permiso de micrófono.";
                    yield break;
                }
            }
#endif
            if (Microphone.devices.Length == 0)
            {
                Error = "No se encontró ningún micrófono.";
                yield break;
            }

            SampleRate = ChooseSampleRate();
            _clip = Microphone.Start(ActiveDevice, true, ClipLengthSeconds, SampleRate);

            // Esperar a que el micrófono empiece a entregar datos.
            float wait = 3f;
            while (Microphone.GetPosition(ActiveDevice) <= 0 && wait > 0f)
            {
                wait -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (Microphone.GetPosition(ActiveDevice) <= 0)
            {
                Error = "El micrófono no responde.";
                StopRecording();
                yield break;
            }

            _lastPosition = Microphone.GetPosition(ActiveDevice);
        }

        public void StopRecording()
        {
            if (Microphone.IsRecording(ActiveDevice)) Microphone.End(ActiveDevice);
            _clip = null;
            LevelDb = SilenceDb;
        }

        private int ChooseSampleRate()
        {
            Microphone.GetDeviceCaps(ActiveDevice, out int min, out int max);
            // 0 y 0 significa "cualquier frecuencia".
            if (min == 0 && max == 0) return preferredSampleRate;
            return Mathf.Clamp(preferredSampleRate, min, max);
        }

        private void Update()
        {
            if (_clip == null) return;

            int position = Microphone.GetPosition(ActiveDevice);
            if (position < 0 || position == _lastPosition) return;

            // Cuántas muestras nuevas hay (el clip es circular).
            int available = position - _lastPosition;
            if (available < 0) available += _clip.samples;

            int channels = _clip.channels;
            EnsureCapacity(ref _readBuffer, available * channels);
            EnsureCapacity(ref _monoBuffer, available);

            ReadCircular(_lastPosition, available, channels);
            _lastPosition = position;

            // Pasar a mono (si el micrófono es estéreo se toma el primer canal).
            for (int i = 0; i < available; i++) _monoBuffer[i] = _readBuffer[i * channels];

            // El volumen (barra y detector de sonidos) se mide con el audio original, sin amplificar.
            LevelDb = ComputeDb(_monoBuffer, available);
            if (!UpdateGate())
                Array.Clear(_monoBuffer, 0, available); // silencio: Vosk termina la frase en vez de mezclar voces lejanas
            else
                ApplyGain(available);
            SamplesAvailable?.Invoke(_monoBuffer, available);
        }

        /// <summary>
        /// True si este bloque debe transcribirse. Sigue el nivel de la voz más fuerte (la más cercana)
        /// y deja pasar solo lo que no esté mucho más bajo, con un margen para no cortar sílabas suaves.
        /// </summary>
        private bool UpdateGate()
        {
            float db = LevelDb;
            _peakVoiceDb = Mathf.Max(db, _peakVoiceDb - peakDecayDbPerSecond * Time.unscaledDeltaTime);
            if (!nearVoiceOnly) return true;

            float threshold = Mathf.Max(gateFloorDb, _peakVoiceDb - gateMarginDb);
            if (db >= threshold) _gateOpenUntil = Time.unscaledTime + 0.35f;
            return Time.unscaledTime < _gateOpenUntil;
        }

        /// <summary>Amplifica el audio (ganancia automática con ataque rápido y subida lenta).</summary>
        private void ApplyGain(int count)
        {
            float gain = baseGain;
            if (autoGain)
            {
                double sum = 0;
                for (int i = 0; i < count; i++) sum += _monoBuffer[i] * _monoBuffer[i];
                float rms = (float)Math.Sqrt(sum / Math.Max(1, count));

                // Ruido de fondo estimado: baja rápido, sube muy despacio (la voz dura poco).
                _noiseRms = rms < _noiseRms ? Mathf.Lerp(_noiseRms, rms, 0.2f) : Mathf.Lerp(_noiseRms, rms, 0.003f);
                _noiseRms = Mathf.Max(_noiseRms, 0.0003f);

                // Solo se considera voz lo que está claramente por encima del ruido de fondo.
                if (rms > noiseFloorRms && rms > _noiseRms * 2.5f)
                {
                    float wanted = Mathf.Clamp(targetRms / rms, baseGain, maxAutoGain);
                    // Si ya está muy fuerte, baja rápido (evita que se sature); si está bajito, sube despacio.
                    _gain = wanted < _gain ? Mathf.Lerp(_gain, wanted, 0.6f) : Mathf.Lerp(_gain, wanted, 0.2f);
                }
                gain = Mathf.Max(baseGain, _gain);

                // Tope: el ruido de fondo nunca debe quedar tan fuerte como una voz. Si Vosk no
                // percibe silencios, no termina las frases y todo se vuelve lento y confuso.
                gain = Mathf.Min(gain, Mathf.Max(baseGain, 0.025f / _noiseRms));
            }

            for (int i = 0; i < count; i++)
            {
                // Limitador suave para no distorsionar con picos.
                float v = _monoBuffer[i] * gain;
                _monoBuffer[i] = v > 0.9f ? 0.9f + 0.1f * (float)Math.Tanh((v - 0.9f) * 10f)
                              : v < -0.9f ? -0.9f - 0.1f * (float)Math.Tanh((-v - 0.9f) * 10f)
                              : v;
            }
        }

        /// <summary>Lee 'count' muestras empezando en 'start', dando la vuelta al final del clip si hace falta.</summary>
        private void ReadCircular(int start, int count, int channels)
        {
            int untilEnd = _clip.samples - start;
            if (count <= untilEnd)
            {
                ReadSegment(start, count, channels, 0);
                return;
            }
            ReadSegment(start, untilEnd, channels, 0);
            ReadSegment(0, count - untilEnd, channels, untilEnd * channels);
        }

        private float[] _segment = Array.Empty<float>();

        private void ReadSegment(int start, int count, int channels, int destOffset)
        {
            if (count <= 0) return;
            // AudioClip.GetData llena el arreglo completo, así que necesita uno del tamaño exacto.
            if (_segment.Length != count * channels) _segment = new float[count * channels];
            _clip.GetData(_segment, start);
            Array.Copy(_segment, 0, _readBuffer, destOffset, count * channels);
        }

        /// <summary>Volumen RMS convertido a decibeles.</summary>
        public static float ComputeDb(float[] samples, int count)
        {
            if (count <= 0) return SilenceDb;
            double sum = 0;
            for (int i = 0; i < count; i++) sum += samples[i] * samples[i];
            float rms = (float)Math.Sqrt(sum / count);
            if (rms <= 0.00001f) return SilenceDb;
            return Mathf.Max(SilenceDb, 20f * Mathf.Log10(rms));
        }

        private static void EnsureCapacity(ref float[] buffer, int size)
        {
            if (buffer.Length < size) buffer = new float[size];
        }

        private void OnDisable() => StopRecording();
    }
}
