# CampusPulse — Campus Events & Registration

**CampusPulse** is a local, offline campus event registration and attendance system.

It runs on your own computer using a local ASP.NET Core server. It does not require
Vercel, Render, GitHub, an internet-hosted API, or a cloud database. Your records are
saved locally in `API/data.json` (encrypted by the application).

## Requirements

- Windows, macOS, or Linux
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

After installing the .NET 8 SDK, you can disconnect from the internet.

## Run CampusPulse

1. Extract the ZIP file to a folder.
2. Open a terminal in the extracted `CCIT102-PROJ/API` folder.
3. Run:

   ```bash
   dotnet run
   ```

4. Wait until the terminal displays a local address, normally `http://localhost:5005`.
5. Open that address in your browser.

**Important:** Keep the terminal window open while using CampusPulse. Closing it stops
the local server. Do not open `FrontEnd/index.html` directly; visit the localhost
address so the website and API work together.

## Demo accounts

The login page lists the available demo accounts. The demo password is:

```text
password123
```

You can also create a student account using **Sign up**.

## Local data and backups

- Data is stored in `API/data.json`.
- The encryption key is stored in `API/data.key`.
- Keep both files together when backing up or moving your records.
- Do not share `data.key`; without it, encrypted data cannot be recovered.
- To back up your data, stop the app and copy both files to a safe location.

## Stop the app

Return to the terminal and press `Ctrl+C`.

## Project structure

```text
CCIT102-PROJ/
  API/       ASP.NET Core API and local encrypted data
  FrontEnd/  Website files served by the local API
  README.md  This guide
```

CampusPulse is configured for local/offline operation only. There are no deployment
steps or Vercel/Render settings in this version.
