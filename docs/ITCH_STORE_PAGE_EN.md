# Smily Volley — itch.io store page (English)

> Ready-to-paste English copy. **Strictly factual**: everything written here exists in the build
> (sources: `README.md`, `docs/GDD.md`). Keep in sync with the French version
> (`ITCH_STORE_PAGE.md`) — correcting only one of the two leaves the other lying.
>
> Page: <https://drangoht.itch.io/smily-volley> — **web only**, pushed to the `html5` channel by
> `tools/release_itch.ps1`.
>
> **Up to date for 1.3.0** (2026-08-26), the touch-ergonomics pass.
>
> ✅ **PUBLISHED**: 1.3.0 is live on the `html5` channel since 2026-08-26 (build #1918157, from
> #1917589), the page description carries the touch text of § 3 and § 4, and the § 7 devlog was
> posted as
> [« Three fixes that all come from the same mistake »](https://drangoht.itch.io/smily-volley/devlog/1641600/three-fixes-that-all-come-from-the-same-mistake)
> (type *General Update*, 1.3.0 build attached).
>
> 1.2.0 (build #1917589) and its devlog
> [« The pad is gone »](https://drangoht.itch.io/smily-volley/devlog/1641338/the-pad-is-gone-your-blob-follows-your-finger-now)
> went out the same day.
>
> The English copy has been live on the page since 2026-08-25 (build #1915521). Tagline, tags,
> embed options and the whole Classification tab were set from this file then, and the 1.1.0
> devlog was posted as
> [« Now playable with your fingers »](https://drangoht.itch.io/smily-volley/devlog/1640647/now-playable-with-your-fingers-two-players-on-one-phone).
>
> ⚠ The page description is a **condensed** version of § 3 and § 4, not a copy: itch shows it above
> the fold, and the full sections below are the reference the condensation is made from.

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

- **Two players on one keyboard** — or **on one screen with your fingers**, each with their own
  half — or **against the computer**: five levels, from Easygoing to Relentless.
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

**Your blob follows your finger.** Each player slides anywhere in their own half of the screen:
the spot you touch is the spot on the court your blob runs to. Nothing to aim, nothing to meter —
your finger points at the place you want, and no pad sits along the bottom of the screen, which is
exactly where the blobs live. Jump keeps its own button, and there is a **pause button** in the
top-right corner: with no Esc key on a phone it is the only way into the menu, and so the only way
to replay, switch opponent or open the options.

**Slide low — it works, and it is the point.** Only the *horizontal* position of your finger is
read, so you can drive from right down at sand level, well away from the blobs and the ball you are
watching. Without that, your own hand sits over the game.

**Pick your side against the computer** (Options → Player's side). The half of the screen you slide
in and the half of the court your blob runs in are the same half — so choosing your side puts the
precise job under whichever hand you prefer, and moves the jump button to the opposite edge.

A finger belongs to the half it landed in, even when it crosses the middle of the screen — running
towards the net never takes over your opponent's blob. The very bottom outer corner ignores fingers
that land there: in landscape that is the base of the hand holding the device, not the thumb
playing. Held in portrait, a panel asks you to turn the device: the court is wider than it is tall,
and it does not fit in the other direction.

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

**On a touchscreen**, hold the device in landscape: slide a finger anywhere in your own half of
the screen — including right along the bottom — to move your blob, tap the jump button to jump, and
pause from the top-right corner. Against the computer, your side is yours to choose in the options.

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
| Mobile friendly | **yes** — the `Mobile friendly` embed box, which was UNTICKED until 1.1.0 |
| Orientation | **Landscape** — the embed option, matching the game's own orientation gate |
| Player count | **1 – 2**, Local multiplayer — it read "1 – 1, single-player" until 1.1.0 |
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

## 7. Devlog — version 1.3.0

**Title:** Three fixes that all come from the same mistake

**Body:**

Removing the direction pad in 1.2.0 gave the bottom of the screen back to the game. It also
created a problem I did not see at the time: **the surface you control with is now the surface you
play on**, and a hand lands where the eye is looking — which is the middle of the court.

These three fixes all follow from that.

**The game finally says what it already allowed.** Only the *horizontal* position of your finger is
read. The vertical means nothing, so you have always been able to drive from right down at sand
level, well away from the blobs and the ball. Nothing said so, and nobody discovers it on their
own. The on-screen hint now reads "slide in the left half of the screen, **even right at the
bottom**". An affordance no player discovers does not exist — the cheapest of these three fixes,
and the one that repairs the most.

**You can pick your side against the computer** (Options → Player's side). Letting players place
their own controls is a standing recommendation in mobile-ergonomics writing, and the Android
Blobby Volley already offers a side swap.

Moving the jump button alone would have been cosmetic, and misleading. The half of the screen you
slide in and the half of the court your blob runs in **cannot be chosen separately** — your finger
points at the court, so the screen half *is* the court half. The setting therefore swaps the roles
for real: the computer takes the other blob, the jump button moves to the opposite edge, and on a
keyboard you move to player 2's keys, which the hint then names for you.

**The bottom outer corner stops listening.** Held in landscape, what touches the glass down there
is not the thumb that plays but the base of the hand that holds the device. Read as a destination,
it pins your blob against its wall and keeps it there — and the symptom is a blob that "stops
responding" while the game is obeying perfectly, to a hand you don't know you put down. Only
fingers that *land* there are refused; a slide already under way passes straight through, because
tearing a finger away mid-run would cost the very point the zone exists to save.

That last one was sized wrong on the first attempt, and measuring caught it: the corner covered a
third of the player's half, all the way to the wall — in exactly the strip the new hint invites you
to use. It now measures about fifteen millimetres by two.

**One honest limitation.** Those two numbers are an estimate. What you would need to know is where
the *centre* of the contact made by the base of a thumb actually falls, and that only comes from a
real device. Too short and the zone lets through the hand it is aiming at; too tall and it refuses
a finger that is playing — and the second failure would be worse than the one being fixed.

---

## 8. Devlog — version 1.2.0

**Title:** The pad is gone — your blob follows your finger now

**Body:**

The touch controls shipped in 1.1.0 gave each player a left/right pad. It worked, and it was in
the wrong place: the pad sat along the bottom of the screen, which is exactly the strip where the
blobs live. You lost sight of the character you were moving at the very moment you moved it.

**So the pad is gone.** Slide anywhere in your own half of the screen instead. The spot you touch
is the spot on the court your blob runs to — the two halves of the screen are already the two
halves of the court, so your finger points *literally* at the place you want. There is no
sensitivity to tune and no gesture to translate, because nothing is being translated.

**It is not faster than the keyboard.** What a finger produces is an axis capped at ±1, exactly
like a held key: a swipe across the whole screen does not make your blob run faster than a player
holding a direction. Without that cap, touch would simply be the stronger way to play, and the two
would no longer be the same game.

**It eases into the target rather than slamming into it.** At full speed the blob would overshoot
the point you picked, come back, overshoot again — vibrating around your finger instead of
settling on it. The last stretch is scaled to the distance the blob covers in one physics step,
which is the only unit that means anything there.

**A finger belongs to the half it landed in**, even once it crosses the middle of the screen.
Without that, a player running towards the net would start driving their opponent's blob — and
running towards the net is precisely what this game is about.

**A pale column marks the spot you picked.** It doesn't duplicate your finger, it corrects for it:
a finger hides the point it touches, and the blob takes a moment to get there. Without the column,
nothing tells you where the game thinks you pointed.

**And the options can finally be reached with a thumb.** This menu has no scrollbar of its own —
the visible window follows the current line, and only the keyboard and the mouse wheel ever moved
it. A phone has neither. You could touch the lines on screen and *nothing in the world* got you to
the ones below. Two ways in now: drag the list, and tap the overflow arrows, which move three
lines at a time. They only ever announced that the list continued, which was enough while you had
a wheel — with a finger, a hint you cannot touch just points at what you can't reach.

The desktop version is unchanged: no touch control appears unless a finger touches the screen.

---

## 9. Devlog — version 1.1.0 *(published 2026-08-25)*

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
