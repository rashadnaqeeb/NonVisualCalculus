# Changelog

Release notes for Non-Visual Calculus. Ongoing work is recorded under the Unreleased heading; the release skill retitles that section to the version being released, and create-release.ps1 reads the tagged version's section as the GitHub release notes.

## Unreleased

Bug fixes:

- Keyring keys no longer vanish when a save made in one language is loaded in another (a game bug in Arabic; a lost key that can't be repaired is announced).
- In Arabic, words ending in alef maqsura no longer read it as yaa.
- In Arabic, the thought-completion screen no longer reads its bonuses as garbled markup.

## V1.2.0

New Features and improvements:

- The bookmarks menu can now list preset bookmarks for notable locations (the Whirling, shops, the harbour, the coast from day 3); enable "Show preset bookmarks" in the mod menu.

Bug fixes:

- Triggering an orb whose icon has not appeared yet no longer sometimes skips its story effects, read state, and XP or money rewards. This was happening very rarely, and should not have affected most players.
- Mod menu sliders now respond to the left and right arrows when the menu is opened during a conversation.
- Ambient dialogue is now read in the game's language instead of English.
- In Arabic, colored game text (purchase costs, thought completion rewards) no longer reads as garbled markup, and a purchase option's price is spoken.
- In Arabic, the options menu now reads each setting's tooltip again.

## V1.1.6

New Features and improvements:

- Mod menu settings now explain what they do, spoken after the setting's state.

Bug fixes:

- In languages other than English, the pause menu now announces itself as the pause menu and offers the learn game sounds entry.
- The scanner now finds wall-mounted things hanging high above the floor, such as the pawnshop's shelf of boomboxes.
- The cursor no longer reads its bearing and distance from your character while scanner readouts are set to come from the cursor.

## V1.1.5

New Features and improvements:

- Hold Shift with WASD to move the cursor at double speed.

Bug fixes:

- The bookmark name field now accepts IME-composed text, so Chinese and other IME names can be typed.

## V1.1.4

New Features and improvements:

- Improved the mod's ability to identify doors: doors that shared one generic name now each speak a distinct one.

## V1.1.3

Bug fixes:

- Item effects now read as the game shows them: attribute bonuses, substance damage, and instruction lines were misread or skipped ("+1 None" for +1 Intellect).

## V1.1.2

Bug fixes:

- Fixed the stairs in the union office being untraversable with the cursor. Warning: they are still very narrow; the bookmark system is suggested in this room.

## V1.1.1

Bug fixes:

- A thought or paralyzer orb that pins the character can now be opened instead of every action refusing with "cutscene playing".
- Actions refused while an orb pins the character now say to interact with the orb.

## V1.1.0

New Features and improvements:

- The mod is now named Non-Visual Calculus, after the game's Visual Calculus skill.
- Updating removes the old Whirling in Words version and carries settings and bookmarks over to the new name.

## V1.0.1

New Features and improvements:

- The installer accepts the Epic Games Store version of the game.
- After an update, the installer log shows the release notes of the new versions.

Bug fixes:

- Fixed occasional dialogue freezes where every option stayed "not ready".
- The installer now asks for administrator rights and handles read-only files, fixing failed installs under Program Files.
- Durations combining hours and minutes now read correctly in languages other than English.

## V1.0.0

First release: full screen-reader access to Disco Elysium - The Final Cut.

- Speech output through Prism, following the game language for all thirteen supported languages
- Dialogue, responses, and skill checks with odds
- Menus, save/load, settings, journal, inventory, thought cabinet, and character sheet
- World navigation: movement cursor, interactable scanner, audio cues, and wall tones
- Learn-game-sounds reference on the pause menu
