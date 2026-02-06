Create a new release for CodexBar. The argument is the version bump type or explicit version number.

Usage: /release <patch|minor|major|vX.Y.Z>

Steps to perform:

1. **Determine version**: Look at the latest git tag to find the current version. Bump according to the argument:
   - `patch` → increment patch (e.g. v1.5.1 → v1.5.2)
   - `minor` → increment minor, reset patch (e.g. v1.5.1 → v1.6.0)
   - `major` → increment major, reset minor+patch (e.g. v1.5.1 → v2.0.0)
   - `vX.Y.Z` → use the explicit version provided

2. **Build**: Run `dotnet build` to make sure everything compiles. Stop if it fails.

3. **Update CHANGELOG.md**: Move everything under `## [Unreleased]` into a new version section `## [vX.Y.Z] - YYYY-MM-DD` (today's date). If there's nothing under Unreleased, ask the user what changes to document. Keep the empty `## [Unreleased]` heading at the top.

4. **Commit**: Stage CHANGELOG.md and any other modified tracked files. Commit with message: `docs: update changelog for vX.Y.Z`

5. **Push**: Push the commit to the remote.

6. **Tag and push tag**: Create tag `vX.Y.Z` and push it. This triggers the GitHub Actions release workflow.

7. **Confirm**: Show the user the tag name and remind them the GitHub Actions workflow will build and publish the release.

Important:
- Do NOT add Co-Authored-By lines to commits (per CLAUDE.md)
- Follow Keep a Changelog format with Added/Changed/Fixed/etc. sections
- Use semantic versioning
