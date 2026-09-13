<!--
  Pull request template — also read CONTRIBUTING.md.
  The PR title must follow the Angular commit convention: <type>(<scope>): <summary>.
  For title: "[Bug] island disappears when music starts"
-->

## What and why

<!-- What changed, and the problem it solves. Link the issue if there is one. -->

Closes #

## How it was verified

<!--
  Evidence, not promises:
  - build:  dotnet build -c Release /p:TreatWarningsAsErrors=true   (0 warnings)
  - run:    the debug flags you used (--demo / --demo-music / ...)
  - log:    relevant lines from %TEMP%\EndfieldCharge\log-YYYYMMDD.txt (must not contain ERROR)
  - tests:  dotnet test tests/EndfieldCharge.Tests   (if logic changed)
-->

## Screenshots / recordings

<!-- Required for anything visual (island states, menus, settings). Before / after. -->

## Checklist

- [ ] PR title follows the Angular convention (`<type>(<scope>): <summary>`)
- [ ] Built with `/p:TreatWarningsAsErrors=true` — no new warnings
- [ ] Actually ran the app and described what I saw above
- [ ] Pure-logic changes (state machine, geometry math, localization) come with unit tests
- [ ] `README.md` updated if behavior, flags or layout changed
- [ ] No build output, logs or IDE files committed (`bin/`, `obj/`, `publish/`)
- [ ] No secrets, tokens or personal paths committed
