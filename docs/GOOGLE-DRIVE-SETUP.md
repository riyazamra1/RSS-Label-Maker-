# Google Drive setup for RSS Label Maker

RSS Label Maker keeps its SQLite product database on the Windows PC. Google Drive is an optional backup destination. RSS Core stores the RSS account and license details only; product records and Google OAuth credentials are not sent to RSS Core.

## One-time Google Cloud setup

1. Open Google Cloud Console and select or create a project owned by Razeen Secure Solution.
2. Enable **Google Drive API** for that project.
3. Configure the OAuth consent screen. While the app is in Testing, add the Google accounts that will test it as test users.
4. Create an OAuth client ID with application type **Desktop app**.
5. Download the OAuth client JSON. Do not commit this file to GitHub or send it to RSS Core.
6. On each Windows PC, create this folder if it does not exist:

   `%LOCALAPPDATA%\Razeen Secure Solution\RSS Label Maker\`

7. Save the downloaded JSON as:

   `%LOCALAPPDATA%\Razeen Secure Solution\RSS Label Maker\google-drive-oauth.json`

8. Open RSS Label Maker → RSS Account → Cloud backup & license → **Connect Google Drive** and complete Google's browser sign-in and consent.

## Storage and privacy

- Backups are stored in the user's visible Google Drive folder **RSS Label Maker Backups**.
- The app uses the `drive.file` scope. It creates and manages its own backup folder/files rather than requesting broad access to the user's entire Drive.
- The OAuth token cache is encrypted using Windows DPAPI for the current Windows user and stays on that PC.
- Backup archives contain the local product SQLite database and a small app/format/timestamp manifest. They do not contain RSS account profile details, passwords, RSS Core session tokens, or license records.
- RSS Core remains the source of truth for account and license information.
- Manual label creation and printing continue to work without Google Drive or an internet connection.

## Important

A real Google OAuth Desktop client JSON is required before Google Drive sign-in can work. Do not put OAuth client JSON, client secrets, access tokens, or refresh tokens in the source repository.
