# Momentary Momentos — Warm Keepsake Redesign

This working copy applies the approved cream/tan/olive + pink/purple/cyan visual direction to the MAUI app while preserving the existing data/services architecture.

## What changed

- Replaced the dark navy "aero glass" theme with a warm cream/tan/olive keepsake palette in shared resources.
- Added the real `logo.png` to the redesigned brand surfaces.
- Home now uses matching cream circular Capture / Relive / Upload actions with pink, violet, and cyan rings.
- Featured and recent momentos use cream Polaroid / paper-style cards.
- Shell/tab bar moved to the same cream/olive palette; Relive uses the dice icon.
- Relive no longer auto-selects a memory when the tab opens. The user rolls a dice to surface a random momento.
- Random Relive opens a guided flow: **Title → Date → Tags → Caption → Save**.
- Guided Relive writes title, caption, tags, and `date_captured` back to Supabase.
- After save, the momento appears in a cream Polaroid with icon actions for favorite, edit tags/details, see less, delete, and share/save.
- Auth, Capture, Profile, Help, Subscription, Admin, Tag Management, Trim, Forgot Password, Register, and Splash inherit the warm redesign through shared styles/backgrounds.
- Added `dice.svg`, `icon_upload.svg`, and a Relive progress converter.

## Important validation note

All XAML/XML files were checked for well-formed XML and all StaticResource references resolve to a resource key in this project. The current ChatGPT runtime does not have the .NET/MAUI SDK installed, so a full `dotnet build` / device run could not be performed here. Run the project in Visual Studio / Rider / your normal MAUI build environment before merging to production.
