using System;
using System.Collections.Generic;

namespace Subtitulos.Speech
{
    /// <summary>
    /// Agrupa frases por "huella de voz" para decidir quién está hablando.
    ///
    /// Vosk, con el modelo de hablantes (vosk-model-spk), entrega un vector (x-vector)
    /// por cada frase. Frases de la misma persona dan vectores parecidos. Aquí comparamos
    /// cada vector nuevo con el promedio de cada hablante conocido (similitud coseno):
    /// si se parece lo suficiente es esa persona; si no, es una persona nueva.
    ///
    /// Es una clase normal (no MonoBehaviour) para poder probarla sin Unity.
    /// </summary>
    public class SpeakerTracker
    {
        /// <summary>Qué tan parecidas deben ser dos voces para considerarlas la misma (0..1).</summary>
        public float SimilarityThreshold { get; set; }

        /// <summary>Máximo de hablantes distintos. Al llegar al límite se asigna el más parecido.</summary>
        public int MaxSpeakers { get; set; }

        /// <summary>Frases con menos frames que esto son muy cortas para identificar la voz.</summary>
        public int MinFrames { get; set; }

        public const int Unknown = -1;

        private readonly List<float[]> _centroids = new List<float[]>();
        private readonly List<int> _counts = new List<int>();

        public int SpeakerCount => _centroids.Count;

        public SpeakerTracker(float similarityThreshold = 0.55f, int maxSpeakers = 6, int minFrames = 30)
        {
            SimilarityThreshold = similarityThreshold;
            MaxSpeakers = Math.Max(1, maxSpeakers);
            MinFrames = minFrames;
        }

        /// <summary>
        /// Regresa el índice del hablante (0, 1, 2...) o Unknown si la frase no trae
        /// suficiente información.
        /// </summary>
        public int Identify(float[] xvector, int frames)
        {
            if (xvector == null || xvector.Length == 0 || frames < MinFrames) return Unknown;

            int best = Unknown;
            float bestSimilarity = float.MinValue;
            for (int i = 0; i < _centroids.Count; i++)
            {
                if (_centroids[i].Length != xvector.Length) continue;
                float similarity = CosineSimilarity(_centroids[i], xvector);
                if (similarity > bestSimilarity)
                {
                    bestSimilarity = similarity;
                    best = i;
                }
            }

            bool isNewSpeaker = best == Unknown
                || (bestSimilarity < SimilarityThreshold && _centroids.Count < MaxSpeakers);

            if (isNewSpeaker)
            {
                _centroids.Add((float[])xvector.Clone());
                _counts.Add(1);
                return _centroids.Count - 1;
            }

            UpdateCentroid(best, xvector);
            return best;
        }

        /// <summary>Olvida a todos los hablantes (p. ej. al cambiar de conversación).</summary>
        public void Reset()
        {
            _centroids.Clear();
            _counts.Clear();
        }

        /// <summary>Promedio acumulado: el perfil de la voz mejora con cada frase.</summary>
        private void UpdateCentroid(int index, float[] xvector)
        {
            float[] centroid = _centroids[index];
            int n = _counts[index] + 1;
            for (int i = 0; i < centroid.Length; i++)
                centroid[i] += (xvector[i] - centroid[i]) / n;
            _counts[index] = n;
        }

        public static float CosineSimilarity(float[] a, float[] b)
        {
            double dot = 0, normA = 0, normB = 0;
            int length = Math.Min(a.Length, b.Length);
            for (int i = 0; i < length; i++)
            {
                dot += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }
            if (normA <= 0 || normB <= 0) return 0f;
            return (float)(dot / (Math.Sqrt(normA) * Math.Sqrt(normB)));
        }
    }
}
