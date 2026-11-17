# Subtítulos en tiempo real — instalación de la base

Esta carpeta trae la base de la app: micrófono → Vosk (sin internet) → subtítulos por persona,
barra de volumen, alertas de sonidos fuertes y ajustes de accesibilidad.

## 1. Plugin de Vosk (librería nativa + clases de C#)

Los scripts usan las clases `Model`, `VoskRecognizer` y `SpkModel` del namespace `Vosk`.
Unity marcará errores de compilación hasta que el plugin esté en el proyecto.

1. Descarguen el proyecto de ejemplo oficial: https://github.com/alphacep/vosk-unity-asr
   (botón verde **Code → Download ZIP**).
2. Dentro del ZIP, busquen la carpeta que contiene estos archivos y cópienla a `Assets/` de su proyecto:
   - `libvosk.dll` (Windows, para probar en el editor)
   - `libvosk.so` dentro de una carpeta `arm64-v8a` (Android / Quest)
   - Los scripts del binding: `Model.cs`, `VoskRecognizer.cs`, `SpkModel.cs`, `Vosk.cs`, `VoskPINVOKE.cs`
     (pueden venir como scripts o como un `Vosk.dll` administrado).
3. Seleccionen `libvosk.so` en Unity y verifiquen en el Inspector: plataforma **Android**, CPU **ARM64**.
   Seleccionen `libvosk.dll`: plataforma **Editor** y **Standalone Windows x86_64**.

No necesitan la escena demo del ejemplo; si trae su propia carpeta `StreamingAssets` con un modelo
en ruso/inglés, no lo copien (pesa y no se usa).

## 2. Modelos (van en `Assets/StreamingAssets/`)

Descarguen de https://alphacephei.com/vosk/models y pongan los **.zip tal cual** (sin descomprimir)
en `Assets/StreamingAssets/`:

| Archivo | Para qué | Tamaño aprox. |
|---|---|---|
| `vosk-model-small-es-0.42.zip` | Reconocimiento en español | ~40 MB |
| `vosk-model-spk-0.4.zip` | Identificar quién habla (colores por persona) | ~13 MB |

Si cambian de modelo, cambien el nombre en el componente **VoskTranscriber** (campo *Language Model*).
Si no ponen el modelo de hablantes, la app funciona igual pero todo sale en un solo color.

## 3. Crear la escena

1. Abran su escena del template de VR (la que ya trae **XR Origin** y **EventSystem**).
2. Menú **Subtítulos → Crear sistema en la escena**.
3. Se crean dos objetos:
   - `SubtitleSystem`: micrófono, Vosk, detector de sonidos y ajustes.
   - `SubtitleCanvas`: el panel que sigue tu vista (alerta, subtítulos, volumen, botón de ajustes).
4. Den Play (el micrófono de la PC funciona en el editor).

## 3b. Realidad mixta (passthrough)

Menú **Subtítulos → Preparar escena para passthrough**: oculta los objetos de demostración del
template (no los borra), pone la cámara transparente, agrega AR Session y activa en OpenXR
(Android) *Meta Quest: Session* y *Meta Quest: Camera (Passthrough)*.
En el editor de la PC el fondo se ve negro; el mundo real solo se ve en el visor.

## 4. Build para Quest

- **Player Settings → Android**: Scripting Backend **IL2CPP**, Target Architectures **ARM64**.
- El permiso de micrófono se pide solo al abrir la app.
- La primera vez que abre, extrae los modelos (unos segundos). Las siguientes ya no.

## Cómo está organizado

| Script | Qué hace |
|---|---|
| `Audio/MicrophoneCapture` | Graba del micrófono, calcula el volumen (dB) y reparte el audio. |
| `Speech/VoskModelLoader` | Extrae los modelos del .zip a una carpeta real (necesario en Android). |
| `Speech/VoskTranscriber` | Corre Vosk en un hilo aparte; emite texto parcial y frases terminadas. |
| `Speech/SpeakerTracker` | Agrupa frases por huella de voz para asignar P1, P2, P3... |
| `Audio/LoudSoundDetector` | Detecta sonidos fuertes que no son voz (golpe corto o sonido continuo). |
| `UI/SubtitleFeed` | Burbujas por persona; une frases del mismo turno, limita cuántas se ven, las desvanece y filtra voces lejanas. |
| `UI/VolumeBar` | Barra de volumen con texto (Silencio / Normal / Alto). |
| `UI/SoundAlertBanner` | Aviso parpadeante cuando hay un sonido fuerte. |
| `UI/AccessibilitySettings` | Tamaño, fuente, contraste, posición, colores, líneas, auto-ocultar y voces lejanas; se guarda entre sesiones. |
| `UI/SettingsPanel` | Conecta los botones del panel de ajustes. |
| `UI/HeadFollowPanel` | Mantiene el panel frente a la vista sin que tiemble (se pausa con los ajustes abiertos). |
| `Editor/SubtitleSceneBuilder` | El menú que arma (o rehace) la interfaz. |
| `Editor/PassthroughSceneSetup` | El menú que deja la escena lista para realidad mixta. |

## Limitaciones conocidas (para README / pruebas)

- **Identificar hablantes** es aproximado: falla con frases muy cortas y voces parecidas.
  Ajusten *Speaker Similarity* en VoskTranscriber según lo que vean en las pruebas.
- **Alertas** no distinguen qué sonido es (timbre vs. alarma); solo "golpe" vs. "sonido continuo".
- El micrófono del visor capta mejor a quien está enfrente; con ruido fuerte la precisión baja.
- **Ignorar voces lejanas** usa solo el volumen: alguien que habla bajito cerca también se filtra.
  El umbral (*Near Voice Db* en SubtitleFeed) hay que ajustarlo con las pruebas.
