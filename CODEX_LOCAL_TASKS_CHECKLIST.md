# Remaining Local Release Tasks

These tasks can be completed in the repository without direct access to
Google Play Console, the production Supabase project, or a physical device.

## Store listing package

- [x] Draft the Google Play short description.
- [x] Draft the Google Play full description.
- [x] Recommend the app category and relevant tags.
- [x] Identify the repository's support email and draft support-contact text.
- [ ] Confirm with the owner that the support and privacy mailboxes are monitored.
- [x] Draft release notes for version `1.0` (`5`).
- [ ] Save the approved listing copy under `docs/`.

## Store artwork and screenshots

- [ ] Create a 1024 x 500 Google Play feature graphic.
- [ ] Verify the feature graphic meets Play asset requirements.
- [ ] Prepare a phone screenshot capture plan.
- [ ] Draft captions and recommended ordering for each screenshot.
- [ ] Document which screens must avoid private or test-user data.

## Play Console declaration drafts

- [ ] Audit application data collection and sharing against the source code.
- [ ] Draft the Google Play Data safety answers.
- [ ] Draft the App access declaration.
- [ ] Draft the Ads declaration.
- [ ] Draft the Target audience declaration.
- [ ] Draft the account-deletion declaration.
- [ ] Clearly mark answers that need owner or Play Console confirmation.

## Release automation and verification

- [ ] Add a release-verification script.
- [ ] Verify package ID, application name, version, and target SDK.
- [ ] Verify the signed AAB exists and passes signature validation.
- [ ] Calculate and report the AAB SHA-256 checksum.
- [ ] Check NuGet packages for known vulnerabilities.
- [ ] Detect obsolete paid-export product references.
- [ ] Document how to run the verification locally.

## Code quality and testing

- [x] Add focused tests for the account-deletion request where practical (6 passing tests).
- [ ] Add focused tests for the billing product configuration where practical.
- [ ] Document any release flows that still require manual testing.
- [ ] Review current Android build warnings and classify their release risk.

## Release configuration review

- [ ] Review whether ARM64-only distribution is appropriate.
- [ ] Recommend any additional Android architectures.
- [ ] Verify version code `5` locally and document when it must be incremented.
- [ ] Review release signing configuration without exposing secrets.

## Documentation cleanup

- [ ] Find older deployment documentation that mentions `data_export_pack`.
- [ ] Remove obsolete paid-export setup instructions.
- [ ] Make the app name consistently `Momentary Momentos` where it represents
  the customer-facing product name.
- [ ] Reconcile duplicate or superseded Android deployment checklists.
- [ ] Prepare exact Supabase Edge Function deployment commands.
- [ ] Prepare an end-to-end account-deletion validation procedure.

## Requires external access after local work

- [ ] Obtain owner approval for store copy, artwork, and declarations.
- [ ] Publish the updated privacy and account-deletion web page.
- [ ] Deploy and test the Supabase Edge Functions.
- [ ] Enter the approved listing and declarations in Play Console.
- [ ] Upload the signed AAB to Internal testing.
- [ ] Capture final screenshots from the release build.
- [ ] Complete physical-device and Google Play installation testing.



