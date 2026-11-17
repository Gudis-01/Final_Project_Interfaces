using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Subtitulos.EditorTools
{
    /// <summary>
    /// Si existe el archivo "Assets/Subtitulos/rebuild.flag", al terminar de compilar se
    /// rehace la interfaz y se prepara el passthrough automáticamente, y se borra el archivo.
    /// Útil para regenerar la escena sin tocar menús (p. ej. después de actualizar scripts).
    /// </summary>
    [InitializeOnLoad]
    internal static class RebuildTrigger
    {
        private const string FlagPath = "Assets/Subtitulos/rebuild.flag";

        static RebuildTrigger()
        {
            if (!File.Exists(FlagPath)) return;
            EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            if (!File.Exists(FlagPath) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            AssetDatabase.DeleteAsset(FlagPath);

            SubtitleSceneBuilder.Build();
            PassthroughSceneSetup.Setup();
            EditorSceneManager.SaveOpenScenes();
        }
    }
}
