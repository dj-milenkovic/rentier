# SonarLint IDE Setup Guide

> **Audience:** Every developer on the project
> **Time:** ~5 minutes
> **Cost:** Free

SonarLint runs the same code analysis rules locally in your IDE that SonarCloud
enforces in CI. You get real-time feedback as you type — no waiting for a push
or pull request.

---

## Benefits

- **Same rules locally as in CI** — no surprises when the pipeline runs
- **Real-time feedback** — issues are highlighted as you code
- **No infrastructure to maintain** — connects directly to SonarCloud
- **Free** — both the IDE extension and SonarCloud (for open-source) are free

---

## 1. Install the SonarLint Extension

### Visual Studio

1. Open **Extensions → Manage Extensions**.
2. Search for **SonarLint for Visual Studio**.
3. Click **Download** and restart Visual Studio when prompted.

### JetBrains Rider

1. Open **Settings → Plugins → Marketplace**.
2. Search for **SonarLint**.
3. Click **Install** and restart Rider when prompted.

---

## 2. Generate a SonarCloud User Token

If you do not already have a personal SonarCloud token:

1. Go to [sonarcloud.io](https://sonarcloud.io) and sign in with GitHub.
2. Click your avatar → **My Account → Security**.
3. Under *Generate Tokens*, enter a name (e.g. `sonarlint-local`) and click
   **Generate**.
4. Copy the token — you will need it in the next step.

> **Note:** This is a personal token, separate from the `SONAR_TOKEN` used in
> CI. Each developer creates their own.

---

## 3. Add a SonarCloud Connection

### Visual Studio

1. Go to **Tools → Options → SonarLint → Connected Mode**.
2. Click **Add Connection…**
3. Select **SonarCloud**.
4. Enter your **organization key** (same as the `SONAR_ORGANIZATION` value).
5. Paste your personal token.
6. Click **OK** / **Save**.

### JetBrains Rider

1. Go to **Settings → Tools → SonarLint → SonarCloud / SonarQube connections**.
2. Click **+** to add a new connection.
3. Select **SonarCloud**.
4. Enter the organization key and your personal token.
5. Click **OK**.

---

## 4. Bind the Project

### Visual Studio

1. In **Solution Explorer**, right-click the solution → **SonarLint → Bind to
   SonarCloud**.
2. Select the connection you created in step 3.
3. Select the **rentier** project from the project list.
4. Click **Bind**.

### JetBrains Rider

1. Go to **Settings → Tools → SonarLint → Project Settings**.
2. Check **Bind project to SonarCloud / SonarQube**.
3. Select your connection and the **rentier** project.
4. Click **OK**.

---

## 5. Verify It Works

1. Open any `.cs` file in the project.
2. Introduce a deliberate issue, for example:

   ```csharp
   string unused = "test"; // SonarLint should flag this as unused variable
   ```

3. Confirm that SonarLint underlines the issue with a warning squiggle.
4. Hover over the squiggle to see the rule ID and description.
5. Remove the test code.

---

## Keeping Rules in Sync

SonarLint in Connected Mode automatically synchronizes its rule set with
SonarCloud. When the project's quality profile changes on SonarCloud, your
local IDE picks up the changes on the next sync (usually within a few minutes
or on IDE restart).

No manual rule configuration is needed.

---

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| No issues shown | Verify the project is bound (step 4) and the connection is active |
| "Authentication failed" | Regenerate your personal token on sonarcloud.io |
| Rules differ from CI | Check that you are bound to the correct SonarCloud project |
| Old rules after profile change | Restart the IDE or trigger a manual sync in SonarLint settings |
