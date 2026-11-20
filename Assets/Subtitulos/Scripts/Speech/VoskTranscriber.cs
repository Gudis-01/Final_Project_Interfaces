using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Subtitulos.Audio;
using UnityEngine;
using Vosk;

namespace Subtitulos.Speech
{
    /// <summary>Idioma de reconocimiento que elige el usuario.</summary>
    public enum RecognitionLanguage { Spanish = 0, English = 1, Auto = 2 }

    /// <summary>Una palabra reconocida y qué tan seguro está el modelo (0..1).</summary>
    public readonly struct RecognizedWord
    {
        public readonly string Text;
        public readonly float Confidence;

        public RecognizedWord(string text, float confidence)
        {
            Text = text;
            Confidence = confidence;
        }
    }

    /// <summary>Una frase terminada: palabras, hablante (o SpeakerTracker.Unknown) e idioma ("es"/"en").</summary>
    public readonly struct TranscriptSegment
    {
        public readonly IReadOnlyList<RecognizedWord> Words;
        public readonly int SpeakerId;
        public readonly string Language;

        public TranscriptSegment(IReadOnlyList<RecognizedWord> words, int speakerId, string language)
        {
            Words = words;
            SpeakerId = speakerId;
            Language = language;
        }

        public string Text => string.Join(" ", System.Linq.Enumerable.Select(Words, w => w.Text));
    }

    /// <summary>
    /// Convierte el audio del micrófono en texto con Vosk, sin internet, en español, inglés o ambos.
    ///
    /// Flujo:
    ///   MicrophoneCapture (hilo principal) → bloques de ~100 ms → cola →
    ///   hilo de reconocimiento (uno o dos reconocedores Vosk) → JSON → cola → hilo principal → eventos.
    ///
    /// Modo automático: el mismo audio entra a los dos reconocedores (español e inglés). Cuando
    /// terminan la frase, se queda la versión con mayor confianza promedio por palabra. Un modelo
    /// que escucha otro idioma "adivina" palabras con confianza baja, así que esto funciona bien
    /// para frases normales (en frases de 1-2 palabras puede equivocarse de idioma).
    /// </summary>
    [RequireComponent(typeof(MicrophoneCapture))]
    public class VoskTranscriber : MonoBehaviour
    {
        [Header("Modelos (en StreamingAssets, como .zip o carpeta)")]
        [SerializeField] private string spanishModel = "vosk-model-small-es-0.42";
        [SerializeField] private string englishModel = "vosk-model-small-en-us-0.15";
        [Tooltip("Modelo de hablantes (opcional, para colores por persona). Vacío = desactivado.")]
        [SerializeField] private string speakerModel = "vosk-model-spk-0.4";

        [Header("Reconocimiento")]
        [SerializeField] private RecognitionLanguage language = RecognitionLanguage.English;
        [Tooltip("Tamaño de cada bloque enviado a Vosk. Menos = más rápido en pantalla, más CPU.")]
        [SerializeField, Range(50, 500)] private int chunkMilliseconds = 100;
        [Tooltip("En modo automático: cuánto esperar (s) a que el otro idioma termine la frase antes de decidir.")]
        [SerializeField] private float autoDecisionWindow = 0.6f;

        [Header("Filtro de ruido")]
        [Tooltip("Frases con confianza promedio menor a esto se descartan (ruido, golpes, música).")]
        [SerializeField, Range(0f, 1f)] private float minAverageConfidence = 0.5f;
        [Tooltip("Una sola palabra suelta necesita al menos esta confianza (el ruido suele dar 'the', 'huh', 'eh').")]
        [SerializeField, Range(0f, 1f)] private float minSingleWordConfidence = 0.8f;

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
        public bool HasError { get; private set; }
        public string Status { get; private set; } = "";
        public RecognitionLanguage Language => language;

        /// <summary>Idioma de la última frase reconocida ("es" o "en").</summary>
        public string LastLanguage { get; private set; } = "es";

        /// <summary>Momento (Time.time) en que se detectó voz por última vez.</summary>
        public float LastSpeechTime { get; private set; } = float.NegativeInfinity;

        public SpeakerTracker Speakers { get; private set; }

        // Modelos más precisos (opcionales): si el .zip está en StreamingAssets se usan automáticamente.
        private const string BigEnglishModel = ""; // desactivado: el lgraph es muy lento en el Quest
        private const string BigSpanishModel = "";

        private const string Es = "es";
        private const string En = "en";

        private MicrophoneCapture _mic;
        private SpkModel _spkModel;
        private readonly Dictionary<string, Model> _models = new Dictionary<string, Model>();
        private readonly Dictionary<string, VoskRecognizer> _recognizers = new Dictionary<string, VoskRecognizer>();
        private readonly object _recognizerLock = new object();
        private volatile int _languageFlags; // bit 0 = es, bit 1 = en (lo lee el hilo de fondo)

        private BlockingCollection<short[]> _audioQueue;
        private readonly ConcurrentQueue<(string lang, bool final, string json)> _results =
            new ConcurrentQueue<(string, bool, string)>();
        private CancellationTokenSource _cancel;
        private Thread _worker;

        private short[] _pending = Array.Empty<short>();
        private int _pendingCount;
        private readonly Dictionary<string, string> _partials = new Dictionary<string, string> { { Es, "" }, { En, "" } };
        private string _lastPartialShown = "";

        // Modo automático: frases terminadas esperando a ser comparadas.
        private readonly Dictionary<string, VoskJson> _pendingFinals = new Dictionary<string, VoskJson>();
        private float _firstPendingTime;
        private readonly Dictionary<string, float> _ignoreFinalUntil = new Dictionary<string, float> { { Es, 0f }, { En, 0f } };

        // Estructura del JSON que regresa Vosk. JsonUtility llena solo los campos que existan.
        [Serializable] private class WordJson { public float conf; public string word; }

        [Serializable]
        private class VoskJson
        {
            public string text;
            public string partial;
            public WordJson[] result;
            public float[] spk;
            public int spk_frames;
        }

        private void Awake()
        {
            _mic = GetComponent<MicrophoneCapture>();
            Speakers = new SpeakerTracker(speakerSimilarity, maxSpeakers);
            ApplyLanguageFlags();
        }

        private IEnumerator Start()
        {
            SetStatus("Cargando modelos de voz…");
            Vosk.Vosk.SetLogLevel(-1); // Silenciar el log interno de Vosk.

            var paths = new Dictionary<string, string>();
            string spkPath = null;

            // Para cada idioma se prueba primero el modelo grande (más preciso) y, si no está en
            // StreamingAssets, el pequeño.
            foreach (var (code, names) in new[]
            {
                (Es, new[] { BigSpanishModel, spanishModel }),
                (En, new[] { BigEnglishModel, englishModel }),
            })
            {
                foreach (string name in names)
                {
                    if (string.IsNullOrEmpty(name) || paths.ContainsKey(code)) continue;
                    yield return VoskModelLoader.Prepare(name, p => paths[code] = p,
                        e => Debug.Log($"[Subtítulos] Modelo '{name}' no disponible: {e}"));
                    if (paths.ContainsKey(code)) Debug.Log($"[Subtítulos] Modelo {code}: {name}");
                }
            }

            if (paths.Count == 0)
            {
                Fail("No se encontró ningún modelo de voz en StreamingAssets.");
                yield break;
            }

            if (!string.IsNullOrEmpty(speakerModel))
            {
                yield return VoskModelLoader.Prepare(speakerModel, p => spkPath = p,
                    e => Debug.LogWarning("[Subtítulos] Sin modelo de hablantes: " + e));
            }

            // Cargar los modelos tarda unos segundos: se hace en otro hilo.
            Task load = Task.Run(() =>
            {
                foreach (var pair in paths) _models[pair.Key] = new Model(pair.Value);
                if (spkPath != null) _spkModel = new SpkModel(spkPath);
            });
            while (!load.IsCompleted) yield return null;

            if (load.IsFaulted || _models.Count == 0)
            {
                Debug.LogException(load.Exception);
                Fail("No se pudo cargar el modelo de voz.");
                yield break;
            }

            // Si falta un idioma, se usa el que sí está.
            if (!_models.ContainsKey(En)) language = RecognitionLanguage.Spanish;
            else if (!_models.ContainsKey(Es)) language = RecognitionLanguage.English;
            ApplyLanguageFlags();

            // Esperar a que el micrófono esté grabando para conocer la frecuencia real.
            while (!_mic.IsRecording)
            {
                if (!string.IsNullOrEmpty(_mic.Error)) { Fail(_mic.Error); yield break; }
                yield return null;
            }

            lock (_recognizerLock)
            {
                foreach (var pair in _models) _recognizers[pair.Key] = CreateRecognizer(pair.Value);
            }

            StartWorker();
            _mic.SamplesAvailable += OnSamples;

            IsReady = true;
            SetStatus("Escuchando");
        }

        private VoskRecognizer CreateRecognizer(Model model)
        {
            VoskRecognizer recognizer = _spkModel != null
                ? new VoskRecognizer(model, _mic.SampleRate, _spkModel)
                : new VoskRecognizer(model, _mic.SampleRate);
            recognizer.SetMaxAlternatives(0);
            recognizer.SetWords(true); // confianza por palabra (sirve para modo automático y para atenuar dudas)
            return recognizer;
        }

        /// <summary>Cambia el idioma de reconocimiento (desde los ajustes).</summary>
        public void SetLanguage(RecognitionLanguage value)
        {
            if (value == RecognitionLanguage.English && _models.Count > 0 && !_models.ContainsKey(En)) value = RecognitionLanguage.Spanish;
            if (value == RecognitionLanguage.Spanish && _models.Count > 0 && !_models.ContainsKey(Es)) value = RecognitionLanguage.English;
            if (value == language) return;

            language = value;
            ApplyLanguageFlags();
            ResetRecognizers();
        }

        private void ApplyLanguageFlags()
        {
            switch (language)
            {
                case RecognitionLanguage.Spanish: _languageFlags = 1; LastLanguage = Es; break;
                case RecognitionLanguage.English: _languageFlags = 2; LastLanguage = En; break;
                default: _languageFlags = 3; break;
            }
        }

        private bool IsActive(string code) => (_languageFlags & (code == Es ? 1 : 2)) != 0;

        /// <summary>Recibe audio del micrófono, lo convierte a 16 bits y lo junta en bloques.</summary>
        private void OnSamples(float[] samples, int count)
        {
            // Bloques de al menos 150 ms: más chicos gastan CPU en cada resultado parcial sin verse más rápido.
            int chunkSize = Mathf.Max(1, _mic.SampleRate * Mathf.Max(chunkMilliseconds, 150) / 1000);
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
            _audioQueue = new BlockingCollection<short[]>(boundedCapacity: 120);
            _cancel = new CancellationTokenSource();
            CancellationToken token = _cancel.Token;

            _worker = new Thread(() => RecognitionLoop(token)) { IsBackground = true, Name = "VoskRecognition", Priority = System.Threading.ThreadPriority.AboveNormal };
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
                        foreach (var pair in _recognizers)
                        {
                            if (!IsActive(pair.Key)) continue;
                            bool ended = pair.Value.AcceptWaveform(chunk, chunk.Length);
                            if (ended)
                                _results.Enqueue((pair.Key, true, pair.Value.Result()));
                            else if (_audioQueue.Count <= 1)
                                // Si va atrasado se salta el texto provisional (es lo que más CPU gasta) hasta ponerse al corriente.
                                _results.Enqueue((pair.Key, false, pair.Value.PartialResult()));
                        }
                    }
                }
            }
            catch (OperationCanceledException) { /* Cierre normal. */ }
            catch (Exception e) { _results.Enqueue(("error", false, e.Message)); }
        }

        /// <summary>Procesa los resultados en el hilo principal y avisa a la UI.</summary>
        private void Update()
        {
            while (_results.TryDequeue(out var item))
            {
                if (item.lang == "error")
                {
                    Debug.LogError("[Subtítulos] " + item.json);
                    Fail("Error de reconocimiento");
                    continue;
                }
                if (!IsActive(item.lang)) continue;

                VoskJson result = JsonUtility.FromJson<VoskJson>(item.json);
                if (result == null) continue;

                if (item.final) OnFinal(item.lang, result);
                else OnPartial(item.lang, result.partial ?? "");
            }

            // Modo automático: decidir cuando ya llegaron los dos idiomas o pasó el tiempo de espera.
            if (_pendingFinals.Count > 0 &&
                (_pendingFinals.Count >= 2 || Time.time - _firstPendingTime > autoDecisionWindow))
            {
                DecideAutoLanguage();
            }
        }

        private void OnPartial(string lang, string text)
        {
            _partials[lang] = text;
            if (text.Length > 0) LastSpeechTime = Time.time;

            // En automático se muestra el parcial del último idioma ganador (o el otro si ese está vacío).
            string other = lang == Es ? En : Es;
            string shown = language != RecognitionLanguage.Auto
                ? text
                : (_partials[LastLanguage].Length > 0 ? _partials[LastLanguage] : _partials[other]);

            if (shown == _lastPartialShown) return;
            _lastPartialShown = shown;
            PartialResult?.Invoke(Format(shown, LastLanguage));
        }

        private void OnFinal(string lang, VoskJson result)
        {
            _partials[lang] = "";

            if (language != RecognitionLanguage.Auto)
            {
                Emit(lang, result);
                return;
            }

            // El otro idioma ya ganó esta frase hace un momento: se ignora su copia tardía.
            if (Time.time < _ignoreFinalUntil[lang]) return;

            if (_pendingFinals.Count == 0) _firstPendingTime = Time.time;
            _pendingFinals[lang] = result;
        }

        private void DecideAutoLanguage()
        {
            string best = null;
            float bestScore = float.MinValue;
            float[] spk = null;
            int spkFrames = 0;

            foreach (var pair in _pendingFinals)
            {
                float score = Score(pair.Value);
                if (score > bestScore) { bestScore = score; best = pair.Key; }
                if (pair.Value.spk != null && pair.Value.spk.Length > 0 && pair.Value.spk_frames > spkFrames)
                {
                    spk = pair.Value.spk;
                    spkFrames = pair.Value.spk_frames;
                }
            }

            VoskJson winner = _pendingFinals[best];
            winner.spk = spk;
            winner.spk_frames = spkFrames;

            // Si el otro idioma todavía no terminaba, su versión llegará tarde: ignorarla un momento.
            foreach (string code in new[] { Es, En })
                if (!_pendingFinals.ContainsKey(code)) _ignoreFinalUntil[code] = Time.time + 1.0f;

            _pendingFinals.Clear();
            if (bestScore > 0f) Emit(best, winner);
            else ClearPartial();
        }

        /// <summary>Confianza promedio de las palabras; 0 si no hay texto.</summary>
        private static float Score(VoskJson json)
        {
            if (json.result == null || json.result.Length == 0) return string.IsNullOrWhiteSpace(json.text) ? 0f : 0.01f;
            float sum = 0f;
            foreach (WordJson w in json.result) sum += w.conf;
            return sum / json.result.Length;
        }

        private void Emit(string lang, VoskJson result)
        {
            if (string.IsNullOrWhiteSpace(result.text)) { ClearPartial(); return; }

            var words = new List<RecognizedWord>();
            if (result.result != null && result.result.Length > 0)
                foreach (WordJson w in result.result) words.Add(new RecognizedWord(w.word, w.conf));
            else
                foreach (string w in result.text.Split(' ')) words.Add(new RecognizedWord(w, 1f));

            // Descartar lo que probablemente es ruido y no habla.
            float average = 0f;
            foreach (RecognizedWord w in words) average += w.Confidence;
            average /= words.Count;
            bool noise = average < minAverageConfidence
                || (words.Count == 1 && words[0].Confidence < minSingleWordConfidence);
            if (noise) { ClearPartial(); return; }

            FormatWords(words, lang);

            int speaker = Speakers.Identify(result.spk, result.spk_frames);
            LastLanguage = lang;
            LastSpeechTime = Time.time;
            _lastPartialShown = "";
            FinalResult?.Invoke(new TranscriptSegment(words, speaker, lang));
        }

        /// <summary>Borra el texto provisional en pantalla (cuando la frase se descartó).</summary>
        private void ClearPartial()
        {
            _lastPartialShown = "";
            PartialResult?.Invoke("");
        }

        // ---------- Formato: los modelos pequeños de Vosk entregan todo en minúsculas ----------

        private static void FormatWords(List<RecognizedWord> words, string lang)
        {
            for (int i = 0; i < words.Count; i++)
            {
                string w = words[i].Text;
                if (i == 0) w = Capitalize(w);
                if (lang == En && w == "i") w = "I";
                if (lang == En && w.StartsWith("i'")) w = "I" + w.Substring(1);
                words[i] = new RecognizedWord(w, words[i].Confidence);
            }
        }

        private static string Format(string text, string lang)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (lang == En) text = (" " + text + " ").Replace(" i ", " I ").Trim();
            return Capitalize(text);
        }

        private static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

        // ---------- Control ----------

        /// <summary>Empieza una conversación nueva: olvida hablantes y texto pendiente.</summary>
        public void ResetConversation()
        {
            Speakers.Reset();
            ResetRecognizers();
        }

        private void ResetRecognizers()
        {
            _partials[Es] = _partials[En] = "";
            _lastPartialShown = "";
            _pendingFinals.Clear();
            lock (_recognizerLock)
                foreach (VoskRecognizer r in _recognizers.Values) r.Reset();
            PartialResult?.Invoke("");
        }

        private void SetStatus(string status)
        {
            Status = status;
            StatusChanged?.Invoke(status);
        }

        private void Fail(string message)
        {
            HasError = true;
            SetStatus(message);
        }

        private void OnDestroy()
        {
            if (_mic != null) _mic.SamplesAvailable -= OnSamples;

            _cancel?.Cancel();
            _audioQueue?.CompleteAdding();
            _worker?.Join(500);

            lock (_recognizerLock)
            {
                foreach (VoskRecognizer r in _recognizers.Values) r.Dispose();
                _recognizers.Clear();
            }
            _spkModel?.Dispose();
            foreach (Model m in _models.Values) m.Dispose();
            _cancel?.Dispose();
        }
    }
}
