# Non-Visual Calculus

A screen-reader accessibility mod for **Disco Elysium: The Final Cut**, for blind and visually-impaired players. It speaks the game's dialogue, menus, and screens, adds keyboard navigation over the whole UI, and makes the world itself playable with a movement cursor, a categorized scanner of everything around you, and audio cues for geometry and objects.

## What works and what doesn't

Everything needed to play the game start to finish works.

The only thing that does not work is the game's collage mode, which is used to take screenshots and contains a few Easter eggs. This will never be accessible.

## Requirements

- Disco Elysium: The Final Cut on **Windows**, from Steam, GOG, or Epic. The console and macOS versions cannot load mods.
- A screen reader (NVDA, JAWS, and others) or the built-in Windows voices.

## Install and update

1. Download the installer: **[NonVisualCalculusInstaller.exe](https://github.com/rashadnaqeeb/NonVisualCalculus/releases/latest/download/NonVisualCalculusInstaller.exe)**
2. Run it. It finds your game folder on its own (Steam, GOG, or Epic; you can browse to it if needed) and downloads and installs the latest release.
3. Launch the game through Steam, GOG Galaxy, or the Epic Games Launcher. The first launch takes a few minutes while the mod loader generates files, so be patient if it seems frozen. This is only a one-time issue.

To **update** later, run the installer again.

## How the mod works

### Menus and dialogue

Menus speak as you move through them. Arrow keys navigate, Tab and Shift+Tab move between panels, Enter activates, Escape backs out, and Backspace is the secondary action (in the inventory it interacts with the focused item instead of equipping it). You can also just start typing to search the current panel. In dialogue, the mod reads each line as it arrives, then your response options, including the success odds on any skill-check option.

### The cursor

WASD moves a cursor around the world, blocked by walls and geometry. To avoid getting stuck on small debris and furniture, it can hop over meter-wide gaps. Enter clicks whatever the cursor is on: your character walks over and interacts. Backspace walks to the cursor without interacting, C recenters the cursor on your character, and Space stops walking.

As you move you hear wall tones where geometry blocks you, and a sonar that sweeps your surroundings, placing a typed sound on each nearby object. The cursor can also glide past the edge of what your character currently senses, into unexplored ground, with a sound marking each crossing in and out; a setting restricts it to the sensed area if you prefer. Since the camera follows your character, moving your character reveals more of the map. This means that once your cursor reaches the unsensed area, you should move your character again to start detecting new objects. Think of it a bit like fog of war.

### The scanner

The scanner is a second point of attention, separate from the cursor: it cycles through everything your character can perceive and tells you what is there and how far, without moving anything. Page Down and Page Up cycle through objects, and Ctrl+Page Down and Ctrl+Page Up switch category (it starts on everything). Three quick-nav keys jump straight to a group: comma for people and interactables, period for containers and orbs (things you only ever click once), and slash for exits. Hold Shift on any of these to cycle backwards.

To act on what the scanner just read: I walks over and interacts with it, J moves the cursor to it, and P speaks the walking direction toward it (the first leg of the actual path, not the straight line, so it takes walls into account). By default distances and directions are read from the cursor; a setting switches them to read from your character instead.

### Sounds

The pause menu has a **learn game sounds** entry that plays every sound the mod uses with its meaning. In short: a pop is an orb, a clink is an interactable, a rattle is a lootable container, a ding is a person, and doors sound like a doorknob turning. The scanner plays the matching sound as it cycles over each object.

### Bookmarks

Ctrl+B opens the bookmarks menu. Adding a bookmark saves your character's current position on the current map under a name you choose. The menu lists each bookmark with its live distance and whether it is currently reachable; Enter walks you there, and each entry can also be deleted.

### Mod settings

F12 opens the mod's settings anywhere. You can toggle automatic dialogue reading and ambient dialogue, adjust wall tone and sonar volume, make either continuous, set the sonar sweep interval, choose which categories the sonar includes, switch scanner readouts between cursor and character, make your character run instead of walk, and restrict the cursor to the sensed area.

## Keys

Shift+F1 opens a key help screen from anywhere that lists every key in context.

Cursor: WASD moves the cursor, Enter interacts with whatever it is on, Backspace walks to the cursor without interacting, C recenters the cursor on your character, Space stops walking.

Scanner: Page Down and Page Up cycle, Ctrl+Page Down and Ctrl+Page Up switch category, comma for people and interactables, period for containers and orbs, slash for exits, Shift reverses. I interacts with the scanned object, J moves the cursor to it, P speaks the walking direction toward it.

Status: M for money, H for health, T for time, R for the map you are on (also announced automatically when it changes), X for experience and skill points.

Game: Ctrl+C character sheet, Ctrl+I inventory, Ctrl+T thought cabinet, Ctrl+J journal (the map is a tab inside it), F1 game help. Left arrow heals health, right arrow heals morale. 1 and 2 use the items in your left and right hands. F5 or Alt+S quicksaves, F8 or Alt+L quickloads. Escape opens the pause menu. Ctrl+L switches the game language to the secondary language, used for language learning.

Mod: F12 settings, Ctrl+B bookmarks, Shift+F1 key help.

## Controller

A gamepad works alongside the keyboard; every pad control mirrors a keyboard key. Button names below are PlayStation first, Xbox in parentheses.

In the world: the left stick moves the cursor. Cross (A) interacts like Enter, triangle (Y) walks to the cursor without interacting like Backspace, circle (B) recenters the cursor on your character like C, and square (X) opens the character sheet. The bumpers cycle the scanner, left going backwards. The left trigger moves the cursor to the thing the scanner last read, like J, and the right trigger walks over and interacts with it, like I. Clicking the left stick uses your left-hand item like 1, clicking the right stick uses your right-hand item like 2. On the dpad, left and right heal health and morale like the arrow keys, and down stops walking like Space. Flicking the right stick speaks a status readout: up for health, right for money, down for time, left for experience. Start opens the pause menu.

In menus and dialogue: the left stick or dpad navigates, cross (A) activates like Enter, circle (B) backs out like Escape, and triangle (Y) is the secondary action like Backspace. The bumpers step between controls like Shift+Tab and Tab. On the character sheet, inventory, journal, and thoughts screens, the triggers switch between those screens, matching the game's own controller layout. In dialogue, dpad left and right still heal, and the right stick still reads status.

## Reporting bugs

Please report anything that speaks wrong, goes silent, or gets stuck on the [issue tracker](https://github.com/rashadnaqeeb/NonVisualCalculus/issues). The most useful reports say where you were, what you pressed, and what you heard versus what you expected. A save file placed near the problem helps enormously, especially for scanner and world-navigation issues.

## Building from source (developers)

You need Disco Elysium: The Final Cut on Steam and the [.NET SDK](https://dotnet.microsoft.com/download), version 9.0.200 or later.

1. Run `setup-bepinex.ps1`. It finds your Steam install on its own and puts the mod loader into the game folder. If it cannot find the game, set the `DISCO_ELYSIUM_DIR` environment variable to the game folder and re-run.
2. Launch the game once and wait until you reach the main menu; this generates files the build needs. Then quit.
3. Run `build.ps1`. It builds the mod and deploys it into the game folder.
4. Play.

To update: pull, close the game, and re-run `build.ps1`. If a game update from Steam breaks things, re-run `setup-bepinex.ps1` first, then `build.ps1`.

## Credits

Zack Kline, who designed the original Disco Elysium accessibility mod, the first ever access mod created for a unity game using AI. It introduced me to the game which immediately became one of my favorites of all time. You can find it [here](https://github.com/BlindGuyNW/disco-accessibility).

Thanks to Chaosbringer216, who designed the concept of the cursor, sonar, and wall tones used within the mod. Check out his project for Pathfinder: Wrath of the Righteous once released, where you can see a much more mature version of this system in play.
