# Design Justification

Each decision below is tied to a user need and to an HCI concept from class. (The screeshots are from figma)

## 1. Minimalist captions with limited text on screen
**Decision:** Short caption lines, a few lines at most, anchored in a fixed region of the view.
**Why:** Users need to read while still looking at the people around them.
**HCI concept — Cognitive load / Hick's and Miller's ideas of limiting information:** less simultaneous text reduces working-memory demand and keeps attention on the conversation.
`[screenshot]`

## 2. Color per speaker, without saturating the view
**Decision:** Each speaker gets a color or bubble; the number of simultaneous captions is capped when many people talk.
**Why:** Distinguishing who speaks is the hardest part of group conversations for deaf users, but too many colored blocks make the view unreadable.
**HCI concept — Perception (Gestalt: similarity and proximity, pre-attentive color):** color groups text by speaker at a glance.
**Accessibility note:** color is never the only cue; speaker bubbles/labels back it up (color-blind users).
`[screenshot]`

## 3. Volume bar
**Decision:** A bar next to the captions shows how loud the environment is.
**Why:** Gives users a sense of whether the system is hearing something and how intense the sound is.
**HCI concept — Feedback / visibility of system status (Nielsen):** the user always sees that the microphone is active and working.
`[screenshot]`

## 4. Visual alerts for ambient sounds
**Decision:** Non-speech sounds trigger a distinct icon/label, different from caption text.
**Why:** Important sounds carry safety and social meaning.
**HCI concept — Signifiers and redundancy of channels:** information normally delivered by audio is delivered visually.
`[screenshot]`

## 5. Personalization: size, font, contrast, position
**Decision:** A settings panel with clear controls and immediate preview.
**Why:** Visual needs vary by person and by environment (bright room vs. dark room).
**HCI concept — Flexibility and efficiency of use; accessibility (WCAG contrast):** users adapt the interface instead of adapting themselves.
`[screenshot]`

## 6. Consistent, Apple-inspired clarity
**Decision:** Rounded panels, generous spacing, one type family, consistent icons and states.
**Why:** The interface must disappear and let the conversation stay central.
**HCI concept — Consistency and standards; aesthetic and minimalist design (Nielsen):** the same controls behave the same everywhere.

## 7. Platform-specific choices (XR)
- **Comfort:** captions are placed in the comfortable gaze zone, slightly below eye line, and follow the head with gentle smoothing to avoid motion discomfort.
- **Input:** controller pointer for settings now; hand tracking/voice is the planned input for glasses.
- **Safety:** passthrough keeps the real world visible; captions never block the full field of view.
- **Constraints:** Quest 2 hardware led us to the small Vosk model for speed over accuracy.

## 8. Error prevention and recovery
- A visible state if the microphone is unavailable or no speech is detected.
- Settings can be reset to defaults.

## Concept summary

| # | Concept | Type | Where it appears |
|---|---|---|---|
| 1 | Cognitive load | Human factor (cognition) | Decision 1 |
| 2 | Gestalt / pre-attentive perception | Human factor (perception) | Decision 2 |
| 3 | Feedback / system status | Interaction principle | Decision 3 |
| 4 | Redundancy / signifiers | Interaction principle | Decision 4 |
| 5 | Flexibility, accessibility | Interaction principle | Decision 5 |
| 6 | Consistency | Interaction principle | Decision 6 |

> Before submitting: confirm each decision matches what is actually in the build, and delete any that is not implemented.
