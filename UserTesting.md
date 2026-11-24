# User Testing

## 1. Participants

| ID | Profile | Hearing status |
|---|---|---|
| T1 | Person outside the class | Hearing |
| T2 | Person outside the class | Hearing |

Setup: Meta Quest 2.

None of the participants were deaf or hard of hearing. This is a limitation of our testing: the feedback reflects usability and transcription quality with hearing users, and testing with the target audience is a next step.

## 2. Tasks

| # | Task | Success criterion |
|---|---|---|
| 1 | Launch the app and start captions | Captions visible without help |
| 2 | Hold a ~5 minute conversation and follow it using the captions | Conversation completed, content understood |

## 3. Results, round 1

| Participant | Session | Completed | Errors | Notes |
|---|---|---|---|---|
| T1 | ~5 min conversation | Yes | Not recorded | Positive feedback (see section 5) |
| T2 | ~5 min conversation | Yes | ~4 | Transcription issues (see section 5) |

Both participants completed the main flow. Completion: 2/2.

Errors were counted approximately during the sessions, not with a formal log. This is a limitation, and a more rigorous count is a next step.

### Measured performance (approximate)

| Metric | Result |
|---|---|
| App start-up time (Unity build, first launch) | ~3 minutes |
| Time to detect that someone is speaking | ~1 to 2 seconds |
| Time from detection to first text on screen | ~1 second |
| Microphone | Active from the first second |

Once the app is running, captions respond quickly. The main performance problem is the long start-up time.

## 4. Results, round 2 (after changes)

After the changes described in section 6, the team tested the app again.

| Aspect | Round 1 | Round 2 |
|---|---|---|
| Detecting voices | Difficulty with voices, unwanted audio picked up | Fewer problems detecting voices |
| Fluidity of captions | Acceptable | Smoother |
| Time to show text | ~1 second after detection | Slightly faster |

The improvement was noticeable but modest. We did not take formal measurements in round 2, so these results are qualitative observations, and a timed comparison is a next step.

## 5. Observations and quotes

- T1: "Esta padrísimo, se ve bien y detecta bien las voces, me gusta el detector de ruido."
  (EN: "It's great, it looks good and detects voices well. I like the noise detector.")
- T2: the app picks up unwanted audio when the environment is noisy.
- T2: when it cannot understand speech, it sometimes invents words.
- T2: the model we use makes some transcription errors.

## 6. Problems found and changes made

| Problem | Evidence | Change | Done |
|---|---|---|---|
| Difficulty detecting voices, unwanted audio in noisy rooms | T2 testing (round 1) | Adjusted the voice detection threshold and the volume handling | ☑ |
| Slow processing | Team testing on the Quest | Optimized the audio and recognition pipeline | ☑ |
| Large model too slow for real time | Team testing on the Quest | Switched to the small Vosk model | ☑ |
| Invents words it cannot understand | T2 testing | Not yet addressed; next step: hide low-confidence words | ☐ |
| ~3 minute start-up with no feedback | Team testing on the Quest | Not yet addressed; next step: loading screen with status message | ☑ |

Only items marked ☑ are implemented in the repo. The rest are next steps.

## 7. Known technical findings (from team testing on the Quest)

- The large Vosk model (`lgraph`) was too slow for real-time captions; we kept the small model, which trades some accuracy for speed.
- The app currently transcribes English, Spanish and automatic language detection.
- Transcription errors remain, especially in noisy environments.
