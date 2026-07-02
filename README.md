# DiscoAccess beta testing notes

DiscoAccess is a mod that makes Disco Elysium: The Final Cut playable by blind users. 
## Install

You need Disco Elysium: The Final Cut on Steam, and the .NET SDK, version 9.0.200 or later, from https://dotnet.microsoft.com/download.

1. Run `setup-bepinex.ps1`. It finds your Steam install on its own and puts the mod loader into the game folder. If it can't find the game, set the `DISCO_ELYSIUM_DIR` environment variable to the game folder and re-run.
2. Launch the game once and wait until you reach the main menu. This first launch generates files the build needs and can take a few minutes, so be patient if it seems frozen. Then quit.
3. Run `build.ps1` the same way. It builds the mod and drops it into the game folder.
4. Play.

To update: pull the new version, close the game, and re-run `build.ps1`. If a game update from Steam breaks things, re-run `setup-bepinex.ps1` first, then `build.ps1`.

## Bugs

I expect a lot of bugs, especially around the scanner and what it can and cannot see. Please feel free to bombard me with examples. Saves help a lot, especially if it's somewhere obscure.

## How everything works

WASD controls a cursor, which can move around your visible screen, blocked by geometry. When it hits the edge of your visible range, you'll hear a boop, which means it's time to click (Enter) so your character moves. The camera is slaved to your character by the game.

The cursor does not stop at all geometry. To avoid getting stuck in tight corners because of small debris and furniture, it's able to hop over meter-wide gaps.

As you move, you will hear wind tones to indicate where geometry is blocking you. You will hear sonar sounds as well, which sweep as you move every 0.4 seconds. I don't super expect that you'll find things with the sonar constantly; it's more a "there's something here" warning.

## Sounds

I haven't made a learn-game-sounds menu yet, but you can find the sounds under `assets/audio`. To summarize: pop is an orb, clink is an interactable, rattle is a lootable container, ding is an NPC, and doors sound like a doorknob being turned. The scanner plays the correct sound over something as it scrolls, which should help.

## The scanner

The scanner lets you press Page Up and Page Down to cycle through things. Ctrl+Page Up and Ctrl+Page Down filter it, though it starts in everything mode. There are also keyboard buttons that do basically the same thing: comma does NPCs and interactables, period handles containers and orbs (things you only ever click once), and slash handles exits. Hold Shift to cycle backwards.

The scanner moves the cursor directly on top of the thing you cycled to, so press Enter to interact with it. The scanner is anchored to the player character and only picks up what the player sees, so for more stuff, move the player by clicking somewhere.

I will eventually add bookmarks to the big city map.

## Cursor regions

Tell me what you think of the cursor regions. I had Claude wing it, so they're not at all perfect, but I haven't decided if I should remove them yet, as they're actually not terrible for orientation. This is what R reads, for example "plaza", "yard", etc.

## Keys

Cursor keys: WASD moves the cursor, Enter interacts with whatever it's on, C recenters the cursor on your character, Space stops walking.

Status keys: M for money, H for health, T for time, R for the region you're in.

Game keys: Ctrl+C character sheet, Ctrl+I inventory, Ctrl+T thoughts, Ctrl+J journal, F1 game help. Left arrow heals health, right arrow heals morale. 1 and 2 use the items in your left and right hands. F5 quicksaves, F8 quickloads. Escape opens the pause menu. Ctrl+L cycles the game language.

In inv: backspace interacts with the focused item instead of equipping.

Mod settings: F12.
