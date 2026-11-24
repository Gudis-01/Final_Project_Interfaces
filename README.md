# Subtitles for the Deaf — Real-Time Captions in Meta Quest

> HCI 2 · Final Project · Technology for Social Impact
> Team: Omar Alejandro Santos Ibarra · Alvaro David Romero Ramirez · Gerardo Esquivel Sosa

## The problem

People who are deaf or hard of hearing are left out of everyday spoken conversation. Group talks, class discussions and ambient sounds, someone calling your name are easy to miss, and lip-reading or asking people to repeat themselves is tiring and socially costly.

## Target users

Deaf and hard-of-hearing persons who want to follow live conversations hands-free and without constantly looking at a phone.

## Our solution

A mixed-reality captioning app built in **Unity** for **Meta Quest**, designed with **Oculus Quest 2** as the intended final device. It listens to the environment and shows captions in your field of view.

Main features:

- **Real-time subtitles** with a **volume bar** that shows how loud the environment is.
- **Speaker separation**: each speaker gets a color or bubble so you can tell who is talking without filling the view with text.
- **Visual alerts for ambient sounds** louder sounds.
- **Accessibility settings**: text size, font, contrast and caption position.
- A clean, minimalist interface in the spirit of Apple's design clarity, without copying it.

Speech recognition runs on-device with **Vosk** (small English model). Currently the app transcribes **English, Spanish, and Automatic language detection**.

## Main flow

1. Launch the app and put on the headset (passthrough shows the real world).
2. Grant microphone permission.
3. Captions start appearing in real time with the volume bar.
4. Open the settings panel and adjust language, size, font, contrast and position.
5. Keep talking: captions update, speakers are color-coded, alerts appear for ambient sounds.

## Tech stack

| Part | Tool |
|---|---|
| Engine | Unity (project: `Proyecto_Final_Interfaces`) |
| Speech-to-text | Vosk (small English model; the large `lgraph` model was discarded for being too slow) |
| Test device | Meta Quest 2 (black-and-white passthrough) |
| Prototype | Figma |

## How to run

**Option A: Play the build (Quest)**
1. Enable Developer Mode on your Quest.
2. Install the APK from the release: `adb install -r lifetitles.apk` (or use SideQuest).
3. Open **Unknown Sources** in your library and launch the app.
4. Accept the microphone permission.

**Option B: Run from Unity**
1. Clone: `git clone https://github.com/Gudis-01/Final_Project_Interfaces.git`
2. Open the folder with Unity Hub using the Unity version in `ProjectSettings/ProjectVersion.txt`.
3. Make sure the Vosk model folder is in `Assets/StreamingAssets/` (see the repo for the exact name).
4. Switch the build platform to Android, connect the Quest by USB, and **Build and Run**.

## Documentation

- [DesignJustification.md](DesignJustification.md)
- [UserResearch.md](UserResearch.md)
- [UserTesting.md](UserTesting.md)
- [AIUsage.md](AIUsage.md)

## Links

- Figma prototype: `[add link]`
- Commercial: `[add link]`

## Team contributions

| Member | Contribution |
|---|---|
| Alvaro David Romero Ramirez | `Main coding` |
| Omar Alejandro Santos Ibarra | `Advertisement and prototype` |
| Gerardo Esquivel Sosa | `Testing` |

## Known limitations

- Sometimes the transcription get mistakes.
- small Vosk model trades accuracy for speed.
- Quest 2 passthrough is black and white; glasses are the intended final form.
