Set-Location D:\project\wingezi-p1c
git add -A
$msg = @'
feat: mirror the elevated-window hotkey notice onto the search hotkey card

The search hotkey is subject to the same Windows UIPI limitation as the
global hotkey (an elevated foreground window blocks the registration),
so its settings card gains the same informational note, reusing the
existing 12-language keys. Also records the notice in the changelog's
Unreleased section. The 12-language key parity is already enforced by
JsonLocales_HaveIdenticalKeysValuesAndPlaceholders, which the audit
missed.
'@
[System.IO.File]::WriteAllText('D:\project\wingezi-p1c\commit-msg.txt', $msg, (New-Object System.Text.UTF8Encoding($false)))
git commit -F D:\project\wingezi-p1c\commit-msg.txt
Remove-Item commit-msg.txt -ErrorAction SilentlyContinue
$ok = $false
foreach ($mode in @("direct", "proxy")) {
    for ($i = 1; $i -le 4; $i++) {
        if ($mode -eq "direct") {
            git -c http.proxy= -c https.proxy= push -u origin chore/search-notice-parity 2>&1 | Select-Object -Last 1
        } else {
            git push -u origin chore/search-notice-parity 2>&1 | Select-Object -Last 1
        }
        Start-Sleep 5
        $tracked = git ls-remote --heads origin chore/search-notice-parity 2>$null
        if ($tracked) { Write-Output "PUSHED via $mode"; $ok = $true; break }
        Write-Output "$mode attempt $i failed"
        Start-Sleep 15
    }
    if ($ok) { break }
}
if ($ok) {
    $body = @'
## Summary

- Mirrors the elevated-window hotkey limitation notice onto the search hotkey card (the search hotkey hits the same Windows UIPI limitation); reuses the existing 12-language keys, no new strings.
- Records the notice in the changelog Unreleased section (bilingual).
- The 12-language key-parity guard the external audit asked for already exists (`JsonLocales_HaveIdenticalKeysValuesAndPlaceholders`: locale set, key sets vs en-US, non-empty translations, placeholder indexes) - no duplicate test added; the audit missed it.

## Validation

- Full suite 4,487/4,487; build 0 errors.
'@
    [System.IO.File]::WriteAllText('D:\project\wingezi-p1c\pr-s.md', $body, (New-Object System.Text.UTF8Encoding($false)))
    gh pr create --base main --head chore/search-notice-parity --title "Mirror the elevated-window hotkey notice onto the search card and record the changelog entry" --body-file D:\project\wingezi-p1c\pr-s.md
    Remove-Item pr-s.md -ErrorAction SilentlyContinue
}
Remove-Item D:\project\wingezi-p1c\push-search-notice.ps1 -ErrorAction SilentlyContinue
