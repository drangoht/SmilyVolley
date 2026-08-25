# Smily Volley — itch.io store page (English)

> Ready-to-paste English copy. **Strictly factual**: everything written here exists in the build
> (sources: `README.md`, `docs/GDD.md`). Keep in sync with the French version
> (`ITCH_STORE_PAGE.md`) — correcting only one of the two leaves the other lying.
>
> Page: <https://drangoht.itch.io/smily-volley> — **web only**, pushed to the `html5` channel by
> `tools/release_itch.ps1`.
>
> **Up to date for 1.1.0** (2026-08-25), the touch release.

---

## 1. Title and tagline

**Title:** Smily Volley

**Tagline (short text under the title):**
> Beach volley for smiling blobs. Three controls, one net, and all the depth in *where* the ball
> hits you.

---

## 2. Short description (itch listings, 1–2 sentences)

> Beach volleyball with two blobs, in the lineage of *Blobby Volley*. Three controls per player —
> two on one keyboard, two on one screen with your fingers, or one against the computer. Plays in
> the browser, on desktop and on phones.

---

## 3. Long description (page body)

### The pitch

Two blobs, one net, one ball. Each side is a smiling half-circle that can do exactly three things:
go left, go right, jump. The whole game lives in the fourth one, which has no button:
**where the ball hits you**.

The hit is not a physics bounce. The ball leaves **radially from the centre of the blob**: touching
it with your side is how you aim, dropping onto it is how you spike. You learn to place your blob,
not to press faster.

### What's in a match

- **Two players on one keyboard** — or **on one screen with your fingers**, each holding their own
  edge — or **against the computer**: five levels, from Easygoing to Relentless.
- **Placement gives direction, momentum gives speed.** A still blob returns at the floor value; a
  blob falling onto the ball adds its own drop — the spike comes out at 20 units/s where a calm
  rally sits around 5. A fast ball loses half of its excess on every hit and settles back down on
  its own within three exchanges, so the rally never runs away.
- **Jelly blobs**: they squash on landing, stretch on take-off and wobble on impact — three styles
  to choose from, Firm, Soft or Moulded.
- **Match to 15 points**, two clear. Everything is adjustable: points to win, the two-point lead,
  touches per side, rally-point or classic side-out scoring, and which side serves next.
- **All six keys rebind**, one by one, and are remembered between sessions.
- A soundtrack that follows the screen: one loop for the menu, another for the match.

### Playing with your fingers

The web version plays on phones and tablets, **for one player and for two**, holding the device
**in landscape**. Controls appear on screen the moment a finger touches it — nothing shows up on a
desktop machine.

Each player gets **three buttons**: a single-piece left/right pad and a jump button. The layout
follows how many hands are actually free:

- **Two players** — each holds their own edge of the screen with their own three buttons.
- **Against the computer** — the controls spread out to both ends: movement under the left thumb,
  jump under the right, since the second hand is free.

The pad is **one continuous piece**, not two buttons with a gap: the border is in the middle, and
sliding from one side to the other without lifting your thumb changes direction. There is a **pause
button** in the top-right corner — on a phone it is the only way into the menu, so it is also the
only way to replay, switch opponent or open the options. Held in portrait, a panel asks you to turn
the device: the court is wider than it is tall, and it does not fit in the other direction.

### What this isn't

No progression, no unlocks, no online leaderboard. A match runs three to six minutes; you replay
because the last point was close.

---

## 4. Controls

| Action | Player 1 (AZERTY) | Player 1 (QWERTY) | Player 2 |
|---|---|---|---|
| Move | `Q` / `D` | `A` / `D` | `←` / `→` |
| Jump | `Z` or `Space` | `W` or `Space` | `↑` |

`Tab` switches between "against the computer" and "two players". `R` restarts the match.
`Esc` opens the pause menu. Every key rebinds in the options.

**On a touchscreen**, hold the device in landscape: each player gets a left/right pad and a jump
button on their own side, plus a pause button in the top-right corner.

---

## 5. Tech sheet (fields to set on the page)

| itch field | Value |
|---|---|
| Kind of project | **HTML** — otherwise the build downloads instead of playing |
| Release status | Released |
| Pricing | Free (No payments) |
| Uploads | web folder pushed by butler to the `html5` channel, "play in browser" ticked |
| Viewport | **1280 × 720**, 16/9 frame over a beach backdrop; on mobile the page goes fullscreen |
| Fullscreen button | enabled |
| Mobile friendly | **yes** — landscape only, on-screen touch controls, 1 and 2 players |
| Genre | Sports |
| Made with | Unity |
| Average session | A few minutes |
| Languages | French |
| Inputs | Keyboard, Touchscreen |
| Accessibility | Configurable controls |

> ⚠ **Languages stays French**: the game's own interface is in French. This page is English; the
> build is not. Claiming otherwise on the store page would be the kind of small lie a store page
> tells to every visitor.

---

## 6. Tags

`volleyball` · `sports` · `local-multiplayer` · `arcade` · `2d` · `physics` · `singleplayer` ·
`unity` · `mobile` · `touchscreen`

---

## 7. Devlog — version 1.1.0

**Title:** Now playable with your fingers — two players on one phone

**Body:**

Smily Volley now plays on a touchscreen, **for one player and for two**, right in the browser.
Hold your phone in landscape and the controls appear the moment you touch the screen.

**Two players, one phone.** Each player holds their own edge of the screen with their own three
buttons: a left/right pad and a jump button. Against the computer, the controls spread out to both
ends instead — movement under the left thumb, jump under the right — because the second hand is
free. The layout follows how many hands are actually available, not the other way round.

**The pad is one continuous piece.** Two buttons with a gap between them force your thumb to aim,
and that gap is exactly where the finger lands when you change direction mid-run. A single pad has
no hole in it: the border is in the middle, and sliding from one side to the other without lifting
your thumb changes direction.

**The button you press is bigger than the button you see.** A finger covers what it touches, so you
aim at the button you saw half a second ago, not the one in front of you. Each control's live area
extends past its drawing — but never towards its neighbour, only towards the edge of the screen.
Two areas growing towards each other overlap, and then the player aiming for "right" jumps instead.

**Esc doesn't exist on a phone.** Without the pause button in the top-right corner, a match could
not be interrupted or left at all, and "replay" would have been out of reach entirely.

**Some of the text was correct and wrong.** The on-screen hint named keys a phone player doesn't
have — and it sat across the very strip where the buttons now live. "Same keyboard", "Press R to
replay" and the menu footer all pointed at gestures that aren't there. Even "Quit" had to go: in a
browser, a tab cannot close itself, so the line was there, it was selectable, and nothing happened.

**And half the port isn't in the engine at all.** Nothing a game engine does can stop a double-tap
from zooming the page, a swipe from scrolling it, an edge swipe from going back a page — and a
direction pad sits right against that edge — or the address bar from covering the bottom of the
screen, which is to say the controls. None of it raises an error. All of it is handled in the host
page now, along with fullscreen on first touch.

Two things were only found by actually playing: the "turn your device" panel was measured against a
landscape reference, which made its title eighteen pixels tall on the very screen whose orientation
it was there to fix; and the direction pad was twice as tall as a blob, hiding the character you
were moving at the exact moment you moved it.

**One honest limitation.** A blob can run all the way to its wall, which means underneath its own
pad — and a thumb is opaque. In a game whose action lives at ground level, that overlap is the
price of playing with fingers. The buttons are very translucent and the pad was shortened after
playtesting, but it doesn't disappear.

The desktop version is unchanged: no touch control appears unless a finger touches the screen.
