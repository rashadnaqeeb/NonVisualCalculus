---
name: translate
description: Translate the mod's authored strings into one game-supported language, end to end - harvest the game's own vocabulary, decide register, draft lang/<language>.txt, validate, verify the composed lines against the live game, review, commit. Use when asked to translate the mod or add a language. Not for fixing individual words in an existing translation - that is a normal edit plus dotnet test.
argument-hint: <language>
---

Produce a complete `lang/<language>.txt` for the language given as the argument. The translation
source is `src/NonVisualCalculus.Core/Strings/Strings.cs`: every entry in the `Defaults` table carries a
comment saying where it is spoken, what fills each `{n}` slot, and which game I2 term to match.
Read the table directly; do not work from `lang/en.txt` (it has no context).

Work through the phases in order. Each depends on the one before it.

## Phase 0 - preconditions

- The game must be running with the dev server up:
  `curl -s --retry 60 --retry-connrefused --retry-delay 1 http://127.0.0.1:8771/health`.
  If it is not running, ask the user to launch it through Steam. Nothing in this skill needs a
  game restart.
- Verify the game actually has the target language (the mod follows the game language, so a file
  for a language the game lacks never loads). Check via `/eval`:
  `I2.Loc.LocalizationManager.GetAllLanguages()` - abort with that explanation if absent.
- File name, in `lang/`: the I2 code lowercased, or the language name lowercased with word runs
  hyphenated (see LanguageSync.FindFile). The game's languages, their file stems, and their
  `_plural` rules (confirmed against the live game; re-check after a game update):
  - Chinese: `zh.txt`, rule `one`
  - Traditional Chinese: `zh-tw.txt`, rule `one`
  - Spanish: `es.txt`, rule `english`
  - Korean: `ko.txt`, rule `one`
  - Portuguese (Brazil): `pt-br.txt` (or `portuguese-brazil.txt`), rule `french`
  - French: `fr.txt`, rule `french`
  - German: `de.txt`, rule `english`
  - Russian: `ru.txt`, rule `slavic`
  - Polish: `pl.txt`, rule `slavic`
  - Japanese: `ja.txt`, rule `one`
  - Turkish: `tr.txt`, rule `one`
  - Arabic: `ar.txt`, rule `arabic`

## Phase 1 - harvest the game's vocabulary

Collect every I2 term named in a "match I2" comment in Strings.cs, then dump their values in the
TARGET language in one `/eval` sweep.

**DE streams one language at a time, so you MUST switch the game to the target language to read
it.** Each I2 source holds only the current language's column in memory (`src.mLanguages.Count`
is 1 per source), and DE loads a language's UI data from asset bundles on demand - so reading
another language's `GetTermData(...).Languages[i]` or a bare `GetTranslation` returns null/empty
until that language is loaded. Setting `LocalizationManager.CurrentLanguage` alone does NOT load
it. The two-call load that works (found by decompiling `LocalizationSettingsOption.SwitchLanguageSource`):

```csharp
// I2 language name + code from the Phase 0 table (e.g. "French"/"fr").
I2.Loc.LocalizationAssetBundlesManager.Instance.LoadBundlesForLanguage("French");
I2.Loc.LocalizationManager.SetLanguageAndCode("French", "fr", false, true);
// Then read terms with the mod's own GetTranslation signature (fixForRTL off):
string G(string t) => I2.Loc.LocalizationManager.GetTranslation(t, false, 0, true, false, null, null, true);
var terms = new[] { "TOOLTIP_TUTO_CHECK_WHITE_OPEN", "TOOLTIP_TUTO_CHECK_RED", /* ...the rest */ };
foreach (var name in terms) Console.WriteLine(name + " = " + (G(name) ?? "<null>"));
```

This also puts the game into the target language, which Phase 5 needs anyway - so harvest and the
listen phase share one switch; there is no separate "switch later" step. Caveat: `LoadBundlesForLanguage`
loads the UI localization ("lockit") bundles but NOT the Pixel Crushers dialogue database, so live
barks and conversation lines stay in the language the save booted in during preview. That is expected
and does not affect reviewing authored UI strings (which never read dialogue). Phase 5 restores the
boot language when the work is done.

Never fetch with RTL fixing on. For the district names, also grep the target-language column of
the dialogue terms for the place names (best effort; where the game never names a place, translate
the meaning given in the table comment).

These harvested values are the terminology the whole draft must agree with. Use them live from the
game each run; do not save them to a reference file that can go stale across game updates.

## Phase 2 - register

Decide the register BEFORE drafting, not per-string.

- Default rule: match the register of the game's own localization in this language (the harvest
  shows it - tu/vous, du/Sie, sentence style).
- For a diglossic language, or any language where the written standard and the spoken language
  diverge enough that a TTS voice reading one sounds wrong to a speaker of the other (Arabic is
  the standing example: Modern Standard Arabic versus dialect), the game's localization cannot
  settle it alone - these strings are HEARD, not read. Ask the user which variety and formality
  to use before writing anything (AskUserQuestion; recommend MSA for Arabic - TTS voices and
  cross-dialect intelligibility favour it - but the user decides).
- Record the decision in a `#` comment at the top of the file so the review pass and any future
  session can see it.

## Phase 3 - draft

Write the whole file in one pass, top to bottom, reading each entry's comment in Strings.cs.

- First line after the header comment: `_plural = <rule>` (the rule from the Phase 0 table).
  Form counts: `one` selects a single form, `english` and `french` two, `slavic` three (one,
  few, many), `arabic` up to six (zero, one, two, few, many, other). Fewer forms than the rule
  selects is legal - selection clamps to the last.
- Keep every `{n}` slot; place them wherever the language wants. `|` separates plural forms
  under the declared rule; `WorldCompass` is an ordered list of exactly eight bearings;
  `ContainerWord_*` is always `singular|plural` regardless of rule.
- Screen-reader register: terse, no fluff, lowercase where the English is lowercase, plain
  punctuation only (no emdashes or typographic quotes - the reader voices them).
- "Match I2" keys use the harvested vocabulary, inflected as the sentence needs.
- Write values in logical character order (normal typing order), including for RTL scripts.
- The literal spaces and commas inside the templates are ENGLISH punctuation, not part of the slot:
  `WorldExitNamed`, `WorldLocation`, `WorldScanCategoryCount` and `WorldOrbNamed` join two pieces,
  and a CJK translation should join them with its own punctuation (or none) rather than carrying the
  English space over. Traditional Chinese is not a character conversion of `zh.txt`: harvest the
  game's own zh-TW column and let it arbitrate vocabulary, then diff against `zh.txt` for keys the
  game does not cover.
- **Gendered languages: never inflect a value for a noun the runtime supplies.** Several values are
  spoken appended after a noun the mod does not choose - an item's name, an equipment slot's caption,
  a health bar's name, a scanner category word - and those nouns differ in gender, so no single
  inflection agrees with all of them. Give such a value its own head noun ("a new item", "an empty
  slot") or an invariant phrasing ("at a critical level"), never a bare adjective. The exposed keys
  are `InventoryFresh`, `InventorySlotEmpty`, `WorldScanCategoryEmpty`, and `CrisisHealLeft`/`Right`
  (both bar names, e.g. Saúde feminine and Moral masculine). Two look like the same trap and are not:
  `StatusOpen` speaks only after a door, and `StatusHasSomethingToSay` only after Kim, so each may
  agree with that one referent - the table's comments say so.

## Phase 4 - validate

`dotnet test NonVisualCalculus.slnx`. `LanguageFileTests` gates the file mechanically (unknown keys,
dropped or invented slots, form counts, the compass). Fix until green.

## Phase 5 - verify against the live game

The mod's loading, hot-reload, input, and speech plumbing are known-good infrastructure; driving
the game to watch them work again proves nothing about the translation. Two things, and only two,
need the live game. Do those; do not press keys to re-observe the mod running.

Deploy first: LanguageSync reads the DEPLOYED copy of the file
(`<PluginPath>/NonVisualCalculus/lang/` - read `BepInEx.Paths.PluginPath` via `/eval` rather than
hardcoding a Steam path), not the repo's, and with the game running a full build cannot refresh
the deploy (locked DLLs). So each iteration is: edit the repo file, copy it into the deployed
lang folder, `POST /reload` (module recreation re-runs LanguageSync). The game is already in the
target language from the Phase 1 load; if a restart intervened, redo that `LoadBundlesForLanguage`
+ `SetLanguageAndCode` pair (setting `CurrentLanguage` alone will not load the data).

**5a - the composed lines.** A value in isolation can be right while the line it builds is wrong:
slots land in the wrong order, a counter word disagrees with its number, a template's punctuation
collides with a game string. Read these by CALLING the Core accessors in one `/eval` sweep with
representative arguments - not by driving the game into each situation, which reaches the same
`string.Format` the long way and can't cover the edge values anyway. Cover at least:

- `WorldCompass(0..7)`, `WorldDistance(0)` and `WorldDistance(n)`, `WorldHere`, `WorldAbove`
- `ExitNamed` with both a place name and a `FloorNumber`/`WorldBasement`, and `WorldLocation`
  with and without a floor (these two templates carry word order)
- `WorldMoney`, `ItemValue`, `PawnshopMoney` (decimal mark and currency placement)
- `WorldHealth`, `CrisisHeal` both directions, `WorldExperience` (many slots, bar-name agreement)
- every counted accessor at each form its `_plural` rule selects: `SkillPoints`, `Duration`
  (0, minutes only, hours only, both), `HealCharges`, `ItemUses`, `ThoughtResearching`, `Percent`,
  `Step`, `Milliseconds`
- `WorldScanCategoryCount` with a nonzero count and with 0 (which routes to the empty template),
  `OrbNamed`, `ContainerWord` for a two-word token and for the uncountable `money`
- in a gendered language, every line whose slot takes a runtime noun, at BOTH genders: `CrisisHeal`
  with each bar name, and `WorldScanCategoryCount(cat, 0)` for a masculine and a feminine category
  (pass the values, e.g. `WorldScanExits` and `WorldScanOrbs`, not invented strings)
- pass game-sourced strings from I2 rather than typing them as C# literals into the `/eval` body:
  non-ASCII in the request body can arrive mangled and will look like a translation bug that is not

Read the output as a speaker of the language. For RTL languages confirm the composed line is in
logical order, not visually reordered.

**5b - authored nouns versus the game's own words.** This is where the real defects are, and the
only check that genuinely needs the running game. Several keys name a thing the game ALSO names
somewhere - in a dialogue node title, an examine line, or an I2 term the harvest did not cover.
If the two disagree, the player hears one word from the mod and another from the game for the same
object. The `WorldPlace*` keys and the type words (`WorldThing*`, `ContainerWord_*`) are the
exposed ones, because they were authored precisely where a term lookup was unavailable.

So: for each authored noun, search the target-language game text for what the game calls that
thing, and adopt its wording.

**The game's text is in I2 sources, so this is a table lookup, not a grep of the disk.**
`LocalizationManager.Sources` holds a group of sources per loaded language. Two facts make the
search easy. Each source carries only ONE language column, so a term's language is decided by which
source you read it from; and `GetTermData` on a source needs only `LoadBundlesForLanguage`, not a
language switch, so you can hold English and the target side by side.

Enumerate the sources first and print each one's index, `mLanguages` names, and `mTerms.Count` -
the indices move as languages load, so never hardcode them from a previous run. Each language group
has two sources that matter, and they are NOT interchangeable:

- The **named-term source** (~5k terms) holds everything addressable by name: `Actors/<id>/Name`,
  `Conversation/<id>/Title` and `/Description`, `Items/<id>/Description`, `Area Names/<scene>`,
  `Thoughts/`, `Skills/`, plus the UI terms Phase 1 harvested. This is where the English-to-target
  lookup happens, because the English column is here too.
- The **dialogue-text source** (~70k terms) holds the spoken lines, keyed `Dialogue Text/0x…` (plus
  `AlternateN`, `tooltipN`). The keys are HASHES: they carry no English, so you cannot look a thing
  up by name here. Search it by VALUE, in the target language, and only for prose the named terms
  do not cover - an examine line, a bark that names an object.

Search the ENGLISH named-term source for a term whose VALUE names the thing, then read that same
term out of the target's named-term source:

```csharp
var en = I2.Loc.LocalizationManager.Sources[2], tgt = I2.Loc.LocalizationManager.Sources[40]; // from the enumeration
string T(string term) { var td = tgt.GetTermData(term); return td?.Languages[0]; }
// scan en.mTerms for Languages[0].Contains("Union"), print td.Term + T(td.Term)
```

**Search with ASCII-only needles.** Non-ASCII in an `/eval` request body can arrive mangled, so a
needle typed in the target language silently matches nothing - and a zero hit reads as "the game
never names this thing", which is the exact wrong conclusion. Cut the needle back to its
diacritic-free substring (`sekretar`, `kontener transportow`) or compare against a string fetched
from the game.

Real finds: `Actors/.../Name` "Cargo Container Door" is `Porta do Container de Carga`, so pt-BR
takes the game's *container*, not the dictionary's *contêiner*; an item description mentions the
`escritório do Sindicato dos Estivadores`, naming `WorldPlaceUnionOffice`. Confirm the authored
name is really needed by reading `Area Names/<scene>-int` in the target language: the three harbour
interiors all return one word (`Porto`), which is exactly why those keys exist. Where a place has a
dialogue that fires on it, `GET /nav` while that dialogue is open shows the node title.

Where the game never names the place itself, it may still name the person or object the authored
name is built from, and that settles an inflection the English hides: the harbour secretary is a man
in the script (Polish `sekretarz`), so `WorldPlaceSecretaryOffice` cannot be built on a feminine
noun. Search for the referent, not only the phrase. And where two game phrasings exist, take the one
the key's comment points at: the yard container is `kontener transportowy` in the examine line and
`kontener towarowy` in a line of Kim's, and the comment names the examine line.

For a `ContainerWord_*` token the game names nothing, so ask the scene instead: the objects a token
catches decide the word. Sweep `Resources.FindObjectsOfTypeAll<Sunshine.ContainerSource>()` and
bucket `gameObject.name` by token. **Bucket by WHOLE TOKEN, the way `EntityNaming.FindContainerType`
matches**: split the name on whitespace, underscore and hyphen, then compare each token (with a
regular `-s`/`-es` stripped) against the type words, after first checking for the adjacent pair
`trash can`. There is no longest-substring rule, so `can` never matches inside `canister` or
`trashcan`, and `cup` never matches inside `cupboard`. Bucketing by SUBSTRING instead invents props
a token does not actually catch - every cupboard in the game lands under `cup` - and also drowns the
real props in UI objects (`Inventory` contains `vent`, `Copotype` contains `pot`).

Their scene and parent path is the evidence: `can` catches objects under `Containers/Plaza Tare` and
`Yard Tare`, returnable deposit tare, so it is a drink can and a word meaning *canned food* is wrong
even though it renders "tin can" faithfully; `pot` catches one `Flower Pot`; `locker` catches
`Sea-fortress-int` steel lockers while `cabinet` catches an office cabinet, which is what keeps them
two words in a language that would otherwise collapse them; `vent` catches boardwalk wall vents, so
it is the object, not the building's ventilation system. A token that catches nothing in the loaded
scene keeps the sense its table comment states.

Fix what is wrong; each fix is edit, copy, `/reload`, re-read. Restore the boot language LAST (the
`LoadBundlesForLanguage` + `SetLanguageAndCode` pair, back to English) so the game is not left in a
mixed UI / dialogue-language state: LanguageSync follows the game language, so once the game is back
on English a `/reload` drops the file and the accessors answer in English again.

## Phase 6 - fresh-eyes review

Spawn ONE subagent with clean context (general-purpose). Give it the finished file and point it
at Strings.cs; instruct it to compare each entry against the English value, the comment, and the
declared register, and to report only entries that are wrong, misleading, register-breaking, or
drop information - explicitly not stylistic preferences. Name the agreement class explicitly (an
authored word that must agree with a noun the runtime picks) and hand it the harvested vocabulary,
so it judges against the game rather than its own taste. Apply the fixes you agree with, re-run the
tests, and re-read any composed line a fix touched.

A reviewer that reads the composition sites in the module may report a key as broken that the code
proves safe (`StatusOpen` fires only on doors). Check the caller before rewording on its word.

## Phase 7 - commit

One commit with the new file (repo message style, e.g. "Strings: French translation").
