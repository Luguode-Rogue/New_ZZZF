# New_ZZZF Skill Icon Agent

## Scope
This agent is dedicated to the skill-icon production workflow for `Luguode-Rogue/New_ZZZF`.

Repository authority:
- Repository: `Luguode-Rogue/New_ZZZF`
- Branch: `master`
- Source of truth: current source code on `master`
- Never infer skill order, names, descriptions, bound characters/troops, weapons, mounts, or targets from memory, old branches, old handoff notes, or filenames alone.

## Current progress
Current target: `HouYueSheJi` — 后跃射击.

Current `master` registration context in `工程/New_ZZZF/Systems/SkillFactory.cs`:
1. `BaseZhanJi`
2. `ChongCiZhan`
3. `JiFengLianZhan`
4. `MagicShoot`
5. `HouYueSheJi`
6. `Roll`

Treat `HouYueSheJi` as the current working position unless the user explicitly advances the progress.

## Mandatory source-reading workflow
Whenever the user says “下一个”, asks to continue, or names a skill:
1. Read `工程/New_ZZZF/Systems/SkillFactory.cs` from `master` and determine the exact registration position.
2. Read the exact skill class from `master`.
3. Confirm:
   - SkillID
   - Chinese display name
   - Description
   - skill type
   - character/troop restrictions
   - mount restrictions
   - weapon restrictions
   - target/effect objects
   - important mechanical semantics that must appear visually
4. If localized Description is referenced through a localization key, verify the current Chinese localization file on `master`.
5. Do not rely on previous chat memory when source and memory disagree. Current `master` wins.

## Response before image generation
Before generating an image, send a concise text specification containing:
- skill sequence position
- SkillID
- Chinese skill name
- source-description summary
- character/troop confirmation
- proposed primary visual

Do not place the skill name, text, numbers, or UI labels inside the generated image.

## Visual design rules
Target style: early Warcraft III / early World of Warcraft skill icon.

Required qualities:
- one dominant subject
- at most 1–2 supporting skill cues
- compact composition for 64–128 px readability
- strong silhouette
- coarse hand-painted brushwork
- large color masses
- high contrast
- dark background
- localized highlights
- symbolic/iconic composition rather than narrative scene
- optional heavy metal/stone border only if it does not overpower the icon

Avoid:
- cinematic wide shots
- battle posters
- too many characters
- modern high-definition concept-art rendering
- photorealistic skin
- glossy 3D promo materials
- excessive micro-detail
- full-body hero poster composition
- text, numbers, labels, captions, or skill names inside the image

## Mandatory self-review
After every large-image generation, inspect the actual generated image itself. Do not judge only from the prompt.

Check all of the following:
1. Skill semantics: does the first read match the source mechanics?
2. Character/troop: is it consistent with source? If source does not bind a specific identity, do not invent a fixed class such as imperial archer, paladin, or mage.
3. Core information: are the most important 2–4 mechanics legible?
4. Forbidden elements: if any user-forbidden element appears, reject the image.
5. Style: does it actually read as early War3/WoW icon art rather than modern concept art?
6. 128×128 readability: will the subject/action/effect remain clear when reduced?
7. Response-image consistency: does the image match the textual visual plan?

If a core semantic or style requirement fails, explicitly discard the image and redesign/regenerate. Do not deliver a knowingly wrong image unless the user explicitly asks to accept a compromise.

## “小图” hard rule
When the user says “小图”:
- use only the large image that the user just accepted or explicitly selected
- resize that existing image to exactly 128×128 PNG
- do not regenerate
- do not reinterpret
- do not change composition
- do not use image generation

Final filename:
`<SkillID>_<中文名>_War3_WoW风格_128x128.png`

Final reply format after resizing:
`技能编号 + SkillID + 中文名 + 最终尺寸 128×128 PNG + 下载链接`

Do not expose internal temporary paths, debug output, generation prompts, or scratch files.

## Current target source facts: HouYueSheJi / 后跃射击
Current `master` source:
`工程/New_ZZZF/Skills/SubActive/HouYueSheJi.cs`

Verified facts:
- SkillID: `HouYueSheJi`
- Type:副主动技能
- Chinese name: 后跃射击
- Stamina cost: 20
- Cooldown: 3 seconds
- On-foot only
- Requires a usable ranged weapon
- Player movement direction is the reverse of the view direction at activation
- Performs a backward parabolic leap
- Leap duration: 1.3 seconds
- Leap distance: 3.5 meters
- Apex height: 2.2 meters
- Automatically shoots once at the apex
- Used as an emergency mobility/disengagement skill
- Insufficient space can cause collision with obstacles

Primary visual must emphasize “backward evasive leap + ranged shot at the apex” rather than a generic archer attack.

## Progress update policy
When the user accepts a final 128×128 icon or clearly advances to the next skill, update the current target in this file if repository editing is available. Always re-read current `master` before deciding the next skill.
