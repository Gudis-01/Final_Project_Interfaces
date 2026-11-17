using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Subtitulos.Speech
{
    /// <summary>
    /// Deja un modelo de Vosk listo en una carpeta real del disco.
    ///
    /// Vosk necesita una ruta de carpeta normal. En Android/Quest, StreamingAssets vive
    /// dentro del APK (comprimido), así que el modelo se guarda como .zip en
    /// StreamingAssets y aquí se extrae una sola vez a Application.persistentDataPath.
    ///
    /// En el editor/PC también acepta el modelo descomprimido directamente en StreamingAssets.
    /// </summary>
    public static class VoskModelLoader
    {
        /// <summary>
        /// Prepara el modelo y llama a onReady con la ruta lista (o onError con el motivo).
        /// </summary>
        /// <param name="modelName">Nombre del modelo sin extensión, p. ej. "vosk-model-small-es-0.42".</param>
        public static IEnumerator Prepare(string modelName, Action<string> onReady, Action<string> onError)
        {
            // 1) Modelo ya descomprimido en StreamingAssets (solo funciona fuera de Android).
            string unzippedInStreaming = Path.Combine(Application.streamingAssetsPath, modelName);
            if (Application.platform != RuntimePlatform.Android && Directory.Exists(unzippedInStreaming))
            {
                onReady?.Invoke(FindModelRoot(unzippedInStreaming));
                yield break;
            }

            // 2) Ya se extrajo en una ejecución anterior.
            string targetDir = Path.Combine(Application.persistentDataPath, modelName);
            string marker = Path.Combine(targetDir, ".extracted");
            if (File.Exists(marker))
            {
                onReady?.Invoke(FindModelRoot(targetDir));
                yield break;
            }

            // 3) Leer el .zip desde StreamingAssets (UnityWebRequest funciona dentro del APK).
            string zipPath = Path.Combine(Application.streamingAssetsPath, modelName + ".zip");
            string zipUrl = zipPath.Contains("://") ? zipPath : "file://" + zipPath;

            byte[] zipBytes;
            using (UnityWebRequest request = UnityWebRequest.Get(zipUrl))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"No se encontró el modelo '{modelName}' en StreamingAssets ({request.error}).");
                    yield break;
                }
                zipBytes = request.downloadHandler.data;
            }

            // 4) Extraer en un hilo aparte para no congelar la app.
            Task extraction = Task.Run(() => ExtractZip(zipBytes, targetDir, marker));
            while (!extraction.IsCompleted) yield return null;

            if (extraction.IsFaulted)
            {
                onError?.Invoke("Error al extraer el modelo: " + extraction.Exception?.GetBaseException().Message);
                yield break;
            }

            onReady?.Invoke(FindModelRoot(targetDir));
        }

        private static void ExtractZip(byte[] zipBytes, string targetDir, string marker)
        {
            if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
            Directory.CreateDirectory(targetDir);

            using (var stream = new MemoryStream(zipBytes))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string destination = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));
                    // Evita que una entrada del zip escriba fuera de la carpeta destino.
                    if (!destination.StartsWith(Path.GetFullPath(targetDir), StringComparison.Ordinal)) continue;

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(destination);
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    entry.ExtractToFile(destination, true);
                }
            }

            // Marca de "ya extraído" para no repetir en cada arranque.
            File.WriteAllText(marker, DateTime.UtcNow.ToString("o"));
        }

        /// <summary>
        /// Los zips oficiales traen una carpeta adentro (modelo/modelo/...). Baja hasta
        /// la carpeta que tiene los archivos reales del modelo.
        /// </summary>
        private static string FindModelRoot(string dir)
        {
            string current = dir;
            for (int i = 0; i < 3; i++)
            {
                string[] files = Directory.GetFiles(current);
                string[] dirs = Directory.GetDirectories(current);
                bool onlyMarker = files.Length == 0 || (files.Length == 1 && Path.GetFileName(files[0]) == ".extracted");
                if (onlyMarker && dirs.Length == 1) current = dirs[0];
                else break;
            }
            return current;
        }
    }
}
