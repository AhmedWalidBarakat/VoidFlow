# VoidFlow bhop challenge

A 10 stage bunny hop map behind the start hall. Finish all ten stages to win the
**Karambit | Velocity** and its matching **Void Gloves | Velocity**, a set you can't get any other
way (not from cases, gifts, or finishing the surf course).

It's an original design built around CS-style movement (Source physics, 64 tick, auto-hop,
air-accelerate 150). No layouts, names or assets were taken from existing maps; the bhop videos it
was studied from only showed the style (stage rooms of separate blocks over a drop, stage
checkpoints, a mix of block sizes, ramps and heights).

## How to get there

- In the start hall, turn around: the back wall has a gold archway marked **BHOP CHALLENGE**.
- Through it: a terrace, then a **bhop trail** of five wide marble steps. Hold W and jump; you can
  hop them at running speed.
- The trail ends at the **plaza**: the challenge's sign, the prize turning on two pedestals, and
  stage 1's pad on the plaza's edge.

## Rules and controls

| | |
|---|---|
| Start | Step onto the stage 1 pad. The clock starts when you hop off it |
| Checkpoints | Every stage's pad is a checkpoint. Fall and you're back on the pad of the stage you're on |
| T | Restart the stage you're on (from its pad) |
| R | Back to stage 1 (the clock resets) |
| C | At the plaza: carry on from the furthest stage you've reached (it counts for the prize, not for a best time) |
| Finish | Land on the finish platform after stage 10. The set is yours and equipped; step into the ring to go back up |
| Noclip | Locked out here (a fast double tap of jump would otherwise switch it on mid-hop). Flying in from the hall makes the attempt practice, with nothing won |

The HUD shows the stage, the run's time, the stage's time, your best, your speed and your keys.

## The layout

The ten stages spiral down around a white and gold spire, one after another, about 150 m in all.
Each stage after the first starts with a **drop-in**: off its pad and 8 m down onto the first
block. Each stage's pad is a ring of light on the spire with the stage's number.

```

                           ###########
                           ###########
                           ###########
                           ###########
                           ###########
                           ###########
                                :
                           . .::
                        ..    :..
                       9      P 1..         . 6
                      .           .. .  . .    .
                     .              . .. .      ~ ..
                     .              .  .   0 ..  ~ ..
                    .            . .      ~~~~~2~~~
                    .           .              ...~~
                   ..        ..                  .. .
                  .        ..                     .. .
                 .         .   OOO                 . .
                ..       5    OO OO                ... .
               8         .     OOO        . 7     . .    .
              .         .            . ..   .  .     ..     .
             .          .        . .        .  .      .      .
         .  ...      ~~~      . .           .   . . .  3   .
        .      ~ ~~~~~   . .                 . . .    .  .
         .      ~~~~~~~                  .  .     . .
          .  . .               .   .  .    .   .
                .  4 .  .   .
                                         .
                 F   .                 ~
                           .        ~~~~
                               ~~~~~
```

`#` the start hall, `:` the bhop trail and terrace, `P` the plaza, `O` the spire, `1` to `9` and `0` the pads of stages 1 to 10, `F` the finish, `.` blocks, `~` surf ramps. The hall is at the top; each square is about 4 x 10 m.


| Pad | Where (x, z) | Height |
|---|---|---|
| stage 1 | -4, -98 | 0 m |
| stage 2 | 52, -134 | -3 m |
| stage 3 | 81, -222 | -11 m |
| stage 4 | -53, -267 | -31 m |
| stage 5 | -30, -182 | -50 m |
| stage 6 | 47, -91 | -61 m |
| stage 7 | 41, -193 | -80 m |
| stage 8 | -67, -199 | -98 m |
| stage 9 | -39, -96 | -110 m |
| stage 10 | 39, -129 | -126 m |
| finish | -62, -282 | -155 m |


Every stage is placed by the same rule: leave the pad in the direction that keeps the stage on a
ring round the spire, always going the same way round, and never within 13 m (across) of another
stage at a similar height (within 16 m), the hall, or the spire. The stages cross over one another
at different heights, so the whole course is visible from most of it.

The surf course was checked against this space: built in full (all 116 stages), nothing of it comes
within the challenge's volume (x -130 to 125, y -180 to 30, z -300 to -30), and the floating islands
of the hall's backdrop are kept out of it too.

## How the stages were designed

Each stage is designed as a **movement sequence**, not a list of blocks. Every hop states how it
should feel: how much its height changes, how much it turns, the size of the block it lands on, and
how hard it is. Difficulty is set as the speed the hop needs, as a share of what an expert has by
then: a player strafing at 70% efficiency from the stage's pad, gaining speed hop by hop. The block
then goes where that hop comes down. So a jump is hard because of speed, timing, angle and
momentum, not just because the block is small.

What shapes every jump, under the game's own physics:

- **With auto-hop, a hop's length is your speed times its airtime.** You can't hop short without
  braking, so the spacing of the blocks sets the speed you need.
- **Heights set the rhythm.** A step up shortens a hop (airtime 0.53 s for +1.2 m), a drop
  lengthens it (0.96 s for -2 m), at the same speed. Chains of up-steps punish any loss of speed.
- **Turning has a limit.** The tightest air turn at speed v has a radius of about v^2/49 m (2 m at
  10 m/s, 8 m at 20 m/s), so tight corners after fast sections force you to slow down first.
- **Gaining speed is slow.** Perfect strafing adds about 28 (m/s)^2 per flat hop (about +1.4 m/s at
  10 m/s, +0.7 m/s at 20 m/s), more in longer airtime. A hop asking for near the limit leaves no room
  for a lost tick.
- **Edges matter.** The player is a capsule: land with your middle near an edge and its round bottom
  catches it, taking most of your speed. Landing windows are measured with your middle at least
  0.2 m in from the edge.
- **Slopes boost, banks turn.** Landing on a downhill slope turns fall speed into forward speed;
  landing on a bank tilted into a turn takes away the outward drift.
- **Surf ramps carry momentum.** Hop onto the face, surf it round its curve, and fly off its end
  onto a strip. Exits come out a little low and slow, so each strip lies mostly behind where a
  perfect exit would land.

Every stage has several hard jumps followed by an easier one to recover and build speed again,
and stage by stage it asks more: 60% of the expert's pace on the warm-up steps, about 90% by stage
5, and up to the full expert pace, or past it, in the last stages.

## Stage by stage

**1. Sky Steps** (warm-up). Wide marble steps (4 x 3.5 m), a gentle curve each way, a first drop
and a long jump down 2 m. Teaches holding jump, sync strafing and gaining speed. Needs at most 70%
of an expert's pace.

**2. Lantern Garden** (narrow platforms). Long planks 1.1 to 1.3 m wide: hold your line. Then three
crosswise planks 1.2 to 1.3 m deep at an even beat: hold your speed, too fast or too slow both miss.
Then a slalom of planks turning 20 to 35 degrees each way, and a wide rest block.

**3. Red Spires** (long gaps). Four wide tower tops to build speed on, a downhill of towers 2 m
down each, and three long gaps (13.6 to 16.3 m) that need the speed you built. A miss means hitting a
tower's side: the towers run on down below.

**4. Banked Bowl** (curves and angled surfaces). A slope boost, a banked 180 (five banks turning 36
degrees each), then a surf ramp curving 90 degrees the other way onto a landing strip, and a
30 degree turn off it. Turn without losing speed.

**5. Null Room** (precision). Small black blocks (2.2 m down to 1.3 m) where heights set the rhythm:
up a metre, down one and a half, up, down, then offsets 25 degrees each way and two tiny blocks at
speed. About 90% of an expert's pace with very little room.

**6. Neon Grid** (high speed). A drop-in, then a straight surf ramp to about 20 m/s, a run of 90
degree direction changes at that speed, a speed check, and a hairpin of three 60 degree turns
that you can only make at about 9 m/s: slowing down is the skill. Then rebuild to the pad.

**7. Glass Slalom** (advanced strafing). Alternating 80 degree left/right turns between 2 m blocks
at 12 to 15 m/s, then a surf ramp curving 90 degrees, a landing strip and a 60 degree momentum
transfer onto a 2 m block.

**8. Shard Field** (extreme precision). A drop onto a wide landing, then crystal shards 1 to 1.2 m
across: drops that build speed, two up-steps where you must slow down or overshoot, a long gap of
13 m onto a 1.2 m shard, and a 15 degree turn onto a 1 m one.

**9. Gauntlet** (everything). Narrow planks, three banks, a climb of four 1.2 m up-steps where each
needs more speed than the last, tiny blocks, a surf ramp curving 60 degrees onto a plank, a hairpin
you must slow for, then a long gap at full speed. Almost nowhere to rest.

**10. Ascension** (mastery). Two slope boosts into long gaps at maximum speed, a fast 100 degree
corner, the climb (five up-steps), tiny blocks at about 19 m/s, the final surf ramp curving 90
degrees, and the final jump: 25 m onto the finish's landing, about 23 m/s needed off the ramp.

## Playtest: is every jump possible, and how hard is it?

A bot played every stage with the game's own movement code (PlayerMovement.Simulate, 64 tick),
from each stage's pad, like a bhop player. It predicts where it will come down and strafes for
speed while that's on the block, turns toward the block when it's off to a side, swings wider to
spend spare speed rather than braking, and brakes early and gently only when it would overshoot.
On a surf ramp it presses into the face. Four players:

- **Perfect**: switches strafe side any tick, no error. Proves the jumps can be made.
- **Near-perfect**: switches every 2 ticks, a few centimetres of aim error. 8 runs.
- **Expert**: switches at most every 7 ticks (about 9 times a second), 2 degrees of aim error, a
  limited swing. 8 runs.
- **Good**: every 12 ticks, more error. 8 runs.

| Stage | Perfect | Near-perfect (8 runs) | Expert (8 runs) | Good (8 runs) | Where the expert falls |
|---|---|---|---|---|---|
| 1 Sky Steps | clears it (8.6s) | 8/8 (8.6s) | 8/8 (8.6s) | 3/8 (8.6s) | - |
| 2 Lantern Garden | clears it (11.1s) | 8/8 (11.1s) | 5/8 (11.1s) | 1/8 (11.1s) | hop 4 (1x), hop 10 (1x), hop 9 (1x) |
| 3 Red Spires | clears it (12.3s) | 8/8 (12.3s) | 4/8 (12.3s) | 0/8 | hop 11 (2x), hop 12 (2x) |
| 4 Banked Bowl | clears it (13.5s) | 8/8 (13.4s) | 3/8 (13.3s) | 0/8 | hop 13 (2x), hop 4 (1x), hop 6 (1x) |
| 5 Null Room | clears it (11.8s) | 8/8 (11.8s) | 2/8 (11.8s) | 0/8 | hop 11 (3x), hop 5 (1x), hop 12 (1x) |
| 6 Neon Grid | falls | 3/8 (15.4s) | 0/8 | 0/8 | hop 10 (3x), hop 9 (2x), hop 8 (1x) |
| 7 Glass Slalom | clears it (14.0s) | 8/8 (13.9s) | 1/8 (14.1s) | 0/8 | hop 8 (3x), hop 7 (2x), hop 5 (1x) |
| 8 Shard Field | falls | 3/8 (10.4s) | 1/8 (10.4s) | 0/8 | hop 3 (3x), hop 9 (1x), hop 2 (1x) |
| 9 Gauntlet | clears it (16.3s) | 1/8 (17.0s) | 1/8 (16.3s) | 0/8 | hop 10 (3x), hop 8 (2x), hop 1 (1x) |
| 10 Ascension | clears it (19.1s) | 4/8 (19.1s) | 0/8 | 0/8 | hop 5 (3x), hop 7 (2x), hop 8 (1x) |

Every stage was finished by at least one of the bots, so none is impossible. A stage the perfect
bot doesn't finish (it plays one fixed way) was finished by the near-perfect bot. The expert's
success falls stage by stage from all 8 runs on stage 1 to almost none from stage 6 on: the last
stages need near-perfect movement.

The playtest found and fixed, before release:

- Landing near an edge killing your speed (the capsule): every landing window now keeps your middle
  0.2 m in from the edge.
- Blocks after surf ramps placed where a perfect exit would land; real exits land 2 to 4 m short, so
  the strips now lie behind that point and the next jump is measured from where you really land.
- A curved final ramp too tight to hold at 21 m/s (120 degrees in 30 m): now 90 degrees in 38 m.
- A shard placed too close after a drop-in (forcing a hard brake that left too little speed for the
  next shard), three slopes in a row (landing on the third was out of reach), a hairpin that needed
  a slowdown from 20 to 8 m/s in one hop (now split by a speed check), and a few gaps over the limit
  after a fast section.

## Every jump

What each hop asks, from the layout: its distance from the planned landing on the block before,
the height change, the turn (the strafe side that makes it), the airtime, the block's size (along
the route x across it), the speed it needs to reach the block (your middle 0.2 m in from the near
edge), the speed above which you must brake to stay on it, the speed a perfect strafer has there,
and what it asks of an expert (needed speed as a share of the 70% strafer's pace: over 100% means
better than that). Speeds in u/s (1 m/s = 39.4 u/s).

#### Stage 1: Sky Steps

| # | Piece | Distance | Height | Turn / strafe | Airtime | Landing (m) | Needs (u/s) | Brake above (u/s) | Perfect has | Asks of an expert |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | block (first hops) | 5.0 m | +0.0 m | sync (L/R) | 0.76 s | 4.0 x 3.5 | 167 | 354 | 287 | 60% |
| 2 | block | 5.6 m | +0.0 m | right 8° | 0.76 s | 4.0 x 3.5 | 196 | 384 | 356 | 60% |
| 3 | block | 5.8 m | +0.0 m | right 8° | 0.76 s | 3.5 x 3.0 | 222 | 384 | 412 | 60% |
| 4 | block (first drop) | 7.0 m | -1.0 m | sync (L/R) | 0.87 s | 3.5 x 3.0 | 248 | 388 | 426 | 60% |
| 5 | block | 6.1 m | +0.0 m | left 12° | 0.76 s | 3.0 x 3.0 | 251 | 386 | 425 | 60% |
| 6 | block | 6.1 m | +0.0 m | left 12° | 0.76 s | 3.0 x 3.0 | 250 | 385 | 424 | 60% |
| 7 | block (long jump) | 9.0 m | -2.0 m | sync (L/R) | 0.96 s | 4.0 x 4.0 | 294 | 441 | 429 | 70% |
| 8 | block | 6.4 m | +0.0 m | right 18° | 0.76 s | 3.5 x 3.5 | 252 | 414 | 477 | 55% |
| 9 | block | 6.0 m | +0.0 m | right 18° | 0.76 s | 4.0 x 4.0 | 222 | 409 | 450 | 50% |
| 10 | checkpoint pad | 5.0 m | +0.0 m | sync (L/R) | 0.76 s | 8.0 x 8.0 | 198 | 323 | 446 | 45% |

#### Stage 2: Lantern Garden

| # | Piece | Distance | Height | Turn / strafe | Airtime | Landing (m) | Needs (u/s) | Brake above (u/s) | Perfect has | Asks of an expert |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | block (drop in: planks) | 8.5 m | -8.0 m | sync (L/R) | 1.34 s | 4.5 x 1.2 | 189 | 309 | 312 | 64% |
| 2 | block | 6.2 m | +0.0 m | sync (L/R) | 0.76 s | 4.5 x 1.2 | 218 | 432 | 350 | 64% |
| 3 | block | 6.8 m | +0.0 m | right 6° | 0.76 s | 4.5 x 1.1 | 245 | 459 | 407 | 64% |
| 4 | block | 7.2 m | +0.0 m | right 6° | 0.76 s | 4.5 x 1.1 | 269 | 483 | 457 | 64% |
| 5 | block (crosswise beat) | 6.9 m | +0.0 m | sync (L/R) | 0.76 s | 1.3 x 4.0 | 337 | 384 | 502 | 74% |
| 6 | block | 6.3 m | +0.0 m | sync (L/R) | 0.76 s | 1.3 x 4.0 | 306 | 353 | 421 | 74% |
| 7 | block | 6.0 m | +0.0 m | sync (L/R) | 0.76 s | 1.2 x 4.0 | 291 | 333 | 391 | 76% |
| 8 | block (plank slalom) | 6.3 m | +0.0 m | left 20° | 0.76 s | 4.0 x 1.3 | 233 | 420 | 372 | 64% |
| 9 | block | 6.8 m | +0.0 m | right 35° | 0.76 s | 4.0 x 1.3 | 258 | 446 | 426 | 64% |
| 10 | block | 7.2 m | +0.0 m | left 35° | 0.76 s | 4.0 x 1.3 | 281 | 469 | 474 | 64% |
| 11 | block | 7.6 m | +0.0 m | right 20° | 0.76 s | 4.0 x 1.3 | 302 | 490 | 504 | 64% |
| 12 | block (rest) | 7.6 m | +0.0 m | sync (L/R) | 0.76 s | 5.0 x 4.0 | 277 | 517 | 525 | 55% |
| 13 | checkpoint pad | 6.3 m | +0.0 m | sync (L/R) | 0.76 s | 6.0 x 6.0 | 267 | 391 | 552 | 50% |

#### Stage 3: Red Spires

| # | Piece | Distance | Height | Turn / strafe | Airtime | Landing (m) | Needs (u/s) | Brake above (u/s) | Perfect has | Asks of an expert |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | block (drop in: build) | 8.3 m | -8.0 m | sync (L/R) | 1.34 s | 5.0 x 5.0 | 178 | 313 | 312 | 60% |
| 2 | block | 6.6 m | +0.0 m | sync (L/R) | 0.76 s | 5.0 x 5.0 | 224 | 463 | 352 | 65% |
| 3 | block | 7.2 m | +0.0 m | sync (L/R) | 0.76 s | 4.5 x 4.5 | 270 | 483 | 409 | 70% |
| 4 | block | 7.9 m | +0.0 m | sync (L/R) | 0.76 s | 4.0 x 4.0 | 317 | 505 | 459 | 75% |
| 5 | block (downhill) | 11.2 m | -2.0 m | sync (L/R) | 0.96 s | 3.5 x 3.5 | 397 | 524 | 510 | 86% |
| 6 | block | 11.8 m | -2.0 m | sync (L/R) | 0.96 s | 3.0 x 3.0 | 431 | 538 | 561 | 86% |
| 7 | block | 12.6 m | -2.0 m | right 5° | 0.96 s | 3.0 x 3.0 | 463 | 570 | 578 | 86% |
| 8 | block | 13.1 m | -2.0 m | right 5° | 0.96 s | 2.6 x 2.6 | 493 | 583 | 609 | 86% |
| 9 | block (long gap) | 13.6 m | -1.0 m | sync (L/R) | 0.87 s | 3.0 x 3.0 | 556 | 674 | 621 | 92% |
| 10 | block (long gap) | 13.8 m | -1.0 m | sync (L/R) | 0.87 s | 2.8 x 2.8 | 570 | 679 | 660 | 90% |
| 11 | block (max gap) | 16.3 m | -2.0 m | sync (L/R) | 0.96 s | 4.0 x 4.0 | 595 | 743 | 699 | 90% |
| 12 | block (rest) | 9.1 m | +0.0 m | right 15° | 0.76 s | 4.0 x 4.0 | 378 | 566 | 733 | 55% |
| 13 | checkpoint pad | 6.9 m | +0.0 m | sync (L/R) | 0.76 s | 6.0 x 6.0 | 298 | 423 | 600 | 50% |

#### Stage 4: Banked Bowl

| # | Piece | Distance | Height | Turn / strafe | Airtime | Landing (m) | Needs (u/s) | Brake above (u/s) | Perfect has | Asks of an expert |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | block (drop in) | 8.3 m | -8.0 m | sync (L/R) | 1.34 s | 4.0 x 3.0 | 192 | 298 | 312 | 65% |
| 2 | block | 6.0 m | +0.0 m | sync (L/R) | 0.76 s | 3.5 x 3.0 | 231 | 393 | 339 | 70% |
| 3 | slope (slope boost) | 10.0 m | -2.5 m | sync (L/R) | 1.00 s | 5.0 x 4.0 | 304 | 484 | 406 | 80% |
| 4 | block | 8.8 m | -1.0 m | sync (L/R) | 0.87 s | 3.0 x 3.0 | 341 | 459 | 469 | 80% |
| 5 | bank (banked 180) | 8.4 m | +0.0 m | right 36° | 0.76 s | 3.0 x 2.5 | 371 | 506 | 495 | 80% |
| 6 | bank | 8.9 m | +0.0 m | right 36° | 0.76 s | 3.0 x 2.5 | 396 | 531 | 537 | 80% |
| 7 | bank | 9.3 m | +0.0 m | right 36° | 0.76 s | 3.0 x 2.5 | 420 | 556 | 567 | 80% |
| 8 | bank | 9.8 m | +0.0 m | right 36° | 0.76 s | 3.0 x 2.5 | 442 | 578 | 590 | 80% |
| 9 | bank | 10.2 m | +0.0 m | right 36° | 0.76 s | 3.0 x 2.5 | 464 | 599 | 613 | 80% |
| 10 | surf ramp (curved ramp) | 11.7 m onto it, then 26 m of ramp | -1.0 m | left 90° along the ramp | 0.87 s | its face | 486 | - | 637 | - |
| 11 | block (ramp to block) | 11.3 m | -2.5 m (fall) | sync (L/R) | 0.50 s | 8.0 x 3.5 | 594 | 1198 | 823 | 75% |
| 12 | block | 12.5 m | +0.0 m | left 30° | 0.76 s | 3.0 x 2.5 | 582 | 718 | 844 | 72% |
| 13 | block (rest) | 10.4 m | +0.0 m | right 20° | 0.76 s | 4.0 x 4.0 | 450 | 637 | 754 | 60% |
| 14 | checkpoint pad | 7.6 m | +0.0 m | sync (L/R) | 0.76 s | 6.0 x 6.0 | 334 | 459 | 672 | 50% |

#### Stage 5: Null Room

| # | Piece | Distance | Height | Turn / strafe | Airtime | Landing (m) | Needs (u/s) | Brake above (u/s) | Perfect has | Asks of an expert |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | block (drop in) | 8.0 m | -8.0 m | sync (L/R) | 1.34 s | 2.2 x 2.2 | 207 | 260 | 312 | 70% |
| 2 | block | 5.0 m | +0.0 m | sync (L/R) | 0.76 s | 2.0 x 2.0 | 220 | 304 | 303 | 75% |
| 3 | block (up) | 5.1 m | +1.0 m | sync (L/R) | 0.59 s | 1.8 x 1.8 | 298 | 391 | 338 | 90% |
| 4 | block (down) | 8.5 m | -1.5 m | sync (L/R) | 0.92 s | 1.8 x 1.8 | 336 | 396 | 397 | 90% |
| 5 | block | 6.2 m | +1.0 m | right 10° | 0.59 s | 1.6 x 1.6 | 372 | 452 | 428 | 90% |
| 6 | block | 10.0 m | -1.5 m | right 10° | 0.92 s | 1.6 x 1.6 | 404 | 455 | 476 | 90% |
| 7 | block (offsets) | 8.9 m | +0.0 m | left 25° | 0.76 s | 1.5 x 1.5 | 436 | 493 | 491 | 90% |
| 8 | block | 9.4 m | +0.0 m | right 25° | 0.76 s | 1.4 x 1.4 | 463 | 515 | 528 | 90% |
| 9 | block | 7.0 m | +1.2 m | left 25° | 0.53 s | 1.3 x 1.3 | 485 | 552 | 545 | 90% |
| 10 | block | 12.9 m | -2.0 m | right 25° | 0.96 s | 1.3 x 1.3 | 509 | 546 | 582 | 90% |
| 11 | block (tiny) | 9.9 m | +0.0 m | sync (L/R) | 0.76 s | 1.4 x 1.4 | 489 | 539 | 581 | 85% |
| 12 | block | 11.2 m | -1.0 m | sync (L/R) | 0.87 s | 1.4 x 1.4 | 485 | 528 | 577 | 85% |
| 13 | block (rest) | 8.0 m | +0.0 m | sync (L/R) | 0.76 s | 3.5 x 3.5 | 335 | 496 | 563 | 60% |
| 14 | checkpoint pad | 6.2 m | +0.0 m | sync (L/R) | 0.76 s | 6.0 x 6.0 | 263 | 388 | 531 | 50% |

#### Stage 6: Neon Grid

| # | Piece | Distance | Height | Turn / strafe | Airtime | Landing (m) | Needs (u/s) | Brake above (u/s) | Perfect has | Asks of an expert |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | block (drop in) | 8.9 m | -8.0 m | sync (L/R) | 1.34 s | 4.0 x 4.0 | 207 | 313 | 312 | 70% |
| 2 | surf ramp (speed ramp) | 7.7 m onto it, then 40 m of ramp | -1.0 m | sync (L/R) along the ramp | 0.87 s | its face | 306 | - | 357 | - |
| 3 | block (off the ramp) | 11.7 m | -2.5 m (fall) | sync (L/R) | 0.50 s | 8.0 x 3.5 | 626 | 1230 | 795 | 80% |
| 4 | block | 14.1 m | +0.0 m | sync (L/R) | 0.76 s | 4.0 x 4.0 | 639 | 827 | 817 | 80% |
| 5 | block (90 at speed) | 14.7 m | +0.0 m | right 45° | 0.76 s | 4.0 x 4.0 | 670 | 858 | 843 | 82% |
| 6 | block | 14.9 m | +0.0 m | right 45° | 0.76 s | 4.0 x 4.0 | 685 | 873 | 869 | 82% |
| 7 | block | 15.2 m | +0.0 m | left 45° | 0.76 s | 4.0 x 4.0 | 700 | 888 | 893 | 82% |
| 8 | block | 15.5 m | +0.0 m | left 45° | 0.76 s | 4.0 x 4.0 | 714 | 902 | 917 | 82% |
| 9 | block (check your speed) | 11.0 m | +0.0 m | right 20° | 0.76 s | 3.0 x 3.0 | 506 | 641 | 940 | 57% |
| 10 | block (hairpin: slow down) | 7.0 m | +0.0 m | right 60° | 0.76 s | 2.5 x 2.5 | 310 | 420 | 677 | 46% |
| 11 | block | 7.0 m | +0.0 m | right 60° | 0.76 s | 2.5 x 2.5 | 310 | 420 | 456 | 69% |
| 12 | block | 7.0 m | +0.0 m | right 60° | 0.76 s | 2.5 x 2.5 | 310 | 420 | 456 | 69% |
| 13 | block (rebuild) | 8.2 m | +0.0 m | sync (L/R) | 0.76 s | 3.0 x 3.0 | 359 | 495 | 456 | 80% |
| 14 | block | 9.4 m | +0.0 m | sync (L/R) | 0.76 s | 3.0 x 3.0 | 424 | 559 | 501 | 88% |
| 15 | block | 9.9 m | +0.0 m | sync (L/R) | 0.76 s | 3.0 x 3.0 | 451 | 586 | 543 | 88% |
| 16 | checkpoint pad | 7.4 m | +0.0 m | sync (L/R) | 0.76 s | 6.0 x 6.0 | 324 | 450 | 581 | 60% |

#### Stage 7: Glass Slalom

| # | Piece | Distance | Height | Turn / strafe | Airtime | Landing (m) | Needs (u/s) | Brake above (u/s) | Perfect has | Asks of an expert |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | block (drop in) | 8.4 m | -8.0 m | sync (L/R) | 1.34 s | 3.0 x 3.0 | 207 | 283 | 312 | 70% |
| 2 | block | 6.1 m | +0.0 m | sync (L/R) | 0.76 s | 3.0 x 3.0 | 253 | 388 | 325 | 80% |
| 3 | block | 7.3 m | +0.0 m | sync (L/R) | 0.76 s | 2.5 x 2.5 | 325 | 434 | 386 | 90% |
| 4 | block (slalom) | 7.7 m | +0.0 m | right 40° | 0.76 s | 2.0 x 2.0 | 361 | 444 | 439 | 90% |
| 5 | block | 8.3 m | +0.0 m | left 80° | 0.76 s | 2.0 x 2.0 | 393 | 476 | 480 | 90% |
| 6 | block | 8.9 m | +0.0 m | right 80° | 0.76 s | 2.0 x 2.0 | 423 | 507 | 512 | 90% |
| 7 | block | 9.4 m | +0.0 m | left 80° | 0.76 s | 2.0 x 2.0 | 451 | 535 | 542 | 90% |
| 8 | block | 10.0 m | +0.0 m | right 80° | 0.76 s | 2.0 x 2.0 | 478 | 561 | 570 | 90% |
| 9 | block | 10.4 m | +0.0 m | left 40° | 0.76 s | 2.0 x 2.0 | 502 | 586 | 596 | 90% |
| 10 | block | 9.7 m | +0.0 m | sync (L/R) | 0.76 s | 3.0 x 3.0 | 439 | 574 | 621 | 75% |
| 11 | surf ramp (curved ramp) | 13.0 m onto it, then 30 m of ramp | -1.0 m | right 90° along the ramp | 0.87 s | its face | 545 | - | 612 | - |
| 12 | block (ramp to platform) | 12.5 m | -2.5 m (fall) | sync (L/R) | 0.50 s | 8.0 x 3.0 | 689 | 1293 | 873 | 80% |
| 13 | block (momentum transfer) | 12.9 m | +0.0 m | right 60° | 0.76 s | 2.0 x 2.0 | 631 | 715 | 893 | 72% |
| 14 | block (rest) | 10.4 m | +0.0 m | sync (L/R) | 0.76 s | 4.0 x 4.0 | 448 | 635 | 750 | 60% |
| 15 | checkpoint pad | 7.6 m | +0.0 m | sync (L/R) | 0.76 s | 6.0 x 6.0 | 333 | 458 | 670 | 50% |

#### Stage 8: Shard Field

| # | Piece | Distance | Height | Turn / strafe | Airtime | Landing (m) | Needs (u/s) | Brake above (u/s) | Perfect has | Asks of an expert |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | wide landing (drop in: a wide landing) | 7.8 m | -8.0 m | sync (L/R) | 1.34 s | 12.0 x 8.0 | 178 | 283 | 312 | 60% |
| 2 | block (shards) | 7.8 m | -1.0 m | sync (L/R) | 0.87 s | 1.2 x 1.2 | 335 | 372 | 330 | 105% |
| 3 | block | 8.1 m | -1.0 m | sync (L/R) | 0.87 s | 1.1 x 1.1 | 352 | 384 | 400 | 95% |
| 4 | block | 7.8 m | +0.0 m | right 10° | 0.76 s | 1.0 x 1.0 | 392 | 423 | 421 | 95% |
| 5 | block (drop) | 11.7 m | -3.0 m | sync (L/R) | 1.04 s | 1.0 x 1.0 | 431 | 454 | 468 | 95% |
| 6 | block (slow down) | 6.0 m | +1.2 m | sync (L/R) | 0.53 s | 1.0 x 1.0 | 420 | 465 | 484 | 88% |
| 7 | block | 6.2 m | +1.2 m | left 20° | 0.53 s | 1.0 x 1.0 | 435 | 480 | 494 | 89% |
| 8 | block | 9.6 m | +0.0 m | sync (L/R) | 0.76 s | 1.0 x 1.0 | 483 | 515 | 515 | 95% |
| 9 | block (long gap) | 13.2 m | -2.0 m | sync (L/R) | 0.96 s | 1.2 x 1.2 | 525 | 558 | 555 | 97% |
| 10 | block | 10.7 m | +0.0 m | right 15° | 0.76 s | 1.0 x 1.0 | 544 | 575 | 593 | 95% |
| 11 | block (rest) | 8.2 m | +0.0 m | sync (L/R) | 0.76 s | 3.0 x 3.0 | 359 | 494 | 610 | 60% |
| 12 | checkpoint pad | 6.2 m | +0.0 m | sync (L/R) | 0.76 s | 6.0 x 6.0 | 262 | 387 | 530 | 50% |

#### Stage 9: Gauntlet

| # | Piece | Distance | Height | Turn / strafe | Airtime | Landing (m) | Needs (u/s) | Brake above (u/s) | Perfect has | Asks of an expert |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | block (drop in: planks) | 9.4 m | -8.0 m | sync (L/R) | 1.34 s | 4.0 x 1.0 | 222 | 328 | 312 | 75% |
| 2 | block | 7.6 m | +0.0 m | sync (L/R) | 0.76 s | 4.0 x 1.0 | 305 | 493 | 367 | 85% |
| 3 | bank (banks) | 7.9 m | +0.0 m | right 40° | 0.76 s | 2.5 x 2.0 | 359 | 468 | 422 | 90% |
| 4 | bank | 8.6 m | +0.0 m | right 40° | 0.76 s | 2.5 x 2.0 | 391 | 501 | 470 | 90% |
| 5 | bank | 9.1 m | +0.0 m | right 40° | 0.76 s | 2.5 x 2.0 | 422 | 531 | 514 | 90% |
| 6 | block (climb) | 6.5 m | +1.2 m | sync (L/R) | 0.53 s | 1.6 x 1.6 | 436 | 524 | 549 | 88% |
| 7 | block | 6.8 m | +1.2 m | sync (L/R) | 0.53 s | 1.6 x 1.6 | 454 | 543 | 554 | 88% |
| 8 | block | 7.2 m | +1.2 m | sync (L/R) | 0.53 s | 1.6 x 1.6 | 483 | 572 | 573 | 90% |
| 9 | block | 7.4 m | +1.2 m | sync (L/R) | 0.53 s | 1.6 x 1.6 | 501 | 589 | 599 | 90% |
| 10 | block (tiny) | 14.1 m | -2.0 m | sync (L/R) | 0.96 s | 1.0 x 1.0 | 565 | 590 | 629 | 97% |
| 11 | block | 11.7 m | +0.0 m | left 30° | 0.76 s | 1.0 x 1.0 | 593 | 624 | 625 | 97% |
| 12 | surf ramp (ramp) | 14.6 m onto it, then 26 m of ramp | -1.0 m | left 60° along the ramp | 0.87 s | its face | 619 | - | 661 | - |
| 13 | block (ramp to plank) | 12.9 m | -2.5 m (fall) | sync (L/R) | 0.50 s | 8.0 x 2.0 | 723 | 1326 | 876 | 85% |
| 14 | block (hairpin) | 6.0 m | +0.0 m | left 60° | 0.76 s | 2.0 x 2.0 | 271 | 355 | 896 | 31% |
| 15 | block | 6.0 m | +0.0 m | left 60° | 0.76 s | 2.0 x 2.0 | 271 | 355 | 393 | 70% |
| 16 | block | 6.0 m | +0.0 m | left 60° | 0.76 s | 2.0 x 2.0 | 271 | 355 | 393 | 70% |
| 17 | block | 7.1 m | +0.0 m | sync (L/R) | 0.76 s | 2.0 x 2.0 | 327 | 411 | 393 | 85% |
| 18 | block (max gap) | 11.2 m | -2.0 m | sync (L/R) | 0.96 s | 2.0 x 2.0 | 427 | 493 | 451 | 100% |
| 19 | checkpoint pad | 6.6 m | +0.0 m | sync (L/R) | 0.76 s | 6.0 x 6.0 | 280 | 405 | 503 | 60% |

#### Stage 10: Ascension

| # | Piece | Distance | Height | Turn / strafe | Airtime | Landing (m) | Needs (u/s) | Brake above (u/s) | Perfect has | Asks of an expert |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | block (drop in) | 9.4 m | -8.0 m | sync (L/R) | 1.34 s | 3.0 x 3.0 | 237 | 313 | 312 | 80% |
| 2 | block | 7.0 m | +0.0 m | sync (L/R) | 0.76 s | 2.5 x 2.5 | 310 | 419 | 353 | 90% |
| 3 | slope (slope boosts) | 11.5 m | -2.5 m | sync (L/R) | 1.00 s | 5.0 x 3.0 | 361 | 542 | 418 | 92% |
| 4 | slope | 12.6 m | -2.5 m | sync (L/R) | 1.00 s | 5.0 x 3.0 | 405 | 586 | 482 | 92% |
| 5 | block | 12.4 m | -2.5 m | sync (L/R) | 1.00 s | 2.5 x 2.5 | 446 | 528 | 539 | 92% |
| 6 | block (max speed) | 12.5 m | -1.0 m | sync (L/R) | 0.87 s | 2.0 x 2.0 | 532 | 605 | 566 | 102% |
| 7 | block | 13.2 m | -1.0 m | sync (L/R) | 0.87 s | 1.8 x 1.8 | 566 | 629 | 609 | 102% |
| 8 | block | 15.3 m | -2.0 m | sync (L/R) | 0.96 s | 1.8 x 1.8 | 599 | 656 | 651 | 102% |
| 9 | block (fast corner) | 12.8 m | +0.0 m | right 50° | 0.76 s | 2.0 x 2.0 | 628 | 711 | 688 | 102% |
| 10 | block | 13.3 m | +0.0 m | right 50° | 0.76 s | 2.0 x 2.0 | 652 | 736 | 719 | 102% |
| 11 | block (the climb) | 9.7 m | +1.2 m | sync (L/R) | 0.53 s | 1.6 x 1.6 | 673 | 761 | 744 | 102% |
| 12 | block | 9.9 m | +1.2 m | sync (L/R) | 0.53 s | 1.6 x 1.6 | 689 | 778 | 764 | 102% |
| 13 | block | 10.2 m | +1.2 m | sync (L/R) | 0.53 s | 1.6 x 1.6 | 705 | 794 | 784 | 102% |
| 14 | block | 10.4 m | +1.2 m | sync (L/R) | 0.53 s | 1.6 x 1.6 | 721 | 809 | 803 | 102% |
| 15 | block | 10.6 m | +1.2 m | sync (L/R) | 0.53 s | 1.6 x 1.6 | 736 | 825 | 822 | 102% |
| 16 | block (tiny) | 17.0 m | -1.0 m | sync (L/R) | 0.87 s | 1.0 x 1.0 | 756 | 783 | 846 | 102% |
| 17 | block | 15.2 m | +0.0 m | left 40° | 0.76 s | 1.0 x 1.0 | 778 | 809 | 820 | 102% |
| 18 | surf ramp (final ramp) | 18.6 m onto it, then 38 m of ramp | -1.0 m | right 90° along the ramp | 0.87 s | its face | 800 | - | 848 | - |
| 19 | block (ramp to plank) | 15.7 m | -2.5 m (fall) | sync (L/R) | 0.50 s | 8.0 x 3.0 | 946 | 1550 | 1106 | 90% |
| 20 | block (final jump) | 25.0 m | -3.0 m | sync (L/R) | 1.04 s | 3.0 x 3.0 | 898 | 996 | 1126 | 84% |
| 21 | finish | 13.1 m | +0.0 m | sync (L/R) | 0.76 s | 10.0 x 10.0 | 620 | 745 | 1037 | 60% |

