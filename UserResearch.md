# User Research

**Project:** Real-time captioning app for deaf and hard of hearing people (Meta Quest)
**Course:** Human Computer Interaction, Universidad Panamericana, 2026
**Team:**
- Omar Alejandro Santos Ibarra
- Alvaro David Romero Ramirez
- Gerardo Esquivel Sosa
**Research date:** November 2026

---

## 0. Main limitation (read first)

**We did not interview or observe any deaf person during this project.** The time between the proposal approval (November 13) and the deadline (November 24) did not allow us to contact an association or school and schedule sessions; no team member had direct contact with a deaf person.

This research is therefore **secondary research**. It is based on official data, published academic studies with deaf and hard of hearing (DHH) participants, accessibility guidelines and an analysis of existing apps. Every finding in this document comes from a linked source. Direct quotes belong to the authors or participants of those sources, **not to people we interviewed**. The customer journey is **built from that evidence**, not from our own observation. Section 8 describes our plan to validate the solution with deaf people.

---

## 1. Participants

### 1.1 Direct participants

None.

### 1.2 Indirect evidence gathered by the team

| Type of contact | Did we do it? | What happened and what we learned |
|---|---|---|
| Team tests with the Quest (team members trying the app with noise and several voices) | Yes | How often recognition failed with noise. State clearly that the team is **not** the target audience. |

> If a team member manages to talk with someone with hearing loss today, even by text message, it goes here as real evidence and is added to the findings marked as a "primary source".

### 1.3 Participants in the secondary sources

The academic sources we used did work with DHH people:

| Source | DHH participants | Context |
|---|---|---|
| Jain et al., ASSETS 2018 [S5] | 12 (formative study) + 10 (walking evaluation) | HoloLens captions during conversations on the move |
| Jain et al., DIS 2018 [S4] | 1 (autoethnography by the lead author, severe-to-profound deafness), 45 days, 10 sessions | HoloLens captions in meetings and lectures |
| Samaradiyakara et al., 2025 [S6] | 8 students + 2 teachers (needs analysis), 24 students (evaluation) | AR captions for deaf students in class |
| *Live Captions in Virtual Reality* [S7] | 8 | Caption movement behaviors in VR |
| *Mixed Reality Speaker Identification* (VRST 2019) [S8] | 18 | Visual cues to show who is speaking in a VR panel |
| Glasser, Kushalnagar and Kushalnagar, ASSETS 2017 [S9] | Experience report by DHH and hearing authors (no N) | Using automatic speech recognition (ASR) apps in conversation |

---

## 2. Research method

1. **Review of official data** on hearing loss worldwide (WHO) and in Mexico (INEGI, 2020 Census), to size the problem.
2. **Literature review** in HCI and accessibility: studies on real-time captions in augmented and virtual reality headsets, and on automatic speech recognition for DHH people.
3. **Review of design and accessibility guidelines:** W3C XR Accessibility User Requirements (XAUR), W3C XR captioning user needs, WCAG 2.2 (contrast and text size) and Meta's VR accessibility guidelines.
4. **Competitive analysis** of two live captioning apps: Google Live Transcribe and Ava (stated features, limitations, public reviews).
5. **Synthesis:** we grouped what we found into findings, turned them into user needs and linked each one to a design decision (section 6). We built the customer journey from the same findings (section 5).

**Inclusion criterion:** we only used data we could read in the original linked source. When a source was only available as a summary (for example, the abstract of the VRST 2019 study), we say so.

---

## 3. Problem context

- **Worldwide:** over 5% of the world's population, or **430 million people**, require rehabilitation for disabling hearing loss, and by 2050 **over 700 million people** (1 in every 10) are expected to have disabling hearing loss [S1].
- **In Mexico:** according to INEGI's 2020 Census, **20,838,108 people live with some disability**, and **21.9%** of them have difficulty hearing even when using a hearing aid [S2]. (Our calculation: about 4.5 million people.)
- **Two different groups:** the WHO defines hard of hearing people as those with mild to severe hearing loss, who usually communicate through spoken language and can benefit from hearing aids, implants and **captioning**; deaf people mostly have profound hearing loss, and some use sign language [S1]. Our app mainly serves people who read Spanish or English in real time; **it does not replace Mexican Sign Language (LSM) or an interpreter**.
- The WHO lists **captioning services** among the assistive technologies for hearing loss [S1].

---

## 4. Key findings


### F1. Group conversations are the hardest and most exhausting moment
- The deaf community calls being left out of group conversations *"Dinner Table Syndrome"*. In a testimony published by RNID, a person with hearing loss writes that they started favoring one-to-one or small group interactions, that *"You're always slightly behind, and it's incredibly draining"*, and describes *"sinking into isolation"* [S10]. (This is a personal account, not a study.)
- In Jain et al. (2018), participants missed speech during **speaker transitions** in a three-person group [S5].
- **Need:** know **who** is speaking without having to search for them.

### F2. Identifying the speaker helps, but too many speakers is confusing
- In the HoloLens autoethnography, the author used **one caption window per speaker** in group meetings [S4]. Participants in Jain et al. (2018) asked to know who was speaking and where the speaker was [S5].
- In a study with 18 DHH participants, visual cues in the headset *"made it easier to identify speakers"* [S8] (we only read the abstract).
- Ava builds its value proposition on a *"color-coded transcript of who says what"* [S12].
- Meta advises against captioning more than 4 people at once: *"captioning more than 4 people is too confusing"* [S13].
- **Need:** tell speakers apart in a simple way, with a small limit on colors.

### F3. Automatic recognition makes mistakes, especially with noise
- Glasser et al. report that speech recognition apps *"often inserted random text, especially in noisy settings"*, and cite research stating that text must be at least 85% to 90% accurate to be useful in class [S9].
- They also suggest it would be ideal if the system told the user **which word** needs to be repeated instead of the whole sentence [S9].
- **Need:** reduce the junk text caused by noise and know which words not to trust.

### F4. Marking uncertain words is useful if it does not distract
- Deaf students in Samaradiyakara et al. asked for uncertain words to be marked with minimal distraction; the authors report that markups can distract from reading, and less obtrusive ones distract less [S6].
- **Need:** signal uncertainty subtly.

### F5. Less text at a time is easier to read
- Students in the same study said *"Too many words shown at a time, it's hard to grasp the overall meaning"* and preferred at most two short lines [S6].
- Meta recommends a **maximum of 32 characters per row and 2 rows** [S13]; Jain et al. used 2 lines of 60 characters by default [S5].
- At the same time, in the autoethnography *"a single line of captioning was often not enough"* to recover from errors [S4].
- **Need:** control how many lines are shown and for how long, with a little history.

### F6. Position preferences vary; customization works best
- In VR, the preferences of 8 DHH participants on caption movement were split, and the authors conclude that *"providing opportunities for caption customization is best"* [S7].
- Several participants prefer text close to the speaker's face [S4][S6]; others get dizzy if text moves with the head [S5].
- XAUR (W3C) states the user need *"Users may need to customise captions, subtitles and other text in XR environments"* [S14]. Meta recommends starting at 1 meter and **giving people the option to move them** [S13].
- **Need:** adjust caption size, font and position.

### F7. Legibility: high contrast and a background behind the text
- WCAG 2.2 requires a minimum contrast of **4.5:1** for normal text (3:1 for large text) [S15]. Meta recommends a **background box** that separates captions from the environment and offering **at least 3 sizes (50% to 200%)** [S13].
- XAUR asks for high contrast skins that suit the luminosity of the environment [S14]. The W3C XR captioning user needs include keeping text legible in contrast with the background and letting low-vision users adjust text size [S16].
- **Need:** a high contrast mode and adjustable size.

### F8. Attention is split between the text, faces and the surroundings
- Participants in Jain et al. (2018) missed a significant part of conversations while walking and were concerned about splitting attention between their safety and the speakers [S5].
- A student in Samaradiyakara et al.: *"I prefer to focus on either captions or lips at a time"* [S6].
- Phone apps require holding the device near the speaker: Live Transcribe says *"Hold your device microphone near the person or sound that you want to capture"* [S11]. A headset avoids looking down at a phone.
- **Need:** text that does not cover faces or surroundings and does not require looking at another device.

### F9. Current tools have specific gaps
- **Live Transcribe:** runs on Android phones, shows sounds like "Laughing" or "Doorbell", and offline it only works in downloadable languages. Its help page **does not mention speaker identification** [S11].
- **Ava:** identifies speakers by color, but for that, other people join through a QR code or link from their own devices. The free plan limits sessions to 40 minutes and the higher-accuracy captions are paid. A recent review complains about being billed after the trial without warning [S12].
- **No verified data:** we did not find studies on live captions in Spanish with deaf people in Mexico. The validation plan must cover this gap.
- **Need:** an option that identifies speakers **without others installing anything**, works in Spanish and English, and is hands-free.

### Comfort and hardware finding
- The autoethnography author could use the HoloLens for only **42 minutes of continuous use on average**, and wearing the device felt socially noticeable [S4]. This also applies to the Meta Quest, and we treat it as a project constraint (section 7).

---

## 5. Customer Journey

> **Built from secondary research (section 4), not from our own observation.** Thoughts and emotions are a synthesis of what the sources report; the only direct quotes are the ones marked with their source.

**Proto-persona (hypothetical, not a participant):** an adult with severe hearing loss who reads Spanish well, in a meeting with 3 to 4 people (family, work team or class). 

### 5.1 Current journey (without the app)

| Stage | Actions | Thoughts | Emotions | Touchpoints | Friction | Findings |
|---|---|---|---|---|---|---|
| **1. Preparation** | Decides whether to go; picks a seat where faces are visible; opens a captioning app on the phone | "Will I be able to follow the conversation?" | Anticipation, anxiety | Phone, app (Live Transcribe/Ava), physical space | Ava requires others to connect; the free plan has a time limit | F1, F9 |
| **2. One-to-one conversation** | Lip-reads and checks the phone; asks people to repeat | "With one person I can manage" | Relative confidence | The other person's face, phone | Has to hold the phone near the speaker; eyes go back and forth | F8 |
| **3. Group conversation** | Tries to figure out who is speaking; gets lost when turns change | "Who said that?" | Frustration, feeling left behind ("always slightly behind" [S10]) | Several faces, transcript without speakers | No speaker identification; speech lost during speaker transitions | F1, F2 |
| **4. Noise and distant voices** | The room gets noisy; the text changes nonsensically | "Nobody said that" | Distrust in the tool | Phone microphone, transcript | Random text inserted by noise [S9]; doesn't know if the app is listening | F3 |
| **5. Error or uncertain word** | Finds a word that doesn't fit; decides whether to ask | "Do I ask again or let it go?" | Doubt, embarrassment about interrupting | Transcript, other person | Doesn't know which word is wrong; has to ask for everything to be repeated | F3, F4 |
| **6. Sustained reading** | Reads long blocks of text while trying to watch faces | "I can't keep up with all of it" | Tiredness | Phone screen | Too much text, small font or low contrast | F5, F7 |
| **7. Afterwards** | Leaves or withdraws into smaller conversations | "Next time I'd rather not go" | Exhaustion, isolation ("incredibly draining" [S10]) | — | Accumulated fatigue | F1 |

### 5.2 Journey with the app (proposed)

| Stage | What changes with the app | Design decision |
|---|---|---|
| 1. Preparation | Puts on the Quest, sets size, position and language once; nobody else installs anything | D6, D7, D9 |
| 2. One-to-one | Captions appear in front of the user without looking down at a phone | D6 |
| 3. Group | Each voice has a color (P1, P2), so the user knows who is speaking | D2 |
| 4. Noise | Distant voices are ignored; the microphone bar shows the app is listening | D1, D3 |
| 5. Uncertain word | Low-confidence words appear dimmed; the user knows what to ask about | D4 |
| 6. Reading | Few lines, high contrast and a size that fits the user | D5, D6, D7 |
| 7. Afterwards | [TO COMPLETE only with evidence from real tests; for now this is a hypothesis we cannot claim] | — |

---

## 6. How the research influenced the solution

| Finding | User need | Design decision in the app |
|---|---|---|
| F3, F8 | Know whether the app is listening and whether noise is affecting it | **D1. Microphone / volume bar** always visible with color levels. It gives feedback on system status. *Honest note: no source literally asks for a volume bar; this decision is our inference from the noise and lag problems reported in [S9].* |
| F1, F2 | Know who is speaking | **D2. Color per person (P1, P2)**, limited to a few colors to avoid clutter, in line with Meta's 4-speaker limit [S13] |
| F3 | Less junk text from background noise | **D3. Ignore distant voices** (volume threshold) |
| F3, F4 | Know which words not to trust, without distraction | **D4. Dim uncertain words** using the recognizer's confidence. We chose dimming instead of red underlines because [S6] reports that less obtrusive markups distract less |
| F5 | Little text at a time, with some history | **D5. Configurable number of lines and "hide after"** |
| F6, F8 | Fit the text to the user's eyesight and situation | **D6. Size, font and position settings** for captions |
| F7 | Read against any background | **D7. High contrast mode** and a background box behind the text, aiming for at least 4.5:1 [S15] [TO COMPLETE: measure the app's actual contrast with a checker and record the value] |
| F9 | Work in the language of the conversation | **D8. Language ES / EN / Auto** [TO COMPLETE: confirm which languages work in the submitted version] |
| F9, F8 | Not depend on others installing anything or on holding a phone | **D9. Self-contained, hands-free app on the headset** |

---

## 7. Identified constraints

- **Hardware:** we tested on a Meta Quest 2, a VR headset with passthrough, not lightweight glasses. Long use and the social visibility of the headset are a known limitation [S4].
- **Speech recognition:** the local recognition model (Vosk) makes mistakes; the app cannot guarantee the accuracy levels that the literature considers necessary for class (85% to 90%) [S9]. [TO COMPLETE: if you measured accuracy, record how and the result.]
- **It does not replace LSM** or an interpreter [S1].

---

## 8. Validation plan with deaf people

Since we could not validate with the target audience, this is the plan for the next iteration.

**Recruitment:** 5 to 8 DHH people (a mix of severe/profound hearing loss and hard of hearing), contacted through either deaf association, school or university service. Communication in writing or with an LSM interpreter; written informed consent.

**Short interview (10 min):** how they follow group conversations today, which apps they use and what bothers them about those apps. This confirms or corrects F1, F2 and F9.

**Tasks with the Quest:**
1. A 3-minute one-to-one conversation: do they understand the topic?
2. A three-person conversation: identify who said a specific sentence .
3. A conversation with background noise and a distant voice.
4. Find an uncertain word and decide what to ask.
5. Set size, position and high contrast to their liking without help.

**Metrics:** tasks completed without help, correct speaker identification, comprehension (questions about the content), errors in settings, comfortable usage time, SUS and open comments.

**Questions we most want to answer:**
- Are P1/P2 colors enough, or do they prefer names or a position next to the speaker?
- Does dimming uncertain words help or confuse?
- Does the microphone bar add value or is it visual noise?
- Do they prefer text fixed in front of their view or near the speaker's face?

The results would go into `UserTesting.md` and into a new version of this document.

---

## 9. Sources

- **[S1]** World Health Organization. *Deafness and hearing loss* (fact sheet, 3 March 2026). https://www.who.int/news-room/fact-sheets/detail/deafness-and-hearing-loss
- **[S2]** Government of Mexico. *Día Mundial de la Audición* (with data from INEGI, Censo de Población y Vivienda 2020). https://www.gob.mx/cms/uploads/attachment/file/619401/D_a_Mundial_de_la_Audici_n.pdf
- **[S3]** INEGI. *Estadísticas a propósito del Día Internacional de las Personas con Discapacidad* (press release 713/21). https://www.inegi.org.mx/contenidos/saladeprensa/aproposito/2021/EAP_PersDiscap21.docx [TO COMPLETE: we could not extract the text of this file; open it and confirm it matches S2, or delete this line]
- **[S4]** Jain, D., Chinh, B., Findlater, L., Kushalnagar, R., Froehlich, J. (2018). *Exploring Augmented Reality Approaches to Real-Time Captioning: A Preliminary Autoethnographic Study.* DIS '18 Companion. https://realitylab.uw.edu/img/research/call/Jain_HoloLensCaptions.pdf
- **[S5]** Jain, D., Franz, R., Findlater, L., Cannon, J., Kushalnagar, R., Froehlich, J. (2018). *Towards Accessible Conversations in a Mobile Context for People who are Deaf and Hard of Hearing.* ASSETS '18. https://soundability.eecs.umich.edu/img/portfolio/Jain_DHHMobileContexts_ASSETS18.pdf
- **[S6]** Samaradiyakara, Y., Pathirage, A., Ushan, T., Sasikumar, P., Karunanayaka, K., Keppitiyagama, C., Nanayakkara, S. (2025). *Tailored Real-time AR Captioning Interface for Enhancing Learning Experience of Deaf and Hard-of-Hearing (DHH) Students.* arXiv:2501.02233. https://arxiv.org/abs/2501.02233
- **[S7]** *Live Captions in Virtual Reality (VR).* Journal on Technology & Persons with Disabilities, vol. 11. https://par.nsf.gov/biblio/10471910
- **[S8]** *Mixed Reality Speaker Identification as an Accessibility Tool for Deaf and Hard of Hearing Users.* VRST '19 (abstract only). https://par.nsf.gov/biblio/10171224
- **[S9]** Glasser, A., Kushalnagar, K., Kushalnagar, R. (2017). *Deaf, Hard of Hearing, and Hearing Perspectives on using Automatic Speech Recognition in Conversation.* ASSETS '17. https://arxiv.org/abs/1909.01176
- **[S10]** RNID. Personal testimony about *"Dinner Table Syndrome"* at Christmas gatherings. https://rnid.org.uk/?p=7760
- **[S11]** Google. *Use Live Transcribe* (Android Accessibility Help). https://support.google.com/accessibility/android/answer/9158064?hl=en
- **[S12]** Transcense, Inc. *Ava: Transcribe Voice to Text* (App Store, accessed 24 November 2026). https://apps.apple.com/us/app/ava-transcribe-voice-to-text/id1030067058
- **[S13]** Meta. *Accessibility, Meta Horizon OS Developers* (updated 6 April 2026). https://developers.meta.com/vr/design/accessibility/
- **[S14]** W3C. *XR Accessibility User Requirements* (W3C Working Group Note, 2021). https://www.w3.org/TR/xaur/
- **[S15]** W3C. *Understanding SC 1.4.3: Contrast (Minimum)*, WCAG 2.2. https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html
- **[S16]** W3C Silver XR Subgroup. *XR Captioning User Needs.* https://w3c.github.io/silver/subgroups/xr/captioning/xr-captioning-user-needs.html
