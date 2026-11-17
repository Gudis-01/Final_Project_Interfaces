using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Subtitulos.Audio;
using UnityEngine;
using Vosk;

namespace Subtitulos.Speech
{
    /// <summary>Una frase terminada, con el hablante que se detectó (o SpeakerTracker.Unknown).</summary>
    public readonly struct TranscriptSegment
    {
        public readonly string Text;
        public readonly int SpeakerId;

        public TranscriptSegment(string text, int speakerId)
        {
            Text = text;
            SpeakerId = speakerId;
        }
    }

    /// <summary>
    /// Convierte el audio del micrófono en texto con Vosk, sin internet.
    ///
    /// Flujo:
    ///   MicrophoneCapture (hilo principal) → bloques de ~100 ms → cola →
    ///   hilo de reconocimiento (Vosk) → resultados JSON → cola → hilo principal → eventos.
    ///
    /// Vosk se corre en otro hilo porque reconocer voz tarda unos milisegundos por bloque
    /// y no queremos que baje el framerate en el visor.
    /// </summary>
    [RequireComponent(typeof(MicrophoneCapture))]
    public class VoskTranscriber : MonoBehaviour
    {
        [Header("Modelos (en StreamingAssets, como .zip o carpeta)")]
        [Tooltip("Modelo de idioma. Ej: vosk-model-small-es-0.42")]
        [SerializeField] private string languageModel = "vosk-model-small-es-0.42";

        [Tooltip("Modelo de hablantes (opcional, para colores por persona). Ej: vosk-model-spk-0.4. Dejar vacío para desactivar.")]
        [SerializeField] private string speakerModel = "vosk-model-spk-0.4";

        [Header("Reconocimiento")]
        [Tooltip("Tamaño de cada bloque enviado a Vosk. Menos = más rápido en pantalla, más trabajo de CPU.")]
        [SerializeField, Range(50, 500)] private int chunkMilliseconds = 100;

        [Header("Hablantes")]
        [SerializeField, Range(0.2f, 0.95f)] private float speakerSimilarity = 0.55f;
        [SerializeField, Range(1, 8)] private int maxSpeakers = 6;

        /// <summary>Texto provisional mientras la persona sigue hablando (cambia muy seguido).</summary>
        public event Action<string> PartialResult;

        /// <summary>Frase terminada.</summary>
        public event Action<TranscriptSegment> FinalResult;

        /// <summary>Mensajes de estado para la UI ("Cargando modelo...", errores, etc.).</summary>
        public event Action<string> StatusChanged;

        public bool IsReady { get; private set; }
        public string Status { get; private set; } = "";

        /// <summary>Momento (Time.time) en que se detectó voz por última vez.</summary>
        public float LastSpeechTime { get; private set; } = float.NegativeInfinity;

        public SpeakerTracker Speakers { get; private set; }

        private MicrophoneCapture _mic;
        private Model _model;
        private SpkModel _spkModel;
        private VoskRecognizer _recognizer;
        private readonly object _recognizerLock = new object();

        private BlockingCollection<short[]> _audioQueue;
        private readonly ConcurrentQueue<string> _resultQueue = new ConcurrentQueue<string>();
        private CancellationTokenSource _cancel;
        private Thread _worker;

        private short[] _pending = Array.Empty<short>();
        private int _pendingCount;
        private string _lastPartial = "";

        // Estructura del JSON que regresa Vosk. JsonUtility llena solo los campos que existan.
        [Serializable]
        private class VoskJson
        {
            public string text;
            public string partial;
            public float[] spk;
            public int spk_frames;
        }

        private void Awake()
        {
            _mic = GetComponent<MicrophoneCapture>();
            Speakers = new SpeakerTracker(speakerSimilarity, maxSpeakers);
        }

        private IEnumerator Start()
        {
            SetStatus("Cargando modelo de voz...");
            Vosk.Vosk.SetLogLevel(-1); // Silenciar el log interno de Vosk.

            string languagePath = null, speakerPath = null, error = null;

            yield return VoskModelLoader.Prepare(languageModel, p => languagePath = p, e => error = e);
            if (error != null) { SetStatus(error); yield break; }

            if (!string.IsNullOrEmpty(speakerModel))
            {
                // Si falla el de hablantes seguimos sin colores, no es crítico.
                yield return VoskModelLoader.Prepare(speakerModel, p => speakerPath = p,
                    e => Debug.LogWarning("[Subtítulos] Sin modelo de hablantes: " + e));
            }

            // Cargar los modelos tarda unos segundos: se hace en otro hilo.
            Task load = Task.Run(() =>
            {
                _model = new Model(languagePath);
                if (speakerPath != null) _spkModel = new SpkModel(speakerPath);
            });
            while (!load.IsCompleted) yield return null;

            if (load.IsFaulted || _model == null)
            {
                SetStatus("No se pudo cargar el modelo de voz.");
                Debug.LogException(load.Exception);
                yield break;
            }

            // Esperar a que el micrófono esté grabando para conocer la frecuencia real.
            while (!_mic.IsRecording)
            {
                if (!string.IsNullOrEmpty(_mic.Error)) { SetStatus(_mic.Error); yield break; }
                yield return null;
            }

            CreateRecognizer();
            StartWorker();
            _mic.SamplesAvailable += OnSamples;

            IsReady = true;
            SetStatus(_spkModel != null ? "Escuchando" : "Escuchando (sin identificar hablantes)");
        }

        private void CreateRecognizer()
        {
            lock (_recognizerLock)
            {
                _recognizer?.Dispose();
                _recognizer = _spkModel != null
                    ? new VoskRecognizer(_model, _mic.SampleRate, _spkModel)
                    : new VoskRecognizer(_model, _mic.SampleRate);
                _recognizer.SetMaxAlternatives(0);
            }
        }

        /// <summary>Recibe audio del micrófono, lo convierte a 16 bits y lo junta en bloques.</summary>
        private void OnSamples(float[] samples, int count)
        {
            int chunkSize = Mathf.Max(1, _mic.SampleRate * chunkMilliseconds / 1000);
            if (_pending.Length != chunkSize)
            {
                _pending = new short[chunkSize];
                _pendingCount = 0;
            }

            for (int i = 0; i < count; i++)
            {
                _pending[_pendingCount++] = (short)(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue);
                if (_pendingCount == chunkSize)
                {
                    // Si el reconocedor va atrasado, se descarta audio en vez de acumular retraso.
                    if (!_audioQueue.TryAdd((short[])_pending.Clone()))
                        Debug.LogWarning("[Subtítulos] Reconocimiento atrasado, se descartó un bloque de audio.");
                    _pendingCount = 0;
                }
            }
        }

        private void StartWorker()
        {
            _audioQueue = new BlockingCollection<short[]>(boundedCapacity: 50);
            _cancel = new CancellationTokenSource();
            CancellationToken token = _cancel.Token;

            _worker = new Thread(() => RecognitionLoop(token)) { IsBackground = true, Name = "VoskRecognition" };
            _worker.Start();
        }

        /// <summary>Corre en el hilo de fondo. Nunca tocar objetos de Unity aquí.</summary>
        private void RecognitionLoop(CancellationToken token)
        {
            try
            {
                foreach (short[] chunk in _audioQueue.GetConsumingEnumerable(token))
                {
                    lock (_recognizerLock)
                    {
                        bool sentenceEnded = _recognizer.AcceptWaveform(chunk, chunk.Length);
                        _resultQueue.Enqueue(sentenceEnded ? _recognizer.Result() : _recognizer.PartialResult());
                    }
                }
            }
            catch (OperationCanceledException) { /* Cierre normal. */ }
            catch (Exception e) { _resultQueue.Enqueue("{\"error\":\"" + e.Message.Replace("\"", "'") + "\"}"); }
        }

        /// <summary>Procesa los resultados en el hilo principal y avisa a la UI.</summary>
        private void Update()
        {
            while (_resultQueue.TryDequeue(out string json))
            {
                if (json.StartsWith("{\"error\"")) { SetStatus("Error de reconocimiento"); Debug.LogError(json); continue; }

                VoskJson result = JsonUtility.FromJson<VoskJson>(json);
                if (result == null) continue;

                if (!string.IsNullOrWhiteSpace(result.text))
                {
                    int speaker = Speakers.Identify(result.spk, result.spk_frames);
                    _lastPartial = "";
                    LastSpeechTime = Time.time;
                    FinalResult?.Invoke(new TranscriptSegment(result.text.Trim(), speaker));
                }
                else if (result.partial != null && result.partial != _lastPartial)
                {
                    _lastPartial = result.partial;
                    if (_lastPartial.Length > 0) LastSpeechTime = Time.time;
                    PartialResult?.Invoke(_lastPartial);
                }
            }
        }

        /// <summary>Empieza una conversación nueva: olvida hablantes y texto pendiente.</summary>
        public void ResetConversation()
        {
            Speakers.Reset();
            _lastPartial = "";
            lock (_recognizerLock) _recognizer?.Reset();
        }

        private void SetStatus(string status)
        {
            Status = status;
            StatusChanged?.Invoke(status);
        }

        private void OnDestroy()
        {
            if (_mic != null) _mic.SamplesAvailable -= OnSamples;

            _cancel?.Cancel();
            _audioQueue?.CompleteAdding();
            _worker?.Join(500);

            lock (_recognizerLock)
            {
                _recognizer?.Dispose();
                _recognizer = null;
            }
            _spkModel?.Dispose();
            _model?.Dispose();
            _cancel?.Dispose();
        }
    }
}
