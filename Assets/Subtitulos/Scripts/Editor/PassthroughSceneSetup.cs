using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.OpenXR;

namespace Subtitulos.EditorTools
{
    /// <summary>
    /// Menú "Subtítulos > Preparar escena para passthrough":
    /// convierte la escena del template de VR en una de realidad mixta, donde se ve
    /// el mundo real (como con unos lentes) y solo flotan los subtítulos.
    ///
    ///  1. Desactiva (no borra) los objetos de demostración del template.
    ///  2. Cámara con fondo transparente + ARCameraManager (passthrough de Meta Quest).
    ///  3. Agrega un AR Session y un piso invisible (para que la gravedad del XR Origin no lo haga caer).
    ///  4. Activa en OpenXR (Android) las funciones "Meta Quest: Session" y "Meta Quest: Camera (Passthrough)".
    ///
    /// Nota: en el editor de la PC el fondo se ve negro; el passthrough solo existe en el visor.
    /// </summary>
    public static class PassthroughSceneSetup
    {
        private static readonly string[] TemplateObjects =
        {
            "Environment", "Interactables", "UI", "Teleport Area Setup",
        };

        [MenuItem("Subtítulos/Preparar escena para passthrough")]
        public static void Setup()
        {
            var report = new List<string>();

            // 1) Ocultar el contenido de demostración del template.
            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (!TemplateObjects.Contains(root.name) || !root.activeSelf) continue;
                Undo.RecordObject(root, "Ocultar objetos del template");
                root.SetActive(false);
                report.Add($"Desactivado: {root.name}");
            }

            // 2) Cámara transparente con passthrough.
            Camera camera = Camera.main;
            if (camera == null)
            {
                Debug.LogError("[Subtítulos] No se encontró la cámara principal (tag MainCamera) dentro del XR Origin.");
                return;
            }
            Undo.RecordObject(camera, "Cámara passthrough");
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            if (camera.GetComponent<ARCameraManager>() == null)
            {
                Undo.AddComponent<ARCameraManager>(camera.gameObject);
                report.Add("ARCameraManager agregado a la cámara");
            }

            // 3) AR Session.
            if (Object.FindAnyObjectByType<ARSession>() == null)
            {
                var session = new GameObject("AR Session", typeof(ARSession));
                Undo.RegisterCreatedObjectUndo(session, "AR Session");
                report.Add("AR Session creado");
            }

            // 3b) Piso invisible. El XR Origin del template tiene gravedad: sin el piso del
            //     Environment, la cámara caería infinitamente (y se vería todo negro).
            if (GameObject.Find("Piso invisible") == null)
            {
                var floor = new GameObject("Piso invisible", typeof(BoxCollider));
                floor.transform.position = new Vector3(0f, -0.05f, 0f);
                floor.GetComponent<BoxCollider>().size = new Vector3(100f, 0.1f, 100f);
                Undo.RegisterCreatedObjectUndo(floor, "Piso invisible");
                report.Add("Piso invisible creado (solo colisión, no se ve)");
            }

            // 4) Funciones de OpenXR para Meta Quest.
            OpenXRSettings xr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (xr != null)
            {
                foreach (var feature in xr.GetFeatures())
                {
                    string type = feature.GetType().FullName ?? "";
                    bool isMeta = type.Contains(".Meta.");
                    bool wanted = type.EndsWith(".ARSessionFeature") || type.EndsWith(".ARCameraFeature");
                    if (!isMeta || !wanted || feature.enabled) continue;
                    feature.enabled = true;
                    report.Add($"OpenXR activado: {feature.name}");
                }
                EditorUtility.SetDirty(xr);
                AssetDatabase.SaveAssets();
            }
            else
            {
                report.Add("No se encontraron ajustes de OpenXR para Android (revisa XR Plug-in Management).");
            }

            EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
            Debug.Log("[Subtítulos] Escena lista para passthrough:\n• " + string.Join("\n• ", report));
        }
    }
}
